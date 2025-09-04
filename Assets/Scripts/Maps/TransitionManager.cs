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
    public void DoTransition(GameObject player, Vector2Int moveDirection, Transform exitPoint)
    {
        StartCoroutine(Transition(player, moveDirection, exitPoint));
    }
    private IEnumerator Transition(GameObject player, Vector2Int moveDirection, Transform exitPoint)
    {
        // Disabilita il controllo del player
        //var catController = player.GetComponent<CatController>();
        //if (catController != null)
        //{
        //    catController.enabled = false;
        //}

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
            animator.SetBool("IsTransitioning", true);
        }

        yield return StartCoroutine(screenFader.FadeOut());

        gridManager.InstantTransitionRoom(moveDirection);

        yield return new WaitForSeconds(transitionDelay);

        // Riposiziona il player nel nuovo ambiente, se exitPoint è impostato
        if (exitPoint != null)
        {
            player.transform.SetPositionAndRotation(exitPoint.position, exitPoint.rotation);
        }

        yield return StartCoroutine(screenFader.FadeIn());

        if (animator != null)
        {
            animator.SetBool("IsTransitioning", false);
        }
        //if (catController != null)
        //{
        //    catController.enabled = true;
        //}
    }
}
