using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform followPivot;
    public Vector3 offset = new Vector3(8, 10, -10);
    public float smoothSpeed = 0.125f;

    void LateUpdate()
    {
        if (followPivot == null) return;

        Vector3 desiredPosition = followPivot.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        transform.LookAt(followPivot);
    }
}
