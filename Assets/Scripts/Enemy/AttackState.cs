using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackState : EnemyState
{
    public AttackState(EnemyBase enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.agent.ResetPath();
    }

    public override void Update()
    {
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);

        if (distanceToPlayer > enemy.attackRadius)
        {
            enemy.TransitionToState(new ChaseState(enemy));
        }

        enemy.AttackPlayer();
    }
}



