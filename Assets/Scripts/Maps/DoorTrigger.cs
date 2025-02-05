using System.Collections;
using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    // Vettore direzionale per la transizione (ad es. (20, 0) per spostarsi a destra)
    public Vector2Int moveDirection;

    // Punto in cui riposizionare il giocatore nella nuova stanza (assicurarsi che sia posizionato correttamente nella nuova stanza)
    public Transform exitPoint;

    // Riferimento al componente ScreenFader (presente nella scena)
    public ScreenFader screenFader;

    // Flag interni per evitare ripetute attivazioni del trigger
    private bool canUseDoor = true;
    private bool playerIsInside = false;
    private GridManager gridManager;

    private void Awake()
    {
        gridManager = FindObjectOfType<GridManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !playerIsInside && canUseDoor)
        {
            playerIsInside = true;
            StartCoroutine(HandleDoorTransition(other.gameObject));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerIsInside = false;
    }

    private IEnumerator HandleDoorTransition(GameObject player)
    {
        canUseDoor = false;

        // Esegue il fade out globale
        yield return StartCoroutine(screenFader.FadeOut());

        // Esegue la transizione istantanea della stanza (il vecchio viene disattivato e il nuovo attivato)
        gridManager.InstantTransitionRoom(moveDirection);

        // Attende un frame per garantire che la transizione sia completata
        yield return null;

        // Riposiziona il giocatore nel punto di uscita della nuova stanza
        if (exitPoint != null)
            player.transform.position = exitPoint.position;

        // Esegue il fade in globale
        yield return StartCoroutine(screenFader.FadeIn());

        yield return new WaitForSeconds(0.5f);
        canUseDoor = true;
    }
}
