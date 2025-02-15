using UnityEngine;
using UnityEngine.AI;

public class EnemyRanged : EnemyBase
{
    public PatrolState patrolState;
    public ChaseStateRanged chaseState;
    public RangedAttackState rangedAttackState;

    [Header("Parametri Attacco a Distanza")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        patrolState = new PatrolState(this);
        chaseState = new ChaseStateRanged(this);
        rangedAttackState = new RangedAttackState(this, projectilePrefab, firePoint);

        TransitionToState(patrolState);
    }

    public override void AttackPlayer()
    {
    }
}
