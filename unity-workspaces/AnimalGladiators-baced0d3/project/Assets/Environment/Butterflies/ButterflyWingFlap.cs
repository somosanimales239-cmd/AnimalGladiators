using UnityEngine;

public class ButterflyWingFlap : MonoBehaviour
{
    public Transform leftWing;
    public Transform rightWing;

    [Header("Wing Axes")]
    public Vector3 leftAxis = Vector3.forward;
    public Vector3 rightAxis = Vector3.forward;

    public float leftSign = 1f;
    public float rightSign = -1f;

    [Header("Fast Flapping")]
    public float flapAngle = 55f;
    public float flapFrequency = 9f;

    [Header("Randomization")]
    public bool randomizePhase = true;

    private Quaternion leftBase;
    private Quaternion rightBase;

    private float phase;
    private float currentBlend = 1f;
    private float targetBlend = 1f;

    void Awake()
    {
        if (leftWing != null)
            leftBase = leftWing.localRotation;

        if (rightWing != null)
            rightBase = rightWing.localRotation;

        phase = randomizePhase
            ? Random.Range(0f, Mathf.PI * 2f)
            : 0f;
    }

    void Update()
    {
        currentBlend = Mathf.MoveTowards(
            currentBlend,
            targetBlend,
            Time.deltaTime * 8f
        );

        float wave = Mathf.Sin(
            (Time.time * flapFrequency * Mathf.PI * 2f) + phase
        );

        float angle = wave * flapAngle * currentBlend;

        if (leftWing != null)
        {
            leftWing.localRotation =
                leftBase *
                Quaternion.AngleAxis(
                    angle * leftSign,
                    leftAxis.normalized
                );
        }

        if (rightWing != null)
        {
            rightWing.localRotation =
                rightBase *
                Quaternion.AngleAxis(
                    angle * rightSign,
                    rightAxis.normalized
                );
        }
    }

    public void SetFlying(bool flying)
    {
        targetBlend = flying ? 1f : 0.035f;
    }
}
