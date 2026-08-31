using System.Collections;
using UnityEngine;

public class DuckSoloRandomFlight : MonoBehaviour
{
    public Vector3 destination;
    public float speed = 0.4f;

    public float minScale = 0.06f;
    public float maxScale = 0.13f;

    public float minAnimationSpeed = 0.72f;
    public float maxAnimationSpeed = 1.28f;

    public float bobAmount = 0.035f;
    public float bobSpeed = 0.7f;

    public float respawnDelayMin = 5f;
    public float respawnDelayMax = 22f;

    public float leftX = -13f;
    public float rightX = 14f;

    public float minY = 8.8f;
    public float maxY = 11.7f;

    public float minZ = 18.0f;
    public float maxZ = 22.0f;

    private Animator animator;
    private Renderer[] renderers;
    private float baseY;
    private float bobOffset;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    void Start()
    {
        RandomizeAnimation();
        StartCoroutine(InitialStart());
    }

    IEnumerator InitialStart()
    {
        SetVisible(false);

        yield return new WaitForSeconds(Random.Range(0f, 18f));

        CreateRandomRoute();
        SetVisible(true);
    }

    void Update()
    {
        if (!IsVisible())
            return;

        Vector3 current = transform.position;

        Vector3 next = Vector3.MoveTowards(
            current,
            destination,
            speed * Time.deltaTime
        );

        float bob =
            Mathf.Sin((Time.time + bobOffset) * bobSpeed)
            * bobAmount;

        next.y += bob * Time.deltaTime;

        transform.position = next;

        Vector3 direction = destination - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction.normalized);

            Vector3 euler = targetRotation.eulerAngles;
            euler.x = 0f;
            euler.z = 0f;

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.Euler(euler),
                3f * Time.deltaTime
            );
        }

        if (Vector3.Distance(transform.position, destination) < 0.25f)
        {
            StartCoroutine(RespawnLater());
        }
    }

    IEnumerator RespawnLater()
    {
        SetVisible(false);

        yield return new WaitForSeconds(
            Random.Range(respawnDelayMin, respawnDelayMax)
        );

        CreateRandomRoute();
        RandomizeAnimation();
        SetVisible(true);
    }

    void CreateRandomRoute()
    {
        bool fromLeft = Random.value > 0.5f;

        float startY = Random.Range(minY, maxY);
        float endY = Mathf.Clamp(
            startY + Random.Range(-1.0f, 1.0f),
            minY,
            maxY
        );

        float startZ = Random.Range(minZ, maxZ);

        float endZ = Mathf.Clamp(
            startZ + Random.Range(-1.5f, 1.8f),
            minZ,
            maxZ
        );

        if (fromLeft)
        {
            transform.position =
                new Vector3(leftX, startY, startZ);

            destination =
                new Vector3(rightX, endY, endZ);
        }
        else
        {
            transform.position =
                new Vector3(rightX, startY, startZ);

            destination =
                new Vector3(leftX, endY, endZ);
        }

        float scale =
            Random.Range(minScale, maxScale);

        transform.localScale =
            Vector3.one * scale;

        speed =
            Random.Range(0.22f, 0.58f);

        bobAmount =
            Random.Range(0.015f, 0.055f);

        bobSpeed =
            Random.Range(0.55f, 1.0f);

        bobOffset =
            Random.Range(0f, 20f);

        baseY = transform.position.y;
    }

    void RandomizeAnimation()
    {
        if (animator == null)
            return;

        animator.speed =
            Random.Range(
                minAnimationSpeed,
                maxAnimationSpeed
            );

        animator.Play(
            0,
            0,
            Random.Range(0f, 1f)
        );
    }

    void SetVisible(bool value)
    {
        if (renderers == null)
            return;

        foreach (Renderer r in renderers)
        {
            if (r != null)
                r.enabled = value;
        }
    }

    bool IsVisible()
    {
        if (renderers == null || renderers.Length == 0)
            return true;

        return renderers[0].enabled;
    }
}
