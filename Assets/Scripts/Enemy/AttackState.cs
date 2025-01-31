using System.Collections;
using System.Collections.Generic;
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

        if (!enemy.CanSeePlayer())
        {
            enemy.TransitionToState(new ChaseState(enemy));
            return;
        }

        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);
        if (distanceToPlayer > enemy.attackRadius && distanceToPlayer <= enemy.chaseRadius)
        {
            enemy.TransitionToState(new ChaseState(enemy));
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




