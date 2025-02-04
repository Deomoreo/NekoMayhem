using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    public Vector2Int gridPosition;
    public bool isDiscovered = false;
    public CanvasGroup roomCanvas;
    public DoorTrigger[] doors;

    public void Initialize(Vector2Int position, GridManager manager)
    {
        gridPosition = position;
        UpdateVisibility(false);
    }

    public void DiscoverRoom()
    {
        isDiscovered = true;
        UpdateVisibility(true);
    }

    private void UpdateVisibility(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void FadeIn()
    {
        StartCoroutine(FadeCanvas(1));
    }

    public void FadeOut()
    {
        StartCoroutine(FadeCanvas(0));
    }

    private IEnumerator FadeCanvas(float targetAlpha)
    {
        float duration = 0.5f;
        float startAlpha = roomCanvas.alpha;
        float time = 0;

        while (time < duration)
        {
            roomCanvas.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        roomCanvas.alpha = targetAlpha;
    }
}

