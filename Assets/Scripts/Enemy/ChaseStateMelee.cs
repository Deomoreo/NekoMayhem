using UnityEngine;

public class ChaseStateMelee : EnemyState
{
    public ChaseStateMelee(EnemyBase enemy) : base(enemy) { }

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
        if (distanceToPlayer <= enemy.attackRadius)
        {
            enemy.TransitionToState(new AttackState(enemy));
            return;
        }
        enemy.agent.SetDestination(enemy.ActivePlayerTarget.position);
    }
}
