using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseState : EnemyState
{
    public ChaseState(EnemyBase enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.agent.speed = enemy.chaseSpeed;
    }

    public override void Update()
    {
        if (!enemy.CanSeePlayer())
        {
            enemy.TransitionToState(new PatrolState(enemy));
            return;
        }

        enemy.agent.SetDestination(enemy.player.position);

        if (enemy.CanSeePlayer())
        {
            Vector3 directionToPlayer = (enemy.player.position - enemy.transform.position).normalized;
            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, Quaternion.LookRotation(directionToPlayer), Time.deltaTime * 5f); 
        }
    }
}

