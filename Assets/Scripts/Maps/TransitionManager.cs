using System.Collections;
using UnityEngine;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    public ScreenFader screenFader;
    public GridManager gridManager;
    public float transitionDelay = 0.1f; // Breve pausa dopo il cambio stanza

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Avvia la transizione:
    /// - Disabilita il controller del player e "congela" il movimento.
    /// - Imposta l'Animator in stato di transizione (IsTransitioning = true) per portare il player in uno stato neutro.
    /// - Esegue il fade out, cambia stanza e riposiziona il player.
    /// - Esegue il fade in e riabilita il controller, riportando l'Animator in stato normale (IsTransitioning = false).
    /// </summary>
    public void DoTransition(GameObject player, Vector2Int moveDirection, Transform exitPoint)
    {
        StartCoroutine(Transition(player, moveDirection, exitPoint));
    }

    private IEnumerator Transition(GameObject player, Vector2Int moveDirection, Transform exitPoint)
    {
        // Disabilita il controllo del player (usiamo CatController come esempio)
        var catController = player.GetComponent<CatController>();
        if (catController != null)
        {
            catController.enabled = false;
        }

        // Azzeriamo eventuali velocità per evitare movimenti indesiderati
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Gestione dell'animazione: segnala l'inizio della transizione
        var animator = player.GetComponent<Animator>();
        if (animator != null)
        {
            // Assicurati di avere un parametro "IsTransitioning" nel tuo Animator Controller
            animator.SetBool("IsTransitioning", true);
        }

        // Esegue il fade out (lo schermo si scurisce)
        yield return StartCoroutine(screenFader.FadeOut());

        // Cambio istantaneo della stanza
        gridManager.InstantTransitionRoom(moveDirection);

        yield return new WaitForSeconds(transitionDelay);

        // Riposiziona il player nel nuovo ambiente, se exitPoint è impostato
        if (exitPoint != null)
        {
            player.transform.position = exitPoint.position;
            player.transform.rotation = exitPoint.rotation;
        }

        // Esegue il fade in (lo schermo torna visibile)
        yield return StartCoroutine(screenFader.FadeIn());

        // Riporta l'animator allo stato normale
        if (animator != null)
        {
            animator.SetBool("IsTransitioning", false);
        }

        // Riabilita il controller del player
        if (catController != null)
        {
            catController.enabled = true;
        }
    }
}
