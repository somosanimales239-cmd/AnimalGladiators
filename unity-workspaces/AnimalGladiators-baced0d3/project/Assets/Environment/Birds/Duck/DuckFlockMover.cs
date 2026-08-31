using UnityEngine;

public sealed class DuckFlockMover : MonoBehaviour
{
    public float moveX = 2.5f;
    public float moveZ = 1.2f;
    public float moveY = 0f;
    public float bobAmplitude = 0.12f;
    public float bobFrequency = 1.4f;

    private float baseY;

    private void Start()
    {
        baseY = transform.position.y;
    }

    private void Update()
    {
        baseY += moveY * Time.deltaTime;

        Vector3 position = transform.position;
        position.x += moveX * Time.deltaTime;
        position.z += moveZ * Time.deltaTime;
        position.y = baseY + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.position = position;

        if (transform.position.x > 30f)
            Destroy(gameObject);
    }
}
