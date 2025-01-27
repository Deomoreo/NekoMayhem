using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseState : EnemyState
{
    public ChaseState(EnemyAI enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.agent.speed = enemy.chaseSpeed;
    }

    public override void Update()
    {
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);

        if (distanceToPlayer <= enemy.attackRadius)
        {
            enemy.TransitionToState(enemy.attackState);
        }
        else if (distanceToPlayer > enemy.chaseRadius)
        {
            enemy.TransitionToState(enemy.patrolState);
        }
        else
        {
            enemy.agent.SetDestination(enemy.player.position);
        }
    }
}

