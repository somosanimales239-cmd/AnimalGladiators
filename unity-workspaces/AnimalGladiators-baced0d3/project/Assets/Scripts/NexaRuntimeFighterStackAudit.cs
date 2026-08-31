using System;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class NexaRuntimeFighterStackAudit : MonoBehaviour
{
private PlayerController[] fighters;
private CharacterController[] controllers;
private Animator[] animators;
private SkinnedMeshRenderer[] meshes;
private Vector3[] previousRootPositions;

private StreamWriter writer;
private float nextSampleTime;
private bool wasNear;
private bool wasStackContact;

private const float SampleInterval = 0.05f;

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void CreateAudit()
{
    GameObject existing =
        GameObject.Find("__NexaRuntimeFighterStackAudit");

    if (existing != null)
        return;

    GameObject auditObject =
        new GameObject("__NexaRuntimeFighterStackAudit");

    DontDestroyOnLoad(auditObject);

    auditObject.AddComponent<NexaRuntimeFighterStackAudit>();
}

private void Start()
{
    TryInitialize();
}

private void TryInitialize()
{
    fighters =
        FindObjectsByType<PlayerController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

    if (fighters == null || fighters.Length < 2)
        return;

    controllers =
        new CharacterController[fighters.Length];

    animators =
        new Animator[fighters.Length];

    meshes =
        new SkinnedMeshRenderer[fighters.Length];

    previousRootPositions =
        new Vector3[fighters.Length];

    for (int i = 0; i < fighters.Length; i++)
    {
        controllers[i] =
            fighters[i].GetComponent<CharacterController>();

        animators[i] =
            fighters[i].GetComponent<Animator>();

        if (animators[i] == null)
        {
            animators[i] =
                fighters[i].GetComponentInChildren<Animator>(true);
        }

        meshes[i] =
            fighters[i].GetComponentInChildren<SkinnedMeshRenderer>(true);

        previousRootPositions[i] =
            fighters[i].transform.position;
    }

    OpenLog();

    WriteLine("NEXA RUNTIME FIGHTER STACK AUDIT");
    WriteLine("================================");
    WriteLine("UTC=" + DateTime.UtcNow.ToString("O"));
    WriteLine("Scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
    WriteLine("FighterCount=" + fighters.Length);
    WriteLine("");

    for (int i = 0; i < fighters.Length; i++)
    {
        CharacterController cc = controllers[i];
        Animator animator = animators[i];

        WriteLine(
            "FIGHTER[" + i + "]=" +
            fighters[i].name +
            " root=" + V(fighters[i].transform.position) +
            " scale=" + V(fighters[i].transform.lossyScale) +
            " ccHeight=" + (cc != null ? F(cc.height) : "MISSING") +
            " ccRadius=" + (cc != null ? F(cc.radius) : "MISSING") +
            " ccCenter=" + (cc != null ? V(cc.center) : "MISSING") +
            " applyRootMotion=" +
            (animator != null ? animator.applyRootMotion.ToString() : "MISSING")
        );
    }

    WriteLine("");
    WriteLine(
        "Columns: time,event,fighter,root,rootDelta,ccBottom,ccTop,meshBottom," +
        "grounded,applyRootMotion,jumpTag,attackTag,clip"
    );
    WriteLine("");
}

private void OpenLog()
{
    try
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string outputDir =
            Path.Combine(projectRoot, ".nexa-bridge");

        Directory.CreateDirectory(outputDir);

        string outputPath =
            Path.Combine(
                outputDir,
                "runtime-fighter-stack-audit.txt"
            );

        writer =
            new StreamWriter(
                outputPath,
                false,
                new UTF8Encoding(false)
            );

        writer.AutoFlush = true;
    }
    catch (Exception ex)
    {
        Debug.LogError(
            "[NexaRuntimeFighterStackAudit] Cannot open log: " +
            ex
        );
    }
}

private void LateUpdate()
{
    if (fighters == null || fighters.Length < 2)
    {
        TryInitialize();
        return;
    }

    if (controllers == null ||
        controllers.Length < 2 ||
        controllers[0] == null ||
        controllers[1] == null)
    {
        return;
    }

    CharacterController a = controllers[0];
    CharacterController b = controllers[1];

    float dx =
        Mathf.Abs(
            fighters[0].transform.position.x -
            fighters[1].transform.position.x
        );

    float radiusSum =
        a.bounds.extents.x +
        b.bounds.extents.x;

    bool near =
        dx <= radiusSum + 0.6f;

    float aBottom =
        a.bounds.center.y -
        a.bounds.extents.y;

    float aTop =
        a.bounds.center.y +
        a.bounds.extents.y;

    float bBottom =
        b.bounds.center.y -
        b.bounds.extents.y;

    float bTop =
        b.bounds.center.y +
        b.bounds.extents.y;

    bool aOnTopOfB =
        Mathf.Abs(aBottom - bTop) <= 0.35f &&
        dx <= radiusSum + 0.15f;

    bool bOnTopOfA =
        Mathf.Abs(bBottom - aTop) <= 0.35f &&
        dx <= radiusSum + 0.15f;

    bool stackContact =
        aOnTopOfB || bOnTopOfA;

    bool anyJump =
        IsJumpState(animators[0]) ||
        IsJumpState(animators[1]);

    if (near != wasNear)
    {
        WritePairEvent(
            near ? "ENTER_NEAR" : "EXIT_NEAR",
            dx,
            radiusSum,
            aBottom,
            aTop,
            bBottom,
            bTop
        );

        wasNear = near;
    }

    if (stackContact != wasStackContact)
    {
        WritePairEvent(
            stackContact
                ? (aOnTopOfB
                    ? "STACK_CONTACT_A_ON_B"
                    : "STACK_CONTACT_B_ON_A")
                : "STACK_CONTACT_END",
            dx,
            radiusSum,
            aBottom,
            aTop,
            bBottom,
            bTop
        );

        wasStackContact = stackContact;
    }

    if (
        Time.unscaledTime >= nextSampleTime &&
        (near || anyJump || stackContact)
    )
    {
        nextSampleTime =
            Time.unscaledTime + SampleInterval;

        for (int i = 0; i < fighters.Length; i++)
        {
            WriteFighterSample(i, "SAMPLE");
        }

        WriteLine(
            F(Time.unscaledTime) +
            ",PAIR," +
            "dx=" + F(dx) +
            ",radiusSum=" + F(radiusSum) +
            ",boundsIntersect=" +
            a.bounds.Intersects(b.bounds) +
            ",A_bottom=" + F(aBottom) +
            ",A_top=" + F(aTop) +
            ",B_bottom=" + F(bBottom) +
            ",B_top=" + F(bTop)
        );
    }

    for (int i = 0; i < fighters.Length; i++)
    {
        previousRootPositions[i] =
            fighters[i].transform.position;
    }
}

private void WriteFighterSample(
    int index,
    string eventName
)
{
    if (index < 0 ||
        index >= fighters.Length ||
        fighters[index] == null)
    {
        return;
    }

    Transform root =
        fighters[index].transform;

    CharacterController cc =
        controllers[index];

    Animator animator =
        animators[index];

    SkinnedMeshRenderer mesh =
        meshes[index];

    Vector3 rootDelta =
        root.position -
        previousRootPositions[index];

    float ccBottom = float.NaN;
    float ccTop = float.NaN;

    if (cc != null)
    {
        ccBottom =
            cc.bounds.center.y -
            cc.bounds.extents.y;

        ccTop =
            cc.bounds.center.y +
            cc.bounds.extents.y;
    }

    float meshBottom = float.NaN;

    if (mesh != null)
    {
        meshBottom =
            mesh.bounds.center.y -
            mesh.bounds.extents.y;
    }

    bool jumpTag =
        IsJumpState(animator);

    bool attackTag =
        IsTagged(animator, "Attack");

    string clipName =
        GetCurrentClipName(animator);

    WriteLine(
        F(Time.unscaledTime) + "," +
        eventName + "," +
        fighters[index].name + "," +
        "root=" + V(root.position) + "," +
        "rootDelta=" + V(rootDelta) + "," +
        "ccBottom=" + F(ccBottom) + "," +
        "ccTop=" + F(ccTop) + "," +
        "meshBottom=" + F(meshBottom) + "," +
        "grounded=" +
        (cc != null ? cc.isGrounded.ToString() : "MISSING") + "," +
        "applyRootMotion=" +
        (animator != null
            ? animator.applyRootMotion.ToString()
            : "MISSING") + "," +
        "jumpTag=" + jumpTag + "," +
        "attackTag=" + attackTag + "," +
        "clip=" + clipName
    );
}

private void WritePairEvent(
    string eventName,
    float dx,
    float radiusSum,
    float aBottom,
    float aTop,
    float bBottom,
    float bTop
)
{
    WriteLine(
        F(Time.unscaledTime) + "," +
        eventName + "," +
        "dx=" + F(dx) + "," +
        "radiusSum=" + F(radiusSum) + "," +
        "A_bottom=" + F(aBottom) + "," +
        "A_top=" + F(aTop) + "," +
        "B_bottom=" + F(bBottom) + "," +
        "B_top=" + F(bTop)
    );

    for (int i = 0; i < fighters.Length; i++)
    {
        WriteFighterSample(i, eventName);
    }
}

private static bool IsJumpState(
    Animator animator
)
{
    return IsTagged(animator, "Jump");
}

private static bool IsTagged(
    Animator animator,
    string tag
)
{
    if (animator == null ||
        !animator.isActiveAndEnabled)
    {
        return false;
    }

    AnimatorStateInfo current =
        animator.GetCurrentAnimatorStateInfo(0);

    if (current.IsTag(tag))
        return true;

    if (animator.IsInTransition(0))
    {
        AnimatorStateInfo next =
            animator.GetNextAnimatorStateInfo(0);

        if (next.IsTag(tag))
            return true;
    }

    return false;
}

private static string GetCurrentClipName(
    Animator animator
)
{
    if (animator == null ||
        !animator.isActiveAndEnabled)
    {
        return "<none>";
    }

    AnimatorClipInfo[] clips =
        animator.GetCurrentAnimatorClipInfo(0);

    if (clips == null || clips.Length == 0)
        return "<none>";

    AnimationClip clip =
        clips[0].clip;

    return clip != null
        ? clip.name
        : "<none>";
}

private void WriteLine(string text)
{
    if (writer == null)
        return;

    writer.WriteLine(text);
}

private void OnApplicationQuit()
{
    CloseWriter();
}

private void OnDestroy()
{
    CloseWriter();
}

private void CloseWriter()
{
    if (writer == null)
        return;

    try
    {
        writer.Flush();
        writer.Dispose();
    }
    catch
    {
    }

    writer = null;
}

private static string V(Vector3 value)
{
    return "(" +
           F(value.x) + "|" +
           F(value.y) + "|" +
           F(value.z) + ")";
}

private static string F(float value)
{
    if (float.IsNaN(value))
        return "NaN";

    return value.ToString("0.######");
}

}