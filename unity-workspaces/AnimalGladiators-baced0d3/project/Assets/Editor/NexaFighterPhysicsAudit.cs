#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NexaFighterPhysicsAudit
{
private const string SessionKey = "NEXA_FIGHTER_PHYSICS_AUDIT_V1";

static NexaFighterPhysicsAudit()
{
    RunOnce();
}

private static void RunOnce()
{
    if (EditorApplication.isPlayingOrWillChangePlaymode)
        return;

    if (SessionState.GetBool(SessionKey, false))
        return;

    SessionState.SetBool(SessionKey, true);

    try
    {
        RunAudit();
    }
    catch (Exception ex)
    {
        Debug.LogError("[NexaFighterPhysicsAudit] " + ex);
    }
}

private static void RunAudit()
{
    string projectRoot = Directory.GetParent(Application.dataPath).FullName;
    string outputDir = Path.Combine(projectRoot, ".nexa-bridge");
    string outputPath = Path.Combine(outputDir, "fighter-physics-audit.txt");

    Directory.CreateDirectory(outputDir);

    var sb = new StringBuilder(32768);

    Scene scene = SceneManager.GetActiveScene();

    sb.AppendLine("NEXA FIGHTER PHYSICS AUDIT");
    sb.AppendLine("==========================");
    sb.AppendLine("Scene: " + scene.path);
    sb.AppendLine("Scene name: " + scene.name);
    sb.AppendLine("Generated UTC: " + DateTime.UtcNow.ToString("O"));
    sb.AppendLine("Play Mode: " + EditorApplication.isPlaying);
    sb.AppendLine();

    PlayerController[] fighters =
        UnityEngine.Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

    sb.AppendLine("PlayerController count: " + fighters.Length);
    sb.AppendLine();

    for (int i = 0; i < fighters.Length; i++)
    {
        PlayerController fighter = fighters[i];
        GameObject go = fighter.gameObject;
        Transform t = fighter.transform;

        sb.AppendLine("============================================================");
        sb.AppendLine("FIGHTER #" + (i + 1));
        sb.AppendLine("============================================================");
        sb.AppendLine("Name: " + go.name);
        sb.AppendLine("Hierarchy: " + GetHierarchyPath(t));
        sb.AppendLine("ActiveSelf: " + go.activeSelf);
        sb.AppendLine("ActiveInHierarchy: " + go.activeInHierarchy);
        sb.AppendLine("Layer: " + go.layer);
        sb.AppendLine("Tag: " + go.tag);
        sb.AppendLine();

        sb.AppendLine("[ROOT TRANSFORM]");
        AppendVector(sb, "World Position", t.position);
        AppendQuaternion(sb, "World Rotation", t.rotation);
        AppendVector(sb, "Local Position", t.localPosition);
        AppendVector(sb, "Local Scale", t.localScale);
        AppendVector(sb, "Lossy Scale", t.lossyScale);
        sb.AppendLine();

        CharacterController cc = go.GetComponent<CharacterController>();

        sb.AppendLine("[CHARACTER CONTROLLER]");
        if (cc == null)
        {
            sb.AppendLine("MISSING");
        }
        else
        {
            sb.AppendLine("Enabled: " + cc.enabled);
            sb.AppendLine("Height: " + F(cc.height));
            sb.AppendLine("Radius: " + F(cc.radius));
            AppendVector(sb, "Center", cc.center);
            sb.AppendLine("Step Offset: " + F(cc.stepOffset));
            sb.AppendLine("Skin Width: " + F(cc.skinWidth));
            sb.AppendLine("Min Move Distance: " + F(cc.minMoveDistance));
            sb.AppendLine("Slope Limit: " + F(cc.slopeLimit));
            sb.AppendLine("Is Grounded: " + cc.isGrounded);
            AppendVector(sb, "Bounds Center", cc.bounds.center);
            AppendVector(sb, "Bounds Size", cc.bounds.size);
            AppendVector(sb, "Bounds Extents", cc.bounds.extents);

            Vector3 bottom =
                cc.bounds.center -
                Vector3.up * cc.bounds.extents.y;

            Vector3 top =
                cc.bounds.center +
                Vector3.up * cc.bounds.extents.y;

            AppendVector(sb, "Bounds Bottom", bottom);
            AppendVector(sb, "Bounds Top", top);

            sb.AppendLine(
                "Root Y minus CC Bottom Y: " +
                F(t.position.y - bottom.y)
            );
        }

        sb.AppendLine();

        sb.AppendLine("[PLAYER CONTROLLER]");
        sb.AppendLine("acceptPlayerInput: " + fighter.acceptPlayerInput);
        sb.AppendLine("autoFaceOpponent: " + fighter.autoFaceOpponent);
        sb.AppendLine("startFacingRight: " + fighter.startFacingRight);
        sb.AppendLine(
            "landingSeparationPadding: " +
            F(fighter.landingSeparationPadding)
        );
        sb.AppendLine(
            "landingResolveSpeed: " +
            F(fighter.landingResolveSpeed)
        );
        sb.AppendLine(
            "Opponent: " +
            (fighter.opponent != null
                ? GetHierarchyPath(fighter.opponent)
                : "<null>")
        );
        sb.AppendLine();

        Animator animator = go.GetComponent<Animator>();
        if (animator == null)
            animator = go.GetComponentInChildren<Animator>(true);

        sb.AppendLine("[ANIMATOR]");
        if (animator == null)
        {
            sb.AppendLine("MISSING");
        }
        else
        {
            sb.AppendLine("Hierarchy: " + GetHierarchyPath(animator.transform));
            sb.AppendLine("Enabled: " + animator.enabled);
            sb.AppendLine("Apply Root Motion: " + animator.applyRootMotion);
            sb.AppendLine("Update Mode: " + animator.updateMode);
            sb.AppendLine("Culling Mode: " + animator.cullingMode);
            AppendVector(sb, "Animator World Position", animator.transform.position);
            AppendVector(sb, "Animator Local Position", animator.transform.localPosition);
            AppendVector(sb, "Animator Lossy Scale", animator.transform.lossyScale);

            if (animator.runtimeAnimatorController != null)
            {
                string controllerPath =
                    AssetDatabase.GetAssetPath(
                        animator.runtimeAnimatorController
                    );

                sb.AppendLine(
                    "Runtime Controller: " +
                    animator.runtimeAnimatorController.name
                );
                sb.AppendLine("Controller Asset: " + controllerPath);
            }
            else
            {
                sb.AppendLine("Runtime Controller: <null>");
            }

            if (animator.isHuman)
            {
                Transform hips =
                    animator.GetBoneTransform(HumanBodyBones.Hips);

                if (hips != null)
                {
                    sb.AppendLine("Hips: " + GetHierarchyPath(hips));
                    AppendVector(sb, "Hips World Position", hips.position);
                    AppendVector(sb, "Hips Local Position", hips.localPosition);
                    AppendVector(
                        sb,
                        "Hips Offset From Fighter Root",
                        hips.position - t.position
                    );
                }
            }
        }

        sb.AppendLine();

        sb.AppendLine("[COLLIDERS ON FIGHTER AND CHILDREN]");
        Collider[] colliders =
            go.GetComponentsInChildren<Collider>(true);

        sb.AppendLine("Collider count: " + colliders.Length);

        for (int c = 0; c < colliders.Length; c++)
        {
            Collider col = colliders[c];

            sb.AppendLine(
                "Collider #" + (c + 1) +
                ": " + col.GetType().Name
            );
            sb.AppendLine(
                "  Hierarchy: " +
                GetHierarchyPath(col.transform)
            );
            sb.AppendLine("  Enabled: " + col.enabled);
            sb.AppendLine("  Is Trigger: " + col.isTrigger);
            AppendVector(sb, "  Bounds Center", col.bounds.center);
            AppendVector(sb, "  Bounds Size", col.bounds.size);
            AppendVector(sb, "  Bounds Extents", col.bounds.extents);
        }

        sb.AppendLine();

        sb.AppendLine("[RIGIDBODIES]");
        Rigidbody[] bodies =
            go.GetComponentsInChildren<Rigidbody>(true);

        sb.AppendLine("Rigidbody count: " + bodies.Length);

        for (int r = 0; r < bodies.Length; r++)
        {
            Rigidbody rb = bodies[r];

            sb.AppendLine(
                "Rigidbody #" + (r + 1) +
                ": " + GetHierarchyPath(rb.transform)
            );
            sb.AppendLine("  Is Kinematic: " + rb.isKinematic);
            sb.AppendLine("  Use Gravity: " + rb.useGravity);
            sb.AppendLine(
                "  Collision Detection: " +
                rb.collisionDetectionMode
            );
        }

        sb.AppendLine();

        sb.AppendLine("[SKINNED MESH RENDERERS]");
        SkinnedMeshRenderer[] skins =
            go.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        sb.AppendLine("SkinnedMeshRenderer count: " + skins.Length);

        bool haveCombined = false;
        Bounds combined = new Bounds();

        for (int s = 0; s < skins.Length; s++)
        {
            SkinnedMeshRenderer skin = skins[s];

            sb.AppendLine(
                "Mesh #" + (s + 1) +
                ": " + GetHierarchyPath(skin.transform)
            );
            sb.AppendLine(
                "  Mesh Asset: " +
                (skin.sharedMesh != null
                    ? AssetDatabase.GetAssetPath(skin.sharedMesh)
                    : "<null>")
            );
            sb.AppendLine(
                "  Root Bone: " +
                (skin.rootBone != null
                    ? GetHierarchyPath(skin.rootBone)
                    : "<null>")
            );
            AppendVector(sb, "  Bounds Center", skin.bounds.center);
            AppendVector(sb, "  Bounds Size", skin.bounds.size);

            if (!haveCombined)
            {
                combined = skin.bounds;
                haveCombined = true;
            }
            else
            {
                combined.Encapsulate(skin.bounds);
            }
        }

        if (haveCombined)
        {
            sb.AppendLine();
            sb.AppendLine("[COMBINED VISUAL MESH BOUNDS]");
            AppendVector(sb, "Center", combined.center);
            AppendVector(sb, "Size", combined.size);
            AppendVector(sb, "Extents", combined.extents);

            Vector3 meshBottom =
                combined.center -
                Vector3.up * combined.extents.y;

            AppendVector(sb, "Bottom", meshBottom);

            sb.AppendLine(
                "Root Y minus Mesh Bottom Y: " +
                F(t.position.y - meshBottom.y)
            );

            if (cc != null)
            {
                sb.AppendLine(
                    "Mesh Center minus CC Center: " +
                    V(combined.center - cc.bounds.center)
                );

                sb.AppendLine(
                    "Mesh Bottom Y minus CC Bottom Y: " +
                    F(
                        meshBottom.y -
                        (
                            cc.bounds.center.y -
                            cc.bounds.extents.y
                        )
                    )
                );
            }
        }

        sb.AppendLine();

        sb.AppendLine("[MESH / ROOT CHILD TRANSFORMS]");
        for (int childIndex = 0; childIndex < t.childCount; childIndex++)
        {
            Transform child = t.GetChild(childIndex);

            sb.AppendLine(
                "Child #" + (childIndex + 1) +
                ": " + child.name
            );
            AppendVector(sb, "  Local Position", child.localPosition);
            AppendVector(sb, "  Local Scale", child.localScale);
            AppendVector(sb, "  World Position", child.position);
        }

        sb.AppendLine();
    }

    sb.AppendLine("============================================================");
    sb.AppendLine("PAIR COMPARISON");
    sb.AppendLine("============================================================");

    if (fighters.Length >= 2)
    {
        for (int a = 0; a < fighters.Length; a++)
        {
            for (int b = a + 1; b < fighters.Length; b++)
            {
                PlayerController fa = fighters[a];
                PlayerController fb = fighters[b];

                CharacterController cca =
                    fa.GetComponent<CharacterController>();

                CharacterController ccb =
                    fb.GetComponent<CharacterController>();

                sb.AppendLine(
                    fa.name + " <-> " + fb.name
                );

                float dx =
                    Mathf.Abs(
                        fa.transform.position.x -
                        fb.transform.position.x
                    );

                float dy =
                    Mathf.Abs(
                        fa.transform.position.y -
                        fb.transform.position.y
                    );

                sb.AppendLine("Root |Delta X|: " + F(dx));
                sb.AppendLine("Root |Delta Y|: " + F(dy));

                if (cca != null && ccb != null)
                {
                    float radiusSum =
                        cca.radius + ccb.radius;

                    float worldExtentsSumX =
                        cca.bounds.extents.x +
                        ccb.bounds.extents.x;

                    sb.AppendLine(
                        "Radius Sum: " +
                        F(radiusSum)
                    );

                    sb.AppendLine(
                        "World Bounds Extents X Sum: " +
                        F(worldExtentsSumX)
                    );

                    sb.AppendLine(
                        "Current CC Bounds Intersect: " +
                        cca.bounds.Intersects(ccb.bounds)
                    );
                }

                sb.AppendLine();
            }
        }
    }
    else
    {
        sb.AppendLine(
            "Need at least two PlayerController objects for comparison."
        );
    }

    File.WriteAllText(
        outputPath,
        sb.ToString(),
        new UTF8Encoding(false)
    );

    Debug.Log(
        "[NexaFighterPhysicsAudit] Audit written: " +
        outputPath
    );
}

private static string GetHierarchyPath(Transform t)
{
    if (t == null)
        return "<null>";

    string path = t.name;
    Transform current = t.parent;

    while (current != null)
    {
        path = current.name + "/" + path;
        current = current.parent;
    }

    return path;
}

private static void AppendVector(
    StringBuilder sb,
    string label,
    Vector3 value
)
{
    sb.AppendLine(label + ": " + V(value));
}

private static void AppendQuaternion(
    StringBuilder sb,
    string label,
    Quaternion value
)
{
    sb.AppendLine(
        label + ": (" +
        F(value.x) + ", " +
        F(value.y) + ", " +
        F(value.z) + ", " +
        F(value.w) + ")"
    );
}

private static string V(Vector3 v)
{
    return "(" +
           F(v.x) + ", " +
           F(v.y) + ", " +
           F(v.z) + ")";
}

private static string F(float value)
{
    return value.ToString("0.######");
}

}
#endif