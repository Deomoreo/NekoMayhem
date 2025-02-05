using System.Collections;
using UnityEngine;

public class Room : MonoBehaviour
{
    [Header("Impostazioni Stanza")]
    public Vector2Int gridPosition;
    public bool isDiscovered = false;
    public CanvasGroup roomCanvas;
    public DoorTrigger[] doors;

    private void Awake()
    {
        if (roomCanvas == null)
            Debug.LogError("CanvasGroup non assegnato per la stanza in posizione: " + gridPosition);
    }

    /// <summary>
    /// Inizializza la stanza con la posizione specificata e la rende invisibile.
    /// </summary>
    public void Initialize(Vector2Int position)
    {
        gridPosition = position;
        UpdateVisibility(false);
        Debug.Log("Inizializzata stanza in posizione: " + gridPosition);
    }

    /// <summary>
    /// Segna la stanza come scoperta e la rende visibile.
    /// </summary>
    public void DiscoverRoom()
    {
        if (!isDiscovered)
        {
            isDiscovered = true;
            Debug.Log("Scoperta stanza in posizione: " + gridPosition);
        }
        UpdateVisibility(true);
    }

    /// <summary>
    /// Aggiorna la visibilità della stanza.
    /// </summary>
    private void UpdateVisibility(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>
    /// Esegue l'effetto di fade-in sul Canvas della stanza.
    /// </summary>
    public IEnumerator FadeIn()
    {
        Debug.Log("FadeIn per stanza in posizione: " + gridPosition);
        yield return FadeCanvas(1f);
    }

    /// <summary>
    /// Esegue l'effetto di fade-out sul Canvas della stanza.
    /// </summary>
    public IEnumerator FadeOut()
    {
        Debug.Log("FadeOut per stanza in posizione: " + gridPosition);
        yield return FadeCanvas(0f);
    }

    /// <summary>
    /// Effettua un'interpolazione lineare sull'alpha del CanvasGroup per creare l'effetto di fade.
    /// </summary>
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
