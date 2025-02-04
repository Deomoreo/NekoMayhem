using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public abstract class EnemyBase : MonoBehaviour
{
    public Transform player;
    public NavMeshAgent agent;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float attackRadius = 2f;
    public float chaseRadius = 10f;
    public float rangedAttackRadius = 5f;
    public LayerMask obstacleLayer;
    protected EnemyState currentState;

    void Start()
    {
        // Imposta manualmente il LayerMask per gli ostacoli se non è stato configurato
        if (obstacleLayer == 0)
        {
            obstacleLayer = LayerMask.GetMask("Walls");
        }
    }
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
        Vector3 startPosition = transform.position + Vector3.up * 0.5f;
        Vector3 directionToPlayer = (player.position - startPosition).normalized;
        float distanceToPlayer = Vector3.Distance(startPosition, player.position);

        bool canSee = false;

        if (Physics.Raycast(startPosition, directionToPlayer, out hit, distanceToPlayer, obstacleLayer))
        {
            Debug.DrawRay(startPosition, directionToPlayer * distanceToPlayer, Color.red, 2.0f);

            if (hit.collider.CompareTag("Player"))
            {
                canSee = false;
            }
        }
        else
        {
            Debug.DrawRay(startPosition, directionToPlayer * distanceToPlayer, Color.green, 2.0f);
            canSee = true;
        }
        return canSee;
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
