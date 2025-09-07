using System.Collections;
using UnityEngine;

public class CameraRotationTrigger : MonoBehaviour
{
    [Header("Impostazioni Rotazione")]
    public float rotationAngle = 16f;
    public float rotationSpeed = 1f;
    public float minOffsetX = -180f;
    public float maxOffsetX = 180f;

    private CameraController cameraController;
    private float originalRotationX;
    private float targetRotationX;

    private Coroutine rotationCoroutine = null;

    private void Start()
    {
        cameraController = Camera.main.GetComponent<CameraController>();
        if (cameraController == null)
        {
            Debug.LogError("CameraRotationTrigger non è stato trovato.");
        }
        originalRotationX = cameraController.offset.x;
        targetRotationX = originalRotationX;
    }

    public void TriggerEntry()
    {
        targetRotationX = Mathf.Clamp(originalRotationX - rotationAngle, minOffsetX, maxOffsetX);
        StartRotation();
    }

    public void TriggerExit()
    {
        targetRotationX = originalRotationX;
        StartRotation();
    }
    private void StartRotation()
    {
        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
        }
        rotationCoroutine = StartCoroutine(RotateCamera());
    }

    private IEnumerator RotateCamera()
    {
        float elapsedTime = 0f;
        float startRotationX = cameraController.offset.x;
        float duration = 1f / rotationSpeed;
        while (elapsedTime < duration)
        {
            float newRotationX = Mathf.Lerp(startRotationX, targetRotationX, elapsedTime / duration);
            cameraController.offset = new Vector3(newRotationX, cameraController.offset.y, cameraController.offset.z);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        cameraController.offset = new Vector3(targetRotationX, cameraController.offset.y, cameraController.offset.z);
        rotationCoroutine = null;
    }
}
