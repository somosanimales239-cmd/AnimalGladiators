#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NexaJumpRootMotionAudit
{
static NexaJumpRootMotionAudit()
{
Run();
}

private static void Run()
{
    if (EditorApplication.isPlayingOrWillChangePlaymode)
        return;

    try
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string outputDir =
            Path.Combine(projectRoot, ".nexa-bridge");

        Directory.CreateDirectory(outputDir);

        string outputPath =
            Path.Combine(outputDir, "jump-root-motion-audit.txt");

        var sb = new StringBuilder(24000);

        sb.AppendLine("NEXA JUMP ROOT MOTION AUDIT");
        sb.AppendLine("===========================");
        sb.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
        sb.AppendLine("Play Mode: " + EditorApplication.isPlaying);
        sb.AppendLine();

        PlayerController[] fighters =
            UnityEngine.Object.FindObjectsByType<PlayerController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < fighters.Length; i++)
        {
            PlayerController fighter = fighters[i];
            Animator animator = fighter.GetComponent<Animator>();

            if (animator == null)
                animator = fighter.GetComponentInChildren<Animator>(true);

            sb.AppendLine("==================================================");
            sb.AppendLine("FIGHTER: " + fighter.name);
            sb.AppendLine("==================================================");

            if (animator == null)
            {
                sb.AppendLine("Animator: MISSING");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine(
                "Serialized Apply Root Motion: " +
                animator.applyRootMotion
            );

            sb.AppendLine(
                "Animator object: " +
                GetHierarchyPath(animator.transform)
            );

            RuntimeAnimatorController runtime =
                animator.runtimeAnimatorController;

            if (runtime == null)
            {
                sb.AppendLine("Controller: <null>");
                sb.AppendLine();
                continue;
            }

            string controllerPath =
                AssetDatabase.GetAssetPath(runtime);

            sb.AppendLine("Controller: " + runtime.name);
            sb.AppendLine("Controller Asset: " + controllerPath);
            sb.AppendLine();

            AnimatorController controller =
                runtime as AnimatorController;

            if (controller == null)
            {
                sb.AppendLine(
                    "Controller is not a direct AnimatorController."
                );
                sb.AppendLine();
                continue;
            }

            for (int layerIndex = 0;
                 layerIndex < controller.layers.Length;
                 layerIndex++)
            {
                AnimatorControllerLayer layer =
                    controller.layers[layerIndex];

                sb.AppendLine(
                    "[LAYER " + layerIndex + "] " +
                    layer.name
                );

                InspectStateMachine(
                    sb,
                    layer.stateMachine,
                    layer.name
                );
            }

            sb.AppendLine();
        }

        File.WriteAllText(
            outputPath,
            sb.ToString(),
            new UTF8Encoding(false)
        );

        Debug.Log(
            "[NexaJumpRootMotionAudit] Written: " +
            outputPath
        );
    }
    catch (Exception ex)
    {
        Debug.LogError(
            "[NexaJumpRootMotionAudit] " + ex
        );
    }
}

private static void InspectStateMachine(
    StringBuilder sb,
    AnimatorStateMachine machine,
    string prefix
)
{
    foreach (ChildAnimatorState child in machine.states)
    {
        AnimatorState state = child.state;

        bool looksLikeJump =
            state.name.IndexOf(
                "jump",
                StringComparison.OrdinalIgnoreCase
            ) >= 0 ||
            state.tag.IndexOf(
                "jump",
                StringComparison.OrdinalIgnoreCase
            ) >= 0;

        if (!looksLikeJump)
            continue;

        sb.AppendLine();
        sb.AppendLine(
            "STATE: " + prefix + "/" + state.name
        );

        sb.AppendLine("Tag: " + state.tag);
        sb.AppendLine("Speed: " + state.speed);
        sb.AppendLine(
            "Write Defaults: " + state.writeDefaultValues
        );

        InspectMotion(sb, state.motion, "  ");
    }

    foreach (ChildAnimatorStateMachine childMachine
             in machine.stateMachines)
    {
        InspectStateMachine(
            sb,
            childMachine.stateMachine,
            prefix + "/" + childMachine.stateMachine.name
        );
    }
}

