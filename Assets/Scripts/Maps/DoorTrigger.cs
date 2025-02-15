using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    [Tooltip("Vettore che indica in quale direzione si vuole spostare")]
    public Vector2Int moveDirection;

    [Tooltip("Punto in cui posizionare il player nella nuova stanza")]
    public Transform exitPoint;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            TransitionManager.Instance.DoTransition(other.gameObject, moveDirection, exitPoint);
        }
    }
}
