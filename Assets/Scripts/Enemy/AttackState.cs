using UnityEngine;

public class AttackState : EnemyState
{
    private float attackTimer;
    private float attackCooldown = 1.5f;

    public AttackState(EnemyBase enemy) : base(enemy) { }

    public override void Enter()
    {
        attackTimer = 0f;
        enemy.agent.isStopped = true;
    }

    public override void Update()
    {
        attackTimer += Time.deltaTime;
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.ActivePlayerTarget.position);

        if (distanceToPlayer > enemy.attackRadius)
        {
            enemy.TransitionToState(new ChaseStateMelee(enemy));
            return;
        }
        if (!enemy.CanSeePlayer())
        {
            enemy.TransitionToState(new PatrolState(enemy));
            return;
        }

        if (attackTimer >= attackCooldown)
        {
            enemy.AttackPlayer();
            attackTimer = 0f;
        }
    }

    public override void Exit()
    {
        enemy.agent.isStopped = false;
    }
}
