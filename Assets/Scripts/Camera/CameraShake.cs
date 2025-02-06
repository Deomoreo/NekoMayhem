using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;
    private Transform camTransform;
    private Vector3 originalOffset;
    private CameraController cameraController;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        camTransform = Camera.main.transform;
        cameraController = camTransform.GetComponent<CameraController>();
        originalOffset = cameraController.offset;
    }

    public void Shake(float duration, float magnitude)
    {
        StartCoroutine(ShakeCoroutine(duration, magnitude));
    }

    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            // Modifica direttamente l'offset della camera
            cameraController.offset = originalOffset + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        cameraController.offset = originalOffset; // Ripristina l'offset originale
    }
}
