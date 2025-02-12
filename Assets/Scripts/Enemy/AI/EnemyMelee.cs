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

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        patrolState = new PatrolState(this);
        chaseState = new ChaseStateMelee(this);
        attackState = new AttackState(this);

        enemyController = GetComponent<EnemyController>();
        catParry = FindObjectOfType<CatParry>();

        TransitionToState(patrolState);
    }

    public override void AttackPlayer()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= attackRadius)
        {
            if (player.GetComponent<CatParry>().IsParrying()) 
            {
                catParry.AddStunnedEnemy(enemyController);
                enemyController.ApplyStun(5f); 
                return;
            }

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
        }
    }
}
