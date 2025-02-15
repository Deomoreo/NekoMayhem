using System.Collections;
using UnityEngine;

public class Room : MonoBehaviour
{
    [Tooltip("Coordinate della stanza nella griglia. X corrisponde all'asse X del mondo, Y corrisponde all'asse Z del mondo.")]
    public Vector2Int gridPosition;
    public bool isDiscovered = false;
    public CanvasGroup roomCanvas;
    public DoorTrigger[] doors;

    private void Awake()
    {
        if (roomCanvas == null)
            Debug.LogError("CanvasGroup non assegnato per la stanza in posizione: " + gridPosition);
    }
    public void Initialize(Vector2Int position)
    {
        gridPosition = position;
        UpdateVisibility(false);
        Debug.Log("Inizializzata stanza in posizione: " + gridPosition);
    }
    public void DiscoverRoom()
    {
        if (!isDiscovered)
        {
            isDiscovered = true;
            //Debug.Log("Scoperta stanza in posizione: " + gridPosition);
        }
        UpdateVisibility(true);
    }
    private void UpdateVisibility(bool visible)
    {
        gameObject.SetActive(visible);
    }
    public IEnumerator FadeIn()
    {
        Debug.Log("FadeIn per stanza in posizione: " + gridPosition);
        yield return FadeCanvas(1f);
    }
    public IEnumerator FadeOut()
    {
        Debug.Log("FadeOut per stanza in posizione: " + gridPosition);
        yield return FadeCanvas(0f);
    }
    private IEnumerator FadeCanvas(float targetAlpha)
    {
        if (roomCanvas == null)
        {
            Debug.LogError("CanvasGroup non assegnato per FadeCanvas nella stanza in posizione: " + gridPosition);
            yield break;
        }

        float duration = 0.5f;
        float startAlpha = roomCanvas.alpha;
        float time = 0f;

        while (time < duration)
        {
            roomCanvas.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        roomCanvas.alpha = targetAlpha;
    }
}
