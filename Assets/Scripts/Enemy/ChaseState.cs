using System.Collections;
using System.Collections.Generic;
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
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);

        if (!enemy.CanSeePlayer())
        {
            enemy.TransitionToState(new PatrolState(enemy));
            return;
        }
        else if (distanceToPlayer <= enemy.rangedAttackRadius)
        {
            enemy.TransitionToState(new RangedAttackState(enemy, enemy.GetComponent<EnemyRanged>().projectilePrefab, enemy.GetComponent<EnemyRanged>().firePoint));
            return;
        }

        enemy.agent.SetDestination(enemy.player.position);
    }
}

