using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    [Tooltip("Vettore che indica in quale direzione si vuole spostare. Es: (0,1) per sopra, (1,0) per destra, ecc.")]
    public Vector2Int moveDirection;

    [Tooltip("Punto in cui posizionare il player nella nuova stanza (opzionale)")]
    public Transform exitPoint;

    // Assumi di usare il TransitionManager (che è sempre attivo)
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Chiamata al TransitionManager per eseguire la transizione
            TransitionManager.Instance.DoTransition(other.gameObject, moveDirection, exitPoint);
        }
    }
}
