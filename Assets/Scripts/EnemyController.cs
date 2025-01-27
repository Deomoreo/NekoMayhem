using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public int maxHealth = 50;
    private int currentHealth;

    public float attackDamage = 10f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    private float lastAttackTime;
    public Transform target; 

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} ha subito {damage} danni! Salute attuale: {currentHealth}");

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
    void Update()
    {
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= attackRange && Time.time >= lastAttackTime + attackCooldown)
            {
                Attack();
                lastAttackTime = Time.time;
            }
        }
    }

    void Attack()
    {
        Debug.Log("Il nemico attacca!");
        if (target.TryGetComponent<CatHealth>(out CatHealth catHealth))
        {
            catHealth.TakeDamage(attackDamage);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            target = other.transform;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            target = null;
        }
    }
}
