using System.Collections;
using UnityEngine;

public class SoloDuckAgentV2 : MonoBehaviour
{
    private SoloDuckSkyManagerV2 manager;

    private Renderer[] renderers;
    private Animator animator;
    private Animation legacyAnimation;

    private Vector3 originalScale;
    private float originalYaw;

    private Vector3 routeStart;
    private Vector3 routeTarget;

    private float speed;
    private float bobAmount;
    private float bobSpeed;
    private float bobPhase;

    private bool travelling;
    private bool initialized;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        animator = GetComponentInChildren<Animator>(true);
        legacyAnimation = GetComponentInChildren<Animation>(true);

        originalScale = transform.localScale;
        originalYaw = transform.localEulerAngles.y;
    }

    public void Initialize(
        SoloDuckSkyManagerV2 newManager,
        int index,
        int total)
    {
        manager = newManager;
        initialized = true;

        // Los 15 empiezan visibles dentro del cielo,
        // repartidos de izquierda a derecha.
        float t = (index + 0.5f) / total;

        float startX = Mathf.Lerp(
            manager.initialLeftX,
            manager.initialRightX,
            t
        );

        startX += Random.Range(-0.30f, 0.30f);

        float startY = Random.Range(
            manager.minLocalY,
            manager.maxLocalY
        );

        float startZ = Random.Range(
            manager.minLocalZ,
            manager.maxLocalZ
        );

        bool flyRight = Random.value > 0.5f;

        StartRoute(
            new Vector3(startX, startY, startZ),
            flyRight
        );
    }

    void Update()
    {
        if (!initialized || !travelling)
            return;

        Vector3 p = transform.localPosition;

        float nextX = Mathf.MoveTowards(
            p.x,
            routeTarget.x,
            speed * Time.deltaTime
        );

        float totalDistance =
            Mathf.Abs(routeTarget.x - routeStart.x);

        float travelled =
            Mathf.Abs(nextX - routeStart.x);

        float progress =
            totalDistance > 0.001f
            ? Mathf.Clamp01(travelled / totalDistance)
            : 1f;

        p.x = nextX;

        float routeY = Mathf.Lerp(
            routeStart.y,
            routeTarget.y,
            progress
        );

        float bob =
            Mathf.Sin(
                (Time.time * bobSpeed) + bobPhase
            ) * bobAmount;

        p.y = routeY + bob;

        p.z = Mathf.Lerp(
            routeStart.z,
            routeTarget.z,
            progress
        );

        transform.localPosition = p;

        if (progress >= 0.999f)
        {
            travelling = false;
            SetVisible(false);
            StartCoroutine(RespawnLater());
        }
    }

    IEnumerator RespawnLater()
    {
        float wait = Random.Range(
            manager.minRespawnDelay,
            manager.maxRespawnDelay
        );

        yield return new WaitForSeconds(wait);

        bool fromLeft = Random.value > 0.5f;

        float startX =
            fromLeft
            ? manager.leftEdge
            : manager.rightEdge;

        float y = Random.Range(
            manager.minLocalY,
            manager.maxLocalY
        );

        float z = Random.Range(
            manager.minLocalZ,
            manager.maxLocalZ
        );

        StartRoute(
            new Vector3(startX, y, z),
            fromLeft
        );
    }

    void StartRoute(Vector3 start, bool flyRight)
    {
        routeStart = start;

        float destinationX =
            flyRight
            ? manager.rightEdge
            : manager.leftEdge;

        float destinationY = Mathf.Clamp(
            start.y + Random.Range(-0.8f, 0.8f),
            manager.minLocalY,
            manager.maxLocalY
        );

        float destinationZ = Mathf.Clamp(
            start.z + Random.Range(-0.55f, 0.55f),
            manager.minLocalZ,
            manager.maxLocalZ
        );

        routeTarget = new Vector3(
            destinationX,
            destinationY,
            destinationZ
        );

        transform.localPosition = routeStart;

        speed = Random.Range(
            manager.minSpeed,
            manager.maxSpeed
        );

        bobAmount = Random.Range(0.015f, 0.045f);
        bobSpeed = Random.Range(0.55f, 0.95f);
        bobPhase = Random.Range(0f, Mathf.PI * 2f);

        float scaleMultiplier = Random.Range(
            manager.minScaleMultiplier,
            manager.maxScaleMultiplier
        );

        transform.localScale =
            originalScale * scaleMultiplier;

        // La plantilla viene específicamente de
        // la bandada que vuela izquierda -> derecha.
        float yaw =
            flyRight
            ? originalYaw
            : originalYaw + 180f;

        transform.localRotation =
            Quaternion.Euler(
                transform.localEulerAngles.x,
                yaw,
                transform.localEulerAngles.z
            );

        RandomizeWingAnimation();

        SetVisible(true);
        travelling = true;
    }

    void RandomizeWingAnimation()
    {
        float animationSpeed = Random.Range(
            manager.minAnimationSpeed,
            manager.maxAnimationSpeed
        );

        if (animator != null)
        {
            animator.speed = animationSpeed;
            StartCoroutine(RandomizeAnimatorPhase());
        }

        if (legacyAnimation != null)
        {
            foreach (AnimationState state in legacyAnimation)
            {
                state.speed = animationSpeed;
                state.time = Random.Range(0f, state.length);
                break;
            }
        }
    }

    IEnumerator RandomizeAnimatorPhase()
    {
        yield return null;

        if (animator == null ||
            animator.runtimeAnimatorController == null)
            yield break;

        AnimatorStateInfo info =
            animator.GetCurrentAnimatorStateInfo(0);

        if (info.fullPathHash != 0)
        {
            animator.Play(
                info.fullPathHash,
                0,
                Random.Range(0f, 1f)
            );
        }
    }

    void SetVisible(bool visible)
    {
        if (renderers == null)
            return;

        foreach (Renderer r in renderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }
}
