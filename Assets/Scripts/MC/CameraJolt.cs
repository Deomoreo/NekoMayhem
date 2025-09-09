using System.Collections;
using UnityEngine;

public class CameraJolt : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0.05f, 0f, 0f);

    public void DoJolt(float seconds)
    {
        StopAllCoroutines();
        StartCoroutine(JoltRoutine(seconds));
    }

    private IEnumerator JoltRoutine(float seconds)
    {
        Vector3 start = transform.localPosition;
        transform.localPosition = start + offset;
        yield return new WaitForSeconds(seconds);
        transform.localPosition = start;
    }
}
