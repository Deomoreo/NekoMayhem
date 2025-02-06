using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public int maxHealth = 50;
    private int currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} ha subito {damage} danni! Salute attuale: {currentHealth}");

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.2f, 0.5f); //shake

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} è stato sconfitto!");
        Destroy(gameObject);
    }
}
