using UnityEngine;
using UnityEngine.AI;

public abstract class EnemyBase : MonoBehaviour
{
    public Transform player;
    public NavMeshAgent agent;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float attackRadius = 2f;
    public float chaseRadius = 10f;
    public LayerMask obstacleLayer;
    protected EnemyState currentState;

    void Update()
    {
        if (currentState != null)
        {
            currentState.Update();
        }
    }

    public abstract void AttackPlayer();

    public bool CanSeePlayer()
    {
        RaycastHit hit;
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (Physics.Raycast(transform.position, directionToPlayer, out hit, distanceToPlayer))
        {
            Debug.DrawRay(transform.position, directionToPlayer * distanceToPlayer, Color.red, 0.1f);
            return hit.collider.CompareTag("Player");
        }
        Debug.DrawRay(transform.position, directionToPlayer * distanceToPlayer, Color.green, 0.1f);
        return false;
    }

    public void TransitionToState(EnemyState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
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
