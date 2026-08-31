using System.Collections;
using UnityEngine;

public sealed class BirdLoopMover : MonoBehaviour
{
    public Vector3 pointA;
    public Vector3 pointB;

    public float moveSpeed = 1.0f;
    public float minWait = 4f;
    public float maxWait = 9f;

    public bool randomizeStart = true;
    public float bobAmplitude = 0.08f;
    public float bobFrequency = 1.2f;

    private Vector3 target;
    private Vector3 basePosition;
    private float randomOffset;

    private void Start()
    {
        if (randomizeStart)
        {
            float t = Random.Range(0f, 1f);
            basePosition = Vector3.Lerp(pointA, pointB, t);
            transform.position = basePosition;
            target = Random.value > 0.5f ? pointA : pointB;
            randomOffset = Random.Range(0f, 10f);
        }
        else
        {
            basePosition = pointA;
            transform.position = pointA;
            target = pointB;
            randomOffset = 0f;
        }

        StartCoroutine(MoveLoop());
    }

    private IEnumerator MoveLoop()
    {
        while (true)
        {
            while (Vector3.Distance(basePosition, target) > 0.05f)
            {
                basePosition = Vector3.MoveTowards(basePosition, target, moveSpeed * Time.deltaTime);

                Vector3 next = basePosition;
                next.y += Mathf.Sin((Time.time + randomOffset) * bobFrequency) * bobAmplitude;
                transform.position = next;

                Vector3 direction = target - basePosition;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion facing = Quaternion.FromToRotation(Vector3.right, direction.normalized);
                    transform.rotation = Quaternion.Lerp(transform.rotation, facing, 4f * Time.deltaTime);
                }

                yield return null;
            }

            transform.position = target;
            basePosition = target;
            float waitTime = Random.Range(minWait, maxWait);
            yield return new WaitForSeconds(waitTime);
            target = target == pointA ? pointB : pointA;
        }
    }
}
