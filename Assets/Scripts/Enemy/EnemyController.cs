using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    [Header("Statistiche Base")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Stato e Bonus")]
    private bool isStunned = false;
    private NavMeshAgent agent;
    private Animator animator;

    public float damageFlashDuration = 0.1f;
    public bool isDead { get; private set; } = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} ha subito {damage} danni. Salute attuale: {currentHealth}");
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.4f, 0.9f); // Esegue il camera shake

        StartCoroutine(FlashDamage());
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void ApplyStun(float duration)
    {
        if (!isStunned && !isDead)
        {
            isStunned = true;
            if (agent != null)
            {
                agent.isStopped = true;
                agent.ResetPath();         
                agent.velocity = Vector3.zero; 
            }
            EnemyMelee meleeComponent = GetComponent<EnemyMelee>();
            if (meleeComponent != null)
            {
                meleeComponent.enabled = false;
            }

            if (animator != null)
            {
                animator.Play("Stunned");
                animator.SetBool("IsStunned", true);
            }

            StartCoroutine(RecoverFromStun(duration));
        }
    }
    private IEnumerator FlashDamage()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Material mat = rend.material;
            float originalValue = mat.GetFloat("_Hit");
            mat.SetFloat("_Hit", 2);
            yield return new WaitForSeconds(damageFlashDuration);
            mat.SetFloat("_Hit", originalValue);
        }
    }

    private IEnumerator RecoverFromStun(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (isDead) yield break;
        isStunned = false;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
        EnemyMelee meleeComponent = GetComponent<EnemyMelee>();
        if (meleeComponent != null)
        {
            meleeComponent.enabled = true;
        }
        if (animator != null)
            animator.SetBool("IsStunned", false);
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} è morto.");
        isDead = true;
        EnemyController controller = GetComponent<EnemyController>();
        if (controller != null)
            controller.isDead = true;
        EnemyMelee meleeComponent = GetComponent<EnemyMelee>();
        if (meleeComponent != null)
            meleeComponent.enabled = false;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        if (animator != null)
        {
            animator.SetLayerWeight(1, 0f);
            animator.Play("Death", 0, 0f);
            animator.SetBool("IsDeath", true);
        }

        StartCoroutine(DelayedDissolve(1.0f));
    }
    private IEnumerator DelayedDissolve(float delay)
    {
        yield return new WaitForSeconds(delay);
        EnemyDissolve dissolve = GetComponent<EnemyDissolve>();
        if (dissolve != null)
        {
            dissolve.StartDissolve();
        }
        Destroy(gameObject, 2.25f);
    }
}
