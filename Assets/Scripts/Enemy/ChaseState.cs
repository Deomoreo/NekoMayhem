using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseState : EnemyState
{
    public ChaseState(EnemyBase enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.agent.speed = enemy.chaseSpeed;
        enemy.agent.isStopped = false;
    }

    public override void Update()
    {
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);

        if (distanceToPlayer <= enemy.attackRadius)
        {
            if (enemy is EnemyMelee)
                enemy.TransitionToState(new AttackState(enemy));
            else if (enemy is EnemyRanged)
                enemy.TransitionToState(new RangedAttackState((EnemyRanged)enemy, ((EnemyRanged)enemy).projectilePrefab, ((EnemyRanged)enemy).firePoint));
        }
        else if (distanceToPlayer > enemy.chaseRadius)
        {
            enemy.TransitionToState(new PatrolState(enemy));
        }
        else
        {
            enemy.agent.SetDestination(enemy.player.position);
        }
    }
}

