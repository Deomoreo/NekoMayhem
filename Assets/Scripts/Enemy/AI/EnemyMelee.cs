using UnityEngine.AI;
using UnityEngine;
using Unity.IO.LowLevel.Unsafe;

public class EnemyMelee : EnemyBase
{
    [Header("Melee Attack Settings")]
    public int meleeDamage = 10;
    public float attackAngleThreshold = 60f;
    public float stopDistance = 1.5f;
    public float attackCooldown = 3f;  // Cooldown impostato a 3 secondi
    public float lastAttackTime = -Mathf.Infinity;  // Inizializzato per permettere il primo attacco immediato

    private CatParry catParry;
    private EnemyController enemyController;
    private Animator animator;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        enemyController = GetComponent<EnemyController>();
        catParry = FindObjectOfType<CatParry>();

        TransitionToState(new PatrolState(this));
    }

    public override void AttackPlayer()
    {
        TransitionToState(new AttackState(this));
    }

    public void ApplyDamage()
    {
        Transform target = ActivePlayerTarget;
        if (target == null)
            return;
        CatParry targetParry = target.GetComponent<CatParry>();
        if (targetParry != null && targetParry.IsParrying())
        {
            catParry.AddStunnedEnemy(enemyController);
            enemyController.ApplyStun(5f);
            return;
        }
        CatHealth playerHealth = target.GetComponent<CatHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(meleeDamage);
        }
    }

    public void EndAttack()
    {
        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }

        EnemyController controller = GetComponent<EnemyController>();
        if (controller != null && controller.isDead)
            return;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            TransitionToState(new EnemyIdleState(this));
        }
    }

}
