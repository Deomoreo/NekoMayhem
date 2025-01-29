using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyMelee : EnemyBase
{
    public PatrolState patrolState;
    public ChaseState chaseState;
    public AttackState attackState;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        patrolState = new PatrolState(this);
        chaseState = new ChaseState(this);
        attackState = new AttackState(this);

        TransitionToState(patrolState);
    }

    public override void AttackPlayer()
    {
        Debug.Log("Il nemico melee attacca il giocatore!");
    }
}
