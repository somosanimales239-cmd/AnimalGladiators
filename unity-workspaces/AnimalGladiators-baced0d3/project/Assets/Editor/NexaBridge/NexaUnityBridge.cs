using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

[InitializeOnLoad]
public static class NexaUnityBridge {
    public const string NEXA_BRIDGE_PLUGIN_VERSION = "1.6.2";

    [Serializable]
    class BridgeRequest {
        public string request_id;
        public string action;
        public bool capture_scene;
        public bool capture_game;
        public string scene_path;
        public string menu_item;
    }

    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string Dir => Path.Combine(Root, ".nexa-bridge");
    static string RequestFile => Path.Combine(Dir, "request.json");
    static string CommandResultFile => Path.Combine(Dir, "unity-command-result.json");
    static string CompileErrorsFile => Path.Combine(Dir, "compile-errors.json");
    static string CompileStateFile => Path.Combine(Dir, "compile-state.json");

    static DateTime lastTick = DateTime.MinValue;
    static DateTime lastRequestWrite = DateTime.MinValue;
    static readonly List<string> compileErrors = new List<string>();

    static NexaUnityBridge() {
        Directory.CreateDirectory(Dir);
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
        CompilationPipeline.compilationStarted += OnCompilationStarted;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        WriteCompileErrors();
        WriteCompileState("idle");
        WriteState();
    }

    static void Tick() {
        if ((DateTime.UtcNow-lastTick).TotalSeconds < 0.75) return;
        lastTick=DateTime.UtcNow;
        WriteState();
        try {
            if (!File.Exists(RequestFile)) return;
            var wt=File.GetLastWriteTimeUtc(RequestFile);
            if (wt<=lastRequestWrite) return;
            lastRequestWrite=wt;
            var text=File.ReadAllText(RequestFile);
            var request=JsonUtility.FromJson<BridgeRequest>(text) ?? new BridgeRequest();
            ExecuteRequest(request);
        } catch(Exception e) {
            WriteCommandResult("", false, e.ToString());
        }
    }