private static void InspectMotion(
    StringBuilder sb,
    Motion motion,
    string indent
)
{
    if (motion == null)
    {
        sb.AppendLine(indent + "Motion: <null>");
        return;
    }

    sb.AppendLine(
        indent + "Motion Type: " +
        motion.GetType().Name
    );

    sb.AppendLine(
        indent + "Motion Name: " +
        motion.name
    );

    string assetPath =
        AssetDatabase.GetAssetPath(motion);

    sb.AppendLine(
        indent + "Motion Asset: " +
        assetPath
    );

    AnimationClip clip = motion as AnimationClip;

    if (clip != null)
    {
        sb.AppendLine(
            indent + "Length: " +
            clip.length.ToString("0.######")
        );

        sb.AppendLine(
            indent + "Frame Rate: " +
            clip.frameRate.ToString("0.######")
        );

        sb.AppendLine(
            indent + "Looping: " +
            clip.isLooping
        );

        sb.AppendLine(
            indent + "Human Motion: " +
            clip.humanMotion
        );

        sb.AppendLine(
            indent + "Has Root Curves: " +
            clip.hasRootCurves
        );

        sb.AppendLine(
            indent + "Has Motion Curves: " +
            clip.hasMotionCurves
        );

        sb.AppendLine(
            indent + "Average Speed: " +
            V(clip.averageSpeed)
        );

        sb.AppendLine(
            indent + "Apparent Speed: " +
            clip.apparentSpeed.ToString("0.######")
        );

        EditorCurveBinding[] bindings =
            AnimationUtility.GetCurveBindings(clip);

        int rootBindingCount = 0;

        for (int i = 0; i < bindings.Length; i++)
        {
            string property =
                bindings[i].propertyName ?? "";

            string path =
                bindings[i].path ?? "";

            bool rootLike =
                property.IndexOf(
                    "Root",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                property.IndexOf(
                    "Motion",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                (
                    string.IsNullOrEmpty(path) &&
                    (
                        property.IndexOf(
                            "Position",
                            StringComparison.OrdinalIgnoreCase
                        ) >= 0 ||
                        property.IndexOf(
                            "Rotation",
                            StringComparison.OrdinalIgnoreCase
                        ) >= 0
                    )
                );

            if (!rootLike)
                continue;

            rootBindingCount++;

            sb.AppendLine(
                indent +
                "Root Curve #" +
                rootBindingCount +
                ": path='" +
                path +
                "' property='" +
                property +
                "' type=" +
                bindings[i].type.Name
            );
        }

        sb.AppendLine(
            indent +
            "Detected Root/Motion Curve Bindings: " +
            rootBindingCount
        );

        ModelImporter importer =
            AssetImporter.GetAtPath(assetPath)
            as ModelImporter;

        if (importer != null)
        {
            sb.AppendLine(
                indent +
                "Model Import Animation Type: " +
                importer.animationType
            );

            ModelImporterClipAnimation[] clips =
                importer.clipAnimations;

            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            for (int c = 0; c < clips.Length; c++)
            {
                ModelImporterClipAnimation imported =
                    clips[c];

                bool sameClip =
                    string.Equals(
                        imported.name,
                        clip.name,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (!sameClip &&
                    clip.name.IndexOf(
                        imported.name,
                        StringComparison.OrdinalIgnoreCase
                    ) < 0 &&
                    imported.name.IndexOf(
                        clip.name,
                        StringComparison.OrdinalIgnoreCase
                    ) < 0)
                {
                    continue;
                }

                sb.AppendLine(
                    indent +
                    "Importer Clip: " +
                    imported.name
                );

                sb.AppendLine(
                    indent +
                    "  loopTime: " +
                    imported.loopTime
                );

                sb.AppendLine(
                    indent +
                    "  loopPose: " +
                    imported.loopPose
                );

                sb.AppendLine(
                    indent +
                    "  lockRootRotation: " +
                    imported.lockRootRotation
                );

                sb.AppendLine(
                    indent +
                    "  keepOriginalOrientation: " +
                    imported.keepOriginalOrientation
                );

                sb.AppendLine(
                    indent +
                    "  lockRootHeightY: " +
                    imported.lockRootHeightY
                );

                sb.AppendLine(
                    indent +
                    "  keepOriginalPositionY: " +
                    imported.keepOriginalPositionY
                );

                sb.AppendLine(
                    indent +
                    "  heightFromFeet: " +
                    imported.heightFromFeet
                );

                sb.AppendLine(
                    indent +
                    "  lockRootPositionXZ: " +
                    imported.lockRootPositionXZ
                );

                sb.AppendLine(
                    indent +
                    "  keepOriginalPositionXZ: " +
                    imported.keepOriginalPositionXZ
                );

                sb.AppendLine(
                    indent +
                    "  mirror: " +
                    imported.mirror
                );
            }
        }

        return;
    }

    BlendTree tree = motion as BlendTree;

    if (tree != null)
    {
        ChildMotion[] children = tree.children;

        for (int i = 0; i < children.Length; i++)
        {
            sb.AppendLine(
                indent +
                "Blend Child #" +
                (i + 1)
            );

            InspectMotion(
                sb,
                children[i].motion,
                indent + "  "
            );
        }
    }
}

private static string GetHierarchyPath(
    Transform transform
)
{
    if (transform == null)
        return "<null>";

    string path = transform.name;
    Transform current = transform.parent;

    while (current != null)
    {
        path = current.name + "/" + path;
        current = current.parent;
    }

    return path;
}

private static string V(Vector3 value)
{
    return "(" +
           value.x.ToString("0.######") + ", " +
           value.y.ToString("0.######") + ", " +
           value.z.ToString("0.######") + ")";
}

}
#endif