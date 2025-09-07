using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Hitbox : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private string targetTag = "Player";

    private Collider col;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        col.enabled = false;
    }

    public void SetActive(bool active) => col.enabled = active;

    private void OnTriggerEnter(Collider other)
    {
        if (!col.enabled) return;
        if (other.CompareTag(targetTag))
        {
            // Qui invoca l’health del nemico
            // other.GetComponent<EnemyHealth>()?.TakeDamage(damage);
            //Debug.Log($"Hit {other.name} for {damage} damage.");
        }
    }
}
