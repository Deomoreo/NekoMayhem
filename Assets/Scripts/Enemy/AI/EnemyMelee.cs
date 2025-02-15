using UnityEngine;
using UnityEngine.AI;

public class EnemyMelee : EnemyBase
{
    public PatrolState patrolState;
    public ChaseStateMelee chaseState;
    public AttackState attackState;

    [Header("Parametri Attacco Melee")]
    public int meleeDamage = 10;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        patrolState = new PatrolState(this);
        chaseState = new ChaseStateMelee(this);
        attackState = new AttackState(this);

        TransitionToState(patrolState);
    }

    public override void AttackPlayer()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= attackRadius)
        {
            CatHealth playerHealth = player.GetComponent<CatHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(meleeDamage);
            }
            else
            {
                Debug.LogWarning("Il player non possiede il componente CatHealth!");
            }

            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }

            Debug.Log($"{gameObject.name} attacca il player (melee)!");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} ha tentato di attaccare, ma il player è fuori portata.");
        }
    }
}
