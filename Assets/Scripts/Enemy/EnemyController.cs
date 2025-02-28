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

        if (animator != null)
        {
            animator.SetTrigger("Hurt");
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void ApplyStun(float duration)
    {
        if (!isStunned)
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


    private IEnumerator RecoverFromStun(float duration)
    {
        yield return new WaitForSeconds(duration);
        isStunned = false;
        animator.SetBool("IsStunned", false);
        if (agent != null)
        {
            agent.isStopped = false;
        }
        EnemyMelee meleeComponent = GetComponent<EnemyMelee>();
        if (meleeComponent != null)
        {
            meleeComponent.enabled = true;
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} è morto.");
        EnemyDissolve dissolve = GetComponent<EnemyDissolve>();
        if (animator != null)
        {
            animator.Play("Death");
            animator.SetBool("IsDeath", true);
        }
        if (agent != null)
        {
            agent.isStopped = true;
        }
        if (dissolve != null)
        {
            dissolve.StartDissolve();
        }
    }
}
