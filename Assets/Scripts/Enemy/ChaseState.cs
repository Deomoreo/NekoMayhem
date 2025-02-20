using UnityEngine;

public class ChaseState : EnemyState
{
    public ChaseState(EnemyBase enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.agent.isStopped = false;
        enemy.agent.speed = enemy.chaseSpeed;
    }

    public override void Update()
    {
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.ActivePlayerTarget.position);

        if (!enemy.CanSeePlayer())
        {
            enemy.TransitionToState(new PatrolState(enemy));
            return;
        }
        if (distanceToPlayer <= enemy.rangedAttackRadius)
        {
            EnemyRanged ranged = enemy.GetComponent<EnemyRanged>();
            if (ranged != null)
            {
                enemy.TransitionToState(new RangedAttackState(enemy, ranged.projectilePrefab, ranged.firePoint));
                return;
            }
        }
        enemy.agent.SetDestination(enemy.ActivePlayerTarget.position);
    }
}
