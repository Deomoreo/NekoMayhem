using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    public Vector2Int moveDirection;
    private bool canUseDoor = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && canUseDoor)
        {
            StartCoroutine(DisableDoorTemporarily());
            StartCoroutine(WaitForTransitionAndMove());
        }
    }

    private IEnumerator DisableDoorTemporarily()
    {
        canUseDoor = false;
        yield return new WaitForSeconds(1f); // Previene bug di attraversamento immediato
        canUseDoor = true;
    }

    private IEnumerator WaitForTransitionAndMove()
    {
        yield return new WaitUntil(() => !FindObjectOfType<GridManager>().isTransitioning);
        FindObjectOfType<GridManager>().TryMoveToRoom(moveDirection);
    }
}
