using UnityEngine;

public class PatrolState : EnemyState
{
    public PatrolState(EnemyAI enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.agent.speed = enemy.patrolSpeed;
        enemy.SetRandomPatrolPoint();
    }

    public override void Update()
    {
        if (Vector3.Distance(enemy.transform.position, enemy.player.position) <= enemy.chaseRadius)
        {
            enemy.TransitionToState(enemy.chaseState);
        }

        if (!enemy.agent.pathPending && enemy.agent.remainingDistance < 0.5f)
        {
            enemy.SetRandomPatrolPoint();
        }
    }
}
