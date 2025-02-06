using UnityEngine;
using UnityEngine.AI;

public abstract class EnemyBase : MonoBehaviour
{
    [Header("Riferimenti")]
    public Transform player;
    public NavMeshAgent agent;

    [Header("Velocità e Distanze")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float attackRadius = 2f;
    public float chaseRadius = 10f;
    public float rangedAttackRadius = 5f; 

    [Header("Layer")]
    public LayerMask obstacleLayer; 
    protected EnemyState currentState;

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
        Debug.Log($"{gameObject.name} transita da {currentState?.GetType().Name} a {newState.GetType().Name}");
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }
    public bool CanSeePlayer()
    {
        RaycastHit hit;
        Vector3 startPosition = transform.position + Vector3.up * 0.5f;
        Vector3 directionToPlayer = (player.position - startPosition).normalized;
        float distanceToPlayer = Vector3.Distance(startPosition, player.position);

        if (Physics.Raycast(startPosition, directionToPlayer, out hit, distanceToPlayer, obstacleLayer))
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
