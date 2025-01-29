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
    protected EnemyState currentState;
    

    public abstract void AttackPlayer();

    public void TransitionToState(EnemyState newState)
    {
        Debug.Log($"Transizione a: {newState.GetType().Name}");
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
            Debug.Log("Nuovo punto di pattuglia trovato.");
        }
        else
        {
            Debug.Log("Tentativo fallito, riprovo a trovare un punto di pattuglia...");
            SetRandomPatrolPoint();
        }
    }
}
