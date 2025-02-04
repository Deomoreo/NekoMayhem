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
        if (Vector3.Distance(enemy.transform.position, enemy.player.position) <= enemy.chaseRadius)
        {
            if (enemy.CanSeePlayer())
            {
                enemy.TransitionToState(new ChaseState(enemy));
            }
            else
            {
                enemy.TransitionToState(new PatrolState(enemy));
            }
        }
        else if (!enemy.agent.pathPending && enemy.agent.remainingDistance < 0.5f)
        {
            enemy.SetRandomPatrolPoint(); 
        }
    }
}