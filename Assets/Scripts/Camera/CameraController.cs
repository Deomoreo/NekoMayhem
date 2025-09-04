using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform followPivot;
    public Vector3 offset = new Vector3(8, 10, -10);
    public float smoothSpeed = 0.125f;

    public float bipedeSize = 5f;
    public float quadrupedeSize = 7f;
    public float sizeSmoothSpeed = 2f;

    void LateUpdate()
    {
        if (followPivot == null) return;

        Vector3 finalOffset = offset;
        if (CameraShake.Instance != null)
            finalOffset += CameraShake.Instance.shakeOffset;


        Vector3 desiredPosition = followPivot.position + finalOffset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        transform.LookAt(followPivot);

        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
        }
    }
}
