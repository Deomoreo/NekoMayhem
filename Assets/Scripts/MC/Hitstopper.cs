using System.Collections;
using UnityEngine;

public class Hitstopper : MonoBehaviour
{
    private bool running;
    public void DoHitstop(float seconds)
    {
        if (running) return;
        StartCoroutine(HitstopRoutine(seconds));
    }

    private IEnumerator HitstopRoutine(float seconds)
    {
        running = true;
        float prevScale = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, seconds));
        Time.timeScale = prevScale;
        running = false;
    }
}
