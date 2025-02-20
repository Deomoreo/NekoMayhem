using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public abstract class EnemyBase : MonoBehaviour
{
    [Header("Player Targets")]
    public Transform[] playerTargets;

    public Transform ActivePlayerTarget
    {
        get
        {
            Transform bestTarget = null;
            float minDistance = Mathf.Infinity;

            if (playerTargets == null || playerTargets.Length == 0)
                return null;

            foreach (Transform target in playerTargets)
            {
                if (target != null && target.gameObject.activeSelf)
                {
                    float dist = Vector3.Distance(transform.position, target.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestTarget = target;
                    }
                }
            }
            return bestTarget;
        }
    }

    [Header("Velocità e Distanze")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float attackRadius = 2f;
    public float chaseRadius = 10f;
    public float rangedAttackRadius = 5f;

    [Header("Layer")]
    public LayerMask obstacleLayer;
    protected EnemyState currentState;

    [HideInInspector]
    public NavMeshAgent agent;

    protected virtual void Start()
    {
        if (obstacleLayer == 0)
        {
            obstacleLayer = LayerMask.GetMask("Walls");
        }
    }

    protected virtual void Update()
    {
        currentState?.Update();
    }

    public abstract void AttackPlayer();

    public void TransitionToState(EnemyState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public bool CanSeePlayer()
    {
        Transform target = ActivePlayerTarget;
        if (target == null)
            return false;

        RaycastHit hit;
        Vector3 startPosition = transform.position + Vector3.up * 0.5f;
        Vector3 directionToTarget = (target.position - startPosition).normalized;
        float distanceToTarget = Vector3.Distance(startPosition, target.position);

        if (Physics.Raycast(startPosition, directionToTarget, out hit, distanceToTarget, obstacleLayer))
        {
            return false;
        }
        return true;
    }

    public void SetRandomPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * 5f;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);
        }
        else
        {
            SetRandomPatrolPoint();
        }
    }
}
