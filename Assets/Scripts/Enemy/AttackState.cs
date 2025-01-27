using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackState : EnemyState
{
    public AttackState(EnemyAI enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.agent.ResetPath(); 
    }

    public override void Update()
    {
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);

        if (distanceToPlayer > enemy.attackRadius)
        {
            enemy.TransitionToState(enemy.chaseState);
        }

        Vector3 directionToPlayer = (enemy.player.position - enemy.transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(directionToPlayer);
        enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, lookRotation, Time.deltaTime * 5f);

        enemy.AttackPlayer();
    }
}

