using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyRanged : EnemyBase
{
    public PatrolState patrolState;
    public ChaseState chaseState;
    public RangedAttackState rangedAttackState;
    public GameObject projectilePrefab;
    public Transform firePoint;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        patrolState = new PatrolState(this);
        chaseState = new ChaseState(this);
        rangedAttackState = new RangedAttackState(this, projectilePrefab, firePoint);

        TransitionToState(patrolState);
    }

    public override void AttackPlayer()
    {
        Debug.Log("Il nemico ranged attacca il giocatore sparando!");
    }
}