    static void ExecuteRequest(BridgeRequest request) {
        string action=(request.action??"").Trim().ToLowerInvariant();
        try {
            if (request.capture_scene || action=="capture_scene" || action=="capture_views") CaptureScene();
            if (request.capture_game || action=="capture_game" || action=="capture_views") CaptureGame();
            switch(action) {
                case "": break;
                case "capture_scene":
                case "capture_game":
                case "capture_views": break;
                case "refresh_assets": AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); break;
                case "save_assets": AssetDatabase.SaveAssets(); break;
                case "play": if(!EditorApplication.isPlaying) EditorApplication.isPlaying=true; break;
                case "stop": if(EditorApplication.isPlaying) EditorApplication.isPlaying=false; break;
                case "pause": EditorApplication.isPaused=true; break;
                case "unpause": EditorApplication.isPaused=false; break;
                case "save_scene":
                    if(SceneManager.GetActiveScene().IsValid()) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    break;
                case "open_scene":
                    if(string.IsNullOrWhiteSpace(request.scene_path)) throw new Exception("scene_path is required");
                    EditorSceneManager.OpenScene(request.scene_path, OpenSceneMode.Single);
                    break;
                case "execute_menu_item":
                    if(string.IsNullOrWhiteSpace(request.menu_item)) throw new Exception("menu_item is required");
                    if(!EditorApplication.ExecuteMenuItem(request.menu_item)) throw new Exception("Unity menu item was not found or could not execute: "+request.menu_item);
                    break;
                default: throw new Exception("Unsupported Nexa Unity action: "+action);
            }
            WriteState();
            WriteCommandResult(request.request_id, true, "");
        } catch(Exception e) {
            WriteCommandResult(request.request_id, false, e.ToString());
        }
    }

    static void WriteCommandResult(string requestId,bool ok,string error) {
        var json="{"+
            "\"request_id\":\""+Escape(requestId)+"\","+
            "\"ok\":"+(ok?"true":"false")+","+
            "\"error\":\""+Escape(error)+"\","+
            "\"updated_at\":\""+DateTime.UtcNow.ToString("o")+"\"}";
        File.WriteAllText(CommandResultFile,json);
    }

    static void WriteState() {
        var scene=SceneManager.GetActiveScene();
        var json="{"+
            "\"updated_at\":\""+DateTime.UtcNow.ToString("o")+"\","+
            "\"bridge_plugin_version\":\""+NEXA_BRIDGE_PLUGIN_VERSION+"\","+
            "\"unity_version\":\""+Escape(Application.unityVersion)+"\","+
            "\"is_playing\":"+(EditorApplication.isPlaying?"true":"false")+","+
            "\"is_paused\":"+(EditorApplication.isPaused?"true":"false")+","+
            "\"is_compiling\":"+(EditorApplication.isCompiling?"true":"false")+","+
            "\"active_scene\":\""+Escape(scene.name)+"\","+
            "\"active_scene_path\":\""+Escape(scene.path)+"\"}";
        File.WriteAllText(Path.Combine(Dir,"unity-state.json"),json);
    }

    static void OnPlayMode(PlayModeStateChange state) {
        File.WriteAllText(Path.Combine(Dir,"playmode.json"),"{\"last_event\":\""+state+"\",\"updated_at\":\""+DateTime.UtcNow.ToString("o")+"\"}");
        WriteState();
    }

    static void OnCompilationStarted(object context) {
        compileErrors.Clear();
        WriteCompileErrors();
        WriteCompileState("started");
        WriteState();
    }

    static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages) {
        foreach(var m in messages) {
            if(m.type != CompilerMessageType.Error) continue;
            var message=m.file+"("+m.line+","+m.column+"): "+m.message;
            if(!compileErrors.Contains(message)) compileErrors.Add(message);
        }
        if(compileErrors.Count>100) compileErrors.RemoveRange(0,compileErrors.Count-100);
        WriteCompileErrors();
        WriteCompileState("compiling");
        WriteState();
    }

    static void OnCompilationFinished(object context) {
        WriteCompileErrors();
        WriteCompileState("finished");
        WriteState();
    }

    static void WriteCompileErrors() {
        var sb=new StringBuilder("[");
        for(int i=0;i<compileErrors.Count;i++) {
            if(i>0) sb.Append(',');
            var msg=compileErrors[i];
            var code="";
            var match=System.Text.RegularExpressions.Regex.Match(msg, @"\bCS\d+\b");
            if(match.Success) code=match.Value;
            sb.Append("{\"message\":\"").Append(Escape(msg)).Append("\",\"code\":\"").Append(Escape(code)).Append("\"}");
        }
        sb.Append(']');
        File.WriteAllText(CompileErrorsFile,sb.ToString());
    }

    static void WriteCompileState(string phase) {
        File.WriteAllText(CompileStateFile,"{\"phase\":\""+Escape(phase)+"\",\"error_count\":"+compileErrors.Count+",\"updated_at\":\""+DateTime.UtcNow.ToString("o")+"\"}");
    }

    static string Escape(string s) {
        return (s??"").Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n");
    }

    static void CaptureScene() {
        var sv=SceneView.lastActiveSceneView;
        if(sv==null || sv.camera==null) return;
        CaptureCamera(sv.camera,Path.Combine(Dir,"scene-view.png"),1280,720);
    }

    static void CaptureGame() {
        Camera cam=Camera.main;
#if UNITY_2022_2_OR_NEWER
        if(cam==null) cam=UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
        if(cam==null) cam=UnityEngine.Object.FindObjectOfType<Camera>();
#endif
        if(cam==null) return;
        CaptureCamera(cam,Path.Combine(Dir,"game-view.png"),1280,720);
    }

    static void CaptureCamera(Camera cam,string file,int width,int height) {
        var oldTarget=cam.targetTexture;
        var oldActive=RenderTexture.active;
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
        try {
            cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,width,height),0,0); tex.Apply(); File.WriteAllBytes(file,tex.EncodeToPNG());
        } finally {
            cam.targetTexture=oldTarget; RenderTexture.active=oldActive;
            UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}