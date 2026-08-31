using UnityEngine;

public sealed class DuckFlightPath : MonoBehaviour
{
    public float speed = 2.5f;
    public float flightDistance = 30f;
    public float verticalBob = 0.15f;
    public float bobSpeed = 1.5f;

    private float baseY;
    private float lockedZ;
    private float bobPhase;

    private void Awake()
    {
        baseY = transform.position.y;
        lockedZ = transform.position.z;
        bobPhase = transform.position.x * 0.31f;
    }

    private void Update()
    {
        Vector3 nextPosition = transform.position;
        nextPosition.x += speed * Time.deltaTime;

        float halfDistance = Mathf.Max(0.01f, flightDistance * 0.5f);
        if (nextPosition.x > halfDistance)
            nextPosition.x = -halfDistance;
        else if (nextPosition.x < -halfDistance)
            nextPosition.x = halfDistance;

        nextPosition.y = baseY + Mathf.Sin(Time.time * bobSpeed + bobPhase) * verticalBob;
        nextPosition.z = lockedZ;
        transform.position = nextPosition;
    }
}
