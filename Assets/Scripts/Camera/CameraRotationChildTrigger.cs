using UnityEngine;

public class CameraRotationChildTrigger : MonoBehaviour
{
    [Tooltip("Se true, questo trigger rappresenta l'ingresso (esegue la rotazione in avanti); altrimenti, l'uscita (torna allo stato iniziale).")]
    public bool isEntryTrigger = true;

    private CameraRotationTrigger parentTrigger;

    private void Start()
    {
        // Trova lo script sul padre
        parentTrigger = GetComponentInParent<CameraRotationTrigger>();
        if (parentTrigger == null)
        {
            Debug.LogError("CameraRotationChildTrigger: Non è stato trovato");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (isEntryTrigger)
            {
                parentTrigger.TriggerEntry();
            }
            else
            {
                parentTrigger.TriggerExit();
            }
        }
    }
}
