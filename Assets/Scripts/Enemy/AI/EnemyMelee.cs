using UnityEngine;
using UnityEngine.AI;

public class EnemyMelee : EnemyBase
{
    public PatrolState patrolState;
    public ChaseStateMelee chaseState;
    public AttackState attackState;
    private EnemyController enemyController;

    [Header("Parametri Attacco Melee")]
    public int meleeDamage = 10;
    private CatParry catParry;

    private Animator animator;
    private bool hasAttacked = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        patrolState = new PatrolState(this);
        chaseState = new ChaseStateMelee(this);
        attackState = new AttackState(this);

        enemyController = GetComponent<EnemyController>();
        catParry = FindObjectOfType<CatParry>();

        TransitionToState(patrolState);
    }

    public override void AttackPlayer()
    {
        if (hasAttacked)
            return;

        Transform target = ActivePlayerTarget;
        if (target == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);
        if (distanceToPlayer <= attackRadius)
        {
            // Controlla se il target sta parando
            CatParry targetParry = target.GetComponent<CatParry>();
            if (targetParry != null && targetParry.IsParrying())
            {
                catParry.AddStunnedEnemy(enemyController);
                enemyController.ApplyStun(5f);
                hasAttacked = true;
                return;
            }

            CatHealth playerHealth = target.GetComponent<CatHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(meleeDamage);
            }
            else
            {
                Debug.LogWarning("Il player non possiede il componente CatHealth!");
            }

            if (animator != null)
            {
                animator.SetBool("IsAttacking", true);
            }
            hasAttacked = true;
        }
    }

    public void EndDamage()
    {
        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }
        hasAttacked = false;
    }

    public void ApplyDamage()
    {
        AttackPlayer();
    }
}
