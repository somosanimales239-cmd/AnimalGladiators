using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FighterCameraController : MonoBehaviour
{
    [Header("Fighters")]
    public Transform fighter1;
    public Transform fighter2;

    [Header("Framing")]
    public float minDistance = 7.5f;
    public float maxDistance = 13.5f;
    public float smoothTime = 0.18f;
    public float targetHeight = 0.9f;
    public float minSeparation = 4f;
    public float maxSeparation = 13f;
    public float fieldOfView = 25.9f;

    [Header("Fixed View")]
    public Vector3 fixedRotation = new Vector3(10f, -20f, 0f);

    private Vector3 movementVelocity;
    private Camera controlledCamera;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        ApplyCameraSettings();
    }

    private void LateUpdate()
    {
        if (fighter1 == null || fighter2 == null)
        {
            return;
        }

        Vector3 midpoint = (fighter1.position + fighter2.position) * 0.5f;
        Vector3 target = midpoint + Vector3.up * targetHeight;
        float separation = Mathf.Abs(fighter1.position.x - fighter2.position.x);
        float distanceRatio = Mathf.InverseLerp(
            minSeparation,
            maxSeparation,
            separation
        );
        float cameraDistance = Mathf.Lerp(
            minDistance,
            maxDistance,
            distanceRatio
        );

        Quaternion viewRotation = Quaternion.Euler(fixedRotation);
        Vector3 desiredPosition =
            target - viewRotation * Vector3.forward * cameraDistance;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref movementVelocity,
            smoothTime
        );
        transform.rotation = viewRotation;

        ApplyCameraSettings();
    }

    private void ApplyCameraSettings()
    {
        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
        }

        if (controlledCamera != null)
        {
            controlledCamera.fieldOfView = fieldOfView;
        }
    }
}
