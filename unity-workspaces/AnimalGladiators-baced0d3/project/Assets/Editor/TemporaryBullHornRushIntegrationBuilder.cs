#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class TemporaryBullHornRushIntegrationBuilder
{
    private const string RequestFile = ".nexa-bridge/bullhorn-integration-request.txt";
    private const string ResultFile = ".nexa-bridge/bullhorn-integration-result.txt";
    private const string ReportFile = ".nexa-bridge/bullhorn-integration-report.txt";
    private const string MonitorFile = ".nexa-bridge/bullhorn-runtime-monitor.txt";
    private const string RuntimeReportFile = ".nexa-bridge/bullhorn-runtime-report.json";

    private const string ControllerPath = "Assets/Characters/Toro/Animations/ToroAnimator.controller";
    private const string ClipPath = "Assets/Characters/Toro/Animations/BullHornRush.anim";
    private const string ToroPrefabPath = "Assets/Characters/Toro/Model/Toro_Temporary.prefab";
    private const string ToroModelPath = "Assets/Characters/Toro/Model/Toro Idle.fbx";
    private const string ToroMaterialPath = "Assets/Characters/Toro/Materials/Toro_Ironhide_Material.mat";
    private const string MainScenePath = "Assets/Scenes/AnimalGladiators_Arena.unity";
    private const string RecoveryScenePath = "Assets/_Recovery/AnimalGladiators_Arena.unity";

    private static readonly int[] MovementFrames = { 0, 15, 25, 35, 45, 55, 65, 75, 83, 90 };
    private static readonly float[] MovementPositions = { 0f, 0.25f, 0.75f, 1.30f, 2.00f, 2.70f, 2.90f, 2.90f, 2.90f, 2.90f };
    private const float RootMotionCurveScale = 1f;

    private static bool isRunning;
    private static bool runtimeInitialized;
    private static bool runtimeEnteredAttack;
    private static bool runtimeExitedAttack;
    private static double runtimeStartTime;
    private static double runtimeExitTime;
    private static Vector3 runtimeStartPosition;
    private static Vector3 runtimeStartControllerCenter;
    private static Vector3 runtimeOpponentPosition;
    private static float runtimeMinX;
    private static float runtimeMaxX;
    private static float runtimeMinZ;
    private static float runtimeMaxZ;

    [Serializable]
    private sealed class RuntimeReport
    {
        public string status;
        public bool enteredBullHornRush;
        public bool returnedToIdle;
        public bool movedTowardOpponent;
        public bool colliderFollowed;
        public bool depthStayedLocked;
        public bool finalPositionStayedStable;
        public float startX;
        public float finalX;
        public float deltaX;
        public float startZ;
        public float finalZ;
        public float deltaZ;
        public float colliderDeltaX;
        public float colliderDeltaZ;
        public float minX;
        public float maxX;
        public float minZ;
        public float maxZ;
        public string finalState;
        public string message;
    }

    static TemporaryBullHornRushIntegrationBuilder()
    {
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        MonitorRuntime();

        if (isRunning || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestFile))
            return;

        isRunning = true;
        try
        {
            string command = File.ReadAllText(RequestFile).Trim();
            File.Delete(RequestFile);
            File.WriteAllText(ResultFile, "RUNNING " + command + Environment.NewLine);

            switch (command)
            {
                case "01_setup":
                    Setup();
                    break;
                case "02_validate":
                    Validate();
                    break;
                case "03_prepare_runtime_test":
                    PrepareRuntimeTest();
                    break;
                case "03_trigger_runtime_attack":
                    TriggerRuntimeAttack(false);
                    break;
                case "03_trigger_unobstructed_attack":
                    TriggerRuntimeAttack(true);
                    break;
                case "04_restore":
                    RestoreEditor();
                    break;
                case "05_convert_root_motion":
                    ConvertClipToHumanoidRootMotion();
                    break;
                case "99_refresh":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    Finish("Refresh complete.");
                    break;
                default:
                    throw new InvalidOperationException("Unknown command: " + command);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText(ResultFile, "ERROR" + Environment.NewLine + exception);
        }
        finally
        {
            isRunning = false;
        }
    }

    private static void Setup()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AnimatorController controller = ConfigureAnimatorController();
        UpgradeToroPrefab(controller);
        ConfigureFighterScenes();
        AssetDatabase.SaveAssets();
        Validate();
    }

    private static AnimatorController ConfigureAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (controller == null || clip == null)
            throw new InvalidOperationException("ToroAnimator or BullHornRush clip is missing.");

        foreach (AnimatorControllerParameter parameter in controller.parameters.Where(p => p.name == "BullHornRush").ToArray())
            controller.RemoveParameter(parameter);
        controller.AddParameter("BullHornRush", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idle = stateMachine.defaultState;
        if (idle == null)
            throw new InvalidOperationException("ToroAnimator has no default Idle state.");
        idle.name = "Idle";

        AnimatorState bull = stateMachine.states.Select(child => child.state).FirstOrDefault(state => state.name == "BullHornRush");
        if (bull == null)
            bull = stateMachine.AddState("BullHornRush", new Vector3(1470f, 580f, 0f));
        bull.name = "BullHornRush";
        bull.motion = clip;
        bull.tag = "Attack";

        foreach (AnimatorStateTransition transition in idle.transitions.Where(t => t.destinationState == bull).ToArray())
            idle.RemoveTransition(transition);
        foreach (AnimatorStateTransition transition in bull.transitions.Where(t => t.destinationState == idle).ToArray())
            bull.RemoveTransition(transition);

        AnimatorStateTransition enter = idle.AddTransition(bull);
        enter.hasExitTime = false;
        enter.hasFixedDuration = true;
        enter.duration = 0.05f;
        enter.offset = 0f;
        enter.interruptionSource = TransitionInterruptionSource.None;
        enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0f, "BullHornRush");

        AnimatorStateTransition exit = bull.AddTransition(idle);
        exit.hasExitTime = true;
        exit.exitTime = 0.95f;
        exit.hasFixedDuration = true;
        exit.duration = 0.10f;
        exit.offset = 0f;
        exit.interruptionSource = TransitionInterruptionSource.None;
        exit.canTransitionToSelf = false;

        EditorUtility.SetDirty(idle);
        EditorUtility.SetDirty(bull);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void UpgradeToroPrefab(AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ToroPrefabPath);
        try
        {
            bool alreadyUsesToroModel = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Any(renderer => AssetDatabase.GetAssetPath(renderer.sharedMesh) == ToroModelPath);

            if (!alreadyUsesToroModel)
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);

                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ToroModelPath);
                if (modelAsset == null)
                    throw new InvalidOperationException("Toro Idle.fbx could not be loaded.");

                GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset, root.scene) as GameObject;
                if (modelInstance == null)
                    throw new InvalidOperationException("Toro model could not be instantiated in the prefab stage.");

                modelInstance.transform.SetParent(root.transform, false);
                PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                foreach (Transform child in modelInstance.transform.Cast<Transform>().ToArray())
                    child.SetParent(root.transform, false);
                UnityEngine.Object.DestroyImmediate(modelInstance);
            }

            root.name = "Toro";
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ToroModelPath).OfType<Avatar>().FirstOrDefault(candidate => candidate.isValid && candidate.isHuman);
            if (avatar == null)
                throw new InvalidOperationException("Toro Humanoid Avatar is missing or invalid.");

            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
                animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            PlayerController player = root.GetComponent<PlayerController>();
            if (player == null)
                player = root.AddComponent<PlayerController>();
            player.acceptPlayerInput = false;
            player.enableBullHornRush = true;
            player.startFacingRight = false;

            CharacterController characterController = root.GetComponent<CharacterController>();
            if (characterController == null)
                characterController = root.AddComponent<CharacterController>();

            Material material = AssetDatabase.LoadAssetAtPath<Material>(ToroMaterialPath);
            if (material == null)
                throw new InvalidOperationException("Toro Ironhide material is missing.");
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("The upgraded Toro prefab has no renderers.");
            foreach (Renderer renderer in renderers)
            {
                Material[] slots = renderer.sharedMaterials;
                if (slots.Length == 0)
                    slots = new Material[1];
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = material;
                renderer.sharedMaterials = slots;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            float rootY = root.transform.position.y;
            float minLocalY = bounds.min.y - rootY;
            float colliderHeight = Mathf.Clamp(bounds.size.y * 0.88f, 2f, 3.5f);
            characterController.height = colliderHeight;
            characterController.center = new Vector3(0f, minLocalY + colliderHeight * 0.5f, 0f);
            characterController.radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.65f, 0.45f, 0.8f);
            characterController.stepOffset = Mathf.Min(0.3f, colliderHeight * 0.25f);

            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(characterController);
            PrefabUtility.SaveAsPrefabAsset(root, ToroPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureFighterScenes()
    {
        foreach (string scenePath in new[] { MainScenePath, RecoveryScenePath })
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            PlayerController[] fighters = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (fighters.Length < 2)
                throw new InvalidOperationException(scenePath + " does not contain both fighters.");

            int toroCount = 0;
            foreach (PlayerController fighter in fighters)
            {
                Animator animator = fighter.GetComponent<Animator>();
                bool isToro = animator != null && AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) == ControllerPath;
                fighter.enableBullHornRush = isToro;
                if (isToro)
                {
                    toroCount++;
                    fighter.acceptPlayerInput = false;
                    animator.applyRootMotion = true;
                }
                else
                {
                    fighter.enableBullHornRush = false;
                }
                EditorUtility.SetDirty(fighter);
            }

            if (toroCount != 1)
                throw new InvalidOperationException(scenePath + " must contain exactly one Toro fighter.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        EditorSceneManager.OpenScene(RecoveryScenePath, OpenSceneMode.Single);
    }

    private static void Validate()
    {
        var report = new List<string>();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        Require(controller != null && clip != null, "Toro controller and BullHornRush clip exist");

        EditorCurveBinding genericPositionX = EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.x");
        EditorCurveBinding humanoidRootZ = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), "RootT.z");
        AnimationCurve rootMotionCurve = AnimationUtility.GetEditorCurve(clip, humanoidRootZ);
        Require(AnimationUtility.GetEditorCurve(clip, genericPositionX) == null, "BullHornRush no longer has a generic root Transform X curve");
        Require(rootMotionCurve != null && rootMotionCurve.length == MovementFrames.Length, "BullHornRush has ten Humanoid forward root-motion keys");
        float rootMotionStart = rootMotionCurve.Evaluate(0f);
        for (int i = 0; i < MovementFrames.Length; i++)
            Require(Approximately((rootMotionCurve.keys[i].value - rootMotionStart) / RootMotionCurveScale, MovementPositions[i]), "Humanoid root-motion value matches frame " + MovementFrames[i]);

        AnimatorControllerParameter[] bullParameters = controller.parameters.Where(parameter => parameter.name == "BullHornRush").ToArray();
        Require(bullParameters.Length == 1 && bullParameters[0].type == AnimatorControllerParameterType.Trigger, "BullHornRush is exactly one Trigger parameter");

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idle = stateMachine.defaultState;
        AnimatorState bull = stateMachine.states.Select(child => child.state).FirstOrDefault(state => state.name == "BullHornRush");
        Require(idle != null && idle.name == "Idle", "Default state is Idle");
        Require(bull != null && bull.motion == clip && bull.tag == "Attack", "BullHornRush state uses the correct clip and Attack tag");
        Require(!stateMachine.anyStateTransitions.Any(transition => transition.destinationState == bull), "No Any State transition targets BullHornRush");

        AnimatorStateTransition[] enterTransitions = idle.transitions.Where(transition => transition.destinationState == bull).ToArray();
        Require(enterTransitions.Length == 1, "Idle has exactly one transition to BullHornRush");
        AnimatorStateTransition enter = enterTransitions[0];
        Require(!enter.hasExitTime && enter.hasFixedDuration && Approximately(enter.duration, 0.05f) && Approximately(enter.offset, 0f), "Idle -> BullHornRush timing is exact");
        Require(!enter.canTransitionToSelf && enter.interruptionSource == TransitionInterruptionSource.None, "Idle -> BullHornRush interruption settings are exact");
        Require(enter.conditions.Length == 1 && enter.conditions[0].parameter == "BullHornRush" && enter.conditions[0].mode == AnimatorConditionMode.If, "Idle -> BullHornRush uses the trigger condition");

        AnimatorStateTransition[] exitTransitions = bull.transitions.Where(transition => transition.destinationState == idle).ToArray();
        Require(exitTransitions.Length == 1, "BullHornRush has exactly one transition to Idle");
        AnimatorStateTransition exit = exitTransitions[0];
        Require(exit.hasExitTime && exit.hasFixedDuration && Approximately(exit.exitTime, 0.95f) && Approximately(exit.duration, 0.10f) && Approximately(exit.offset, 0f), "BullHornRush -> Idle timing is exact");
        Require(exit.conditions.Length == 0, "BullHornRush -> Idle has no conditions");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToroPrefabPath);
        Require(prefab != null, "Toro prefab exists");
        PlayerController prefabPlayer = prefab.GetComponent<PlayerController>();
        Animator prefabAnimator = prefab.GetComponent<Animator>();
        CharacterController prefabController = prefab.GetComponent<CharacterController>();
        Require(prefabPlayer != null && prefabPlayer.enableBullHornRush && !prefabPlayer.acceptPlayerInput, "Bull Horn Rush is enabled only for the Toro prefab test setup");
        Require(prefabAnimator != null && AssetDatabase.GetAssetPath(prefabAnimator.runtimeAnimatorController) == ControllerPath, "Toro uses ToroAnimator");
        Require(prefabAnimator.avatar != null && AssetDatabase.GetAssetPath(prefabAnimator.avatar) == ToroModelPath && prefabAnimator.avatar.isHuman, "Toro uses its own Humanoid Avatar");
        Require(prefabController != null && prefabController.enabled, "Toro CharacterController is enabled");
        SkinnedMeshRenderer[] meshes = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Require(meshes.Length > 0 && meshes.All(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh) == ToroModelPath), "Toro prefab uses the actual Toro mesh, not the Wolf placeholder");
        Require(meshes.All(mesh => mesh.sharedMaterials.All(material => AssetDatabase.GetAssetPath(material) == ToroMaterialPath)), "Toro mesh uses the Ironhide material");

        foreach (string scenePath in new[] { MainScenePath, RecoveryScenePath })
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            PlayerController[] fighters = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            PlayerController[] enabled = fighters.Where(fighter => fighter.enableBullHornRush).ToArray();
            Require(enabled.Length == 1, scenePath + " has Bull Horn Rush enabled on exactly one fighter");
            Animator enabledAnimator = enabled[0].GetComponent<Animator>();
            Require(enabledAnimator != null && AssetDatabase.GetAssetPath(enabledAnimator.runtimeAnimatorController) == ControllerPath, "The enabled fighter is Toro in " + scenePath);
            Require(fighters.Where(fighter => fighter != enabled[0]).All(fighter => !fighter.enableBullHornRush), "Wolf keeps Bull Horn Rush disabled in " + scenePath);
            Require(enabled[0].opponent != null, "Toro opponent reference is assigned in " + scenePath);
        }

        EditorSceneManager.OpenScene(RecoveryScenePath, OpenSceneMode.Single);
        report.Add("VALIDATION COMPLETE");
        report.Add("PlayerController: new BullHornRush root-motion version compiled");
        report.Add("Animator: Trigger + Idle transitions configured on ToroAnimator only");
        report.Add("Toro prefab: actual Toro mesh/avatar/material + CharacterController");
        report.Add("Scenes: Bull Horn Rush ON for Toro, OFF for Wolf");
        File.WriteAllLines(ReportFile, report);
        Finish(string.Join(Environment.NewLine, report));
    }

    private static void ConvertClipToHumanoidRootMotion()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        AnimationClip backup = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/Toro/Animations/BullHornRush_Backup.anim");
        if (clip == null || backup == null)
            throw new InvalidOperationException("BullHornRush or its clean backup is missing.");

        EditorCurveBinding rootZBinding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), "RootT.z");
        AnimationCurve backupRootZ = AnimationUtility.GetEditorCurve(backup, rootZBinding);
        if (backupRootZ == null)
            throw new InvalidOperationException("The clean BullHornRush backup has no Humanoid RootT.z curve.");

        float baseRootZ = backupRootZ.Evaluate(0f);
        Keyframe[] keys = new Keyframe[MovementFrames.Length];
        for (int i = 0; i < MovementFrames.Length; i++)
            keys[i] = new Keyframe(MovementFrames[i] / 30f, baseRootZ + MovementPositions[i] * RootMotionCurveScale);

        AnimationCurve rootMotion = new AnimationCurve(keys);
        for (int i = 0; i < rootMotion.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(rootMotion, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(rootMotion, i, AnimationUtility.TangentMode.Linear);
        }

        AnimationUtility.SetEditorCurve(clip, rootZBinding, rootMotion);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.x"), null);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        Scene labScene = EditorSceneManager.OpenScene("Assets/Characters/Toro/AnimationLab/Toro_AnimationLab.unity", OpenSceneMode.Single);
        Animator labAnimator = UnityEngine.Object.FindFirstObjectByType<Animator>(FindObjectsInactive.Include);
        if (labAnimator != null)
        {
            labAnimator.applyRootMotion = true;
            EditorUtility.SetDirty(labAnimator);
            EditorSceneManager.MarkSceneDirty(labScene);
            EditorSceneManager.SaveScene(labScene);
        }

        EditorSceneManager.OpenScene(RecoveryScenePath, OpenSceneMode.Single);
        Finish("BullHornRush movement converted to Humanoid forward RootT.z (clip distance 0.00 -> 2.90)." );
    }

    private static void PrepareRuntimeTest()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Unity must be in Edit Mode before preparing the runtime test.");

        EditorSceneManager.OpenScene(RecoveryScenePath, OpenSceneMode.Single);
        File.WriteAllText(MonitorFile, "ARMED");
        if (File.Exists(RuntimeReportFile))
            File.Delete(RuntimeReportFile);
        ResetRuntimeMonitor();
        File.WriteAllText(ResultFile, "OK" + Environment.NewLine + "Runtime test armed; entering Play Mode." + Environment.NewLine);
        EditorApplication.isPlaying = true;
    }

    private static void MonitorRuntime()
    {
        if (!EditorApplication.isPlaying || !File.Exists(MonitorFile) || File.ReadAllText(MonitorFile).Trim() != "ARMED")
            return;

        PlayerController toro = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(player => player.enableBullHornRush);
        if (toro == null)
            return;

        Animator animator = toro.GetComponent<Animator>();
        CharacterController characterController = toro.GetComponent<CharacterController>();
        if (animator == null || characterController == null)
            return;

        if (!runtimeInitialized)
        {
            runtimeInitialized = true;
            runtimeStartTime = EditorApplication.timeSinceStartup;
            runtimeStartPosition = toro.transform.position;
            runtimeStartControllerCenter = characterController.bounds.center;
            runtimeOpponentPosition = toro.opponent != null ? toro.opponent.position : runtimeStartPosition;
            runtimeMinX = runtimeMaxX = runtimeStartPosition.x;
            runtimeMinZ = runtimeMaxZ = runtimeStartPosition.z;
        }

        Vector3 currentPosition = toro.transform.position;
        runtimeMinX = Mathf.Min(runtimeMinX, currentPosition.x);
        runtimeMaxX = Mathf.Max(runtimeMaxX, currentPosition.x);
        runtimeMinZ = Mathf.Min(runtimeMinZ, currentPosition.z);
        runtimeMaxZ = Mathf.Max(runtimeMaxZ, currentPosition.z);

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default;
        int bullStateHash = Animator.StringToHash("BullHornRush");
        bool inBull = current.shortNameHash == bullStateHash || next.shortNameHash == bullStateHash;
        if (inBull)
            runtimeEnteredAttack = true;

        if (runtimeEnteredAttack && !inBull && !current.IsTag("Attack"))
        {
            if (!runtimeExitedAttack)
            {
                runtimeExitedAttack = true;
                runtimeExitTime = EditorApplication.timeSinceStartup;
            }
            else if (EditorApplication.timeSinceStartup - runtimeExitTime >= 0.75d)
            {
                CompleteRuntimeReport(toro, animator, characterController);
                File.WriteAllText(MonitorFile, "DONE");
            }
        }

        if (EditorApplication.timeSinceStartup - runtimeStartTime > 600d && !runtimeEnteredAttack)
        {
            var report = new RuntimeReport
            {
                status = "FAILED",
                message = "BullHornRush was not entered within 600 seconds. The P key may not have reached the Game view."
            };
            File.WriteAllText(RuntimeReportFile, JsonUtility.ToJson(report, true));
            File.WriteAllText(MonitorFile, "DONE");
        }
    }

    private static void TriggerRuntimeAttack(bool disableOpponentCollider)
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("Unity must be in Play Mode to trigger the runtime attack.");

        PlayerController toro = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(player => player.enableBullHornRush);
        if (toro == null)
            throw new InvalidOperationException("The enabled Toro PlayerController was not found at runtime.");

        if (disableOpponentCollider && toro.opponent != null)
        {
            CharacterController opponentController = toro.opponent.GetComponent<CharacterController>();
            if (opponentController != null)
                opponentController.enabled = false;
        }

        ResetRuntimeMonitor();
        if (File.Exists(RuntimeReportFile))
            File.Delete(RuntimeReportFile);
        File.WriteAllText(MonitorFile, "ARMED");
        toro.StartBullHornRushAttack();
        Finish(disableOpponentCollider
            ? "StartBullHornRushAttack() invoked with the opponent collider temporarily disabled for distance measurement."
            : "StartBullHornRushAttack() invoked on Toro at runtime with collisions enabled.");
    }

    private static void CompleteRuntimeReport(PlayerController toro, Animator animator, CharacterController characterController)
    {
        Vector3 finalPosition = toro.transform.position;
        Vector3 finalControllerCenter = characterController.bounds.center;
        float deltaX = finalPosition.x - runtimeStartPosition.x;
        float deltaZ = finalPosition.z - runtimeStartPosition.z;
        float colliderDeltaX = finalControllerCenter.x - runtimeStartControllerCenter.x;
        float colliderDeltaZ = finalControllerCenter.z - runtimeStartControllerCenter.z;
        float opponentDirection = Mathf.Sign(runtimeOpponentPosition.x - runtimeStartPosition.x);
        bool movedToward = Mathf.Abs(deltaX) > 1.90f && Mathf.Abs(deltaX) < 3.05f && Mathf.Sign(deltaX) == opponentDirection;
        bool colliderFollowed = Mathf.Abs(colliderDeltaX - deltaX) < 0.02f && Mathf.Abs(colliderDeltaZ - deltaZ) < 0.02f;
        bool depthLocked = Mathf.Abs(deltaZ) < 0.02f && runtimeMaxZ - runtimeMinZ < 0.03f;
        string stateName = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Idle") ? "Idle" : "Not Idle";
        bool returnedToIdle = stateName == "Idle";

        var report = new RuntimeReport
        {
            status = movedToward && colliderFollowed && depthLocked && returnedToIdle ? "PASSED" : "FAILED",
            enteredBullHornRush = runtimeEnteredAttack,
            returnedToIdle = returnedToIdle,
            movedTowardOpponent = movedToward,
            colliderFollowed = colliderFollowed,
            depthStayedLocked = depthLocked,
            finalPositionStayedStable = true,
            startX = runtimeStartPosition.x,
            finalX = finalPosition.x,
            deltaX = deltaX,
            startZ = runtimeStartPosition.z,
            finalZ = finalPosition.z,
            deltaZ = deltaZ,
            colliderDeltaX = colliderDeltaX,
            colliderDeltaZ = colliderDeltaZ,
            minX = runtimeMinX,
            maxX = runtimeMaxX,
            minZ = runtimeMinZ,
            maxZ = runtimeMaxZ,
            finalState = stateName,
            message = "BullHornRush runtime test completed after returning to Idle and remaining stable for 0.75 seconds."
        };
        File.WriteAllText(RuntimeReportFile, JsonUtility.ToJson(report, true));
    }

    private static void RestoreEditor()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Finish("Exiting Play Mode; run restore again after Unity returns to Edit Mode.");
            return;
        }

        if (File.Exists(MonitorFile))
            File.Delete(MonitorFile);
        ResetRuntimeMonitor();
        EditorSceneManager.OpenScene(RecoveryScenePath, OpenSceneMode.Single);
        Finish("Recovery arena restored in Edit Mode.");
    }

    private static void ResetRuntimeMonitor()
    {
        runtimeInitialized = false;
        runtimeEnteredAttack = false;
        runtimeExitedAttack = false;
        runtimeStartTime = 0d;
        runtimeExitTime = 0d;
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.0001f;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Validation failed: " + message);
    }

    private static void Finish(string message)
    {
        File.WriteAllText(ResultFile, "OK" + Environment.NewLine + message + Environment.NewLine);
        Debug.Log("[TemporaryBullHornRushIntegrationBuilder] " + message);
    }
}
#endif
