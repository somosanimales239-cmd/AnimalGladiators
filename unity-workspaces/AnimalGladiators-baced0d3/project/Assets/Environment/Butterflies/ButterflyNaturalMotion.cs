using System.Collections;
using UnityEngine;

public class ButterflyNaturalMotion : MonoBehaviour
{
    public ButterflyHabitat habitat;
    public ButterflyWingFlap wingFlap;

    [Header("Slow Body Movement")]
    public float minMoveSpeed = 0.32f;
    public float maxMoveSpeed = 0.62f;

    [Header("Natural Motion")]
    public float turnSpeed = 3.5f;
    public float sidewaysWobble = 0.07f;
    public float verticalWobble = 0.05f;
    public float wobbleSpeed = 2.0f;

    [Header("Behaviour")]
    public float minHoverTime = 0.5f;
    public float maxHoverTime = 2.2f;

    public float minPerchTime = 3.0f;
    public float maxPerchTime = 8.0f;

    public int minFlightsBeforePerch = 2;
    public int maxFlightsBeforePerch = 5;

    [Header("Model Orientation")]
    public float modelYawOffset = 0f;

    private float moveSpeed;
    private float phase;

    void Start()
    {
        phase = Random.Range(0f, 20f);
        moveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);

        if (wingFlap == null)
            wingFlap = GetComponent<ButterflyWingFlap>();

        StartCoroutine(BehaviourLoop());
    }

    IEnumerator BehaviourLoop()
    {
        yield return new WaitForSeconds(
            Random.Range(0f, 2.5f)
        );

        while (true)
        {
            int flights = Random.Range(
                minFlightsBeforePerch,
                maxFlightsBeforePerch + 1
            );

            for (int i = 0; i < flights; i++)
            {
                Transform air = habitat != null
                    ? habitat.GetRandomAirPoint()
                    : null;

                if (air != null)
                {
                    if (wingFlap != null)
                        wingFlap.SetFlying(true);

                    moveSpeed = Random.Range(
                        minMoveSpeed,
                        maxMoveSpeed
                    );

                    yield return MoveTo(
                        air.position,
                        true
                    );

                    yield return new WaitForSeconds(
                        Random.Range(
                            minHoverTime,
                            maxHoverTime
                        )
                    );
                }
            }

            Transform perch = habitat != null
                ? habitat.GetRandomPerchPoint()
                : null;

            if (perch != null)
            {
                if (wingFlap != null)
                    wingFlap.SetFlying(true);

                yield return MoveTo(
                    perch.position,
                    false
                );

                transform.position = perch.position;

                if (wingFlap != null)
                    wingFlap.SetFlying(false);

                yield return new WaitForSeconds(
                    Random.Range(
                        minPerchTime,
                        maxPerchTime
                    )
                );

                if (wingFlap != null)
                    wingFlap.SetFlying(true);

                yield return new WaitForSeconds(
                    Random.Range(0.08f, 0.25f)
                );
            }
        }
    }

    IEnumerator MoveTo(
        Vector3 destination,
        bool allowFullWobble)
    {
        while (
            Vector3.Distance(
                transform.position,
                destination
            ) > 0.06f
        )
        {
            Vector3 toTarget =
                destination - transform.position;

            Vector3 direction =
                toTarget.normalized;

            Vector3 side =
                Vector3.Cross(
                    Vector3.up,
                    direction
                ).normalized;

            float wobbleAmount =
                allowFullWobble ? 1f : 0.25f;

            float sideWave =
                Mathf.Sin(
                    (Time.time + phase) * wobbleSpeed
                ) * sidewaysWobble * wobbleAmount;

            float verticalWave =
                Mathf.Sin(
                    ((Time.time + phase) * wobbleSpeed * 1.37f)
                ) * verticalWobble * wobbleAmount;

            Vector3 desired =
                destination +
                side * sideWave +
                Vector3.up * verticalWave;

            transform.position =
                Vector3.MoveTowards(
                    transform.position,
                    desired,
                    moveSpeed * Time.deltaTime
                );

            Vector3 lookDirection =
                desired - transform.position;

            lookDirection.y *= 0.35f;

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(
                        lookDirection.normalized
                    ) *
                    Quaternion.Euler(
                        0f,
                        modelYawOffset,
                        0f
                    );

                transform.rotation =
                    Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        turnSpeed * Time.deltaTime
                    );
            }

            yield return null;
        }
    }
}
