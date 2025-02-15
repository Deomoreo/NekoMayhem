using UnityEngine;

public class PatrolState : EnemyState
{
    public PatrolState(EnemyBase enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.agent.speed = enemy.patrolSpeed;
        enemy.SetRandomPatrolPoint();
    }

    public override void Update()
    {
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);

        if (distanceToPlayer <= enemy.chaseRadius && enemy.CanSeePlayer())
        {
            if (enemy is EnemyMelee)
                enemy.TransitionToState(new ChaseStateMelee(enemy));
            else if (enemy is EnemyRanged)
                enemy.TransitionToState(new ChaseStateRanged(enemy));
            return;
        }
        else if (!enemy.agent.pathPending && enemy.agent.remainingDistance < 0.5f)
        {
            enemy.SetRandomPatrolPoint();
        }
    }
}
