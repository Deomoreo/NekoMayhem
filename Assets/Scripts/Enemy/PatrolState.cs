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
        Transform target = enemy.ActivePlayerTarget;
        if (target != null)
        {
            float distance = Vector3.Distance(enemy.transform.position, target.position);
            if (distance <= enemy.chaseRadius && enemy.CanSeePlayer())
            {
                if (enemy is EnemyMelee)
                    enemy.TransitionToState(new ChaseStateMelee(enemy));
                // Se ranged, si transita ad uno stato ranged
                return;
            }
        }
        if (!enemy.agent.pathPending && enemy.agent.remainingDistance < 0.5f)
        {
            enemy.SetRandomPatrolPoint();
        }
    }
}
