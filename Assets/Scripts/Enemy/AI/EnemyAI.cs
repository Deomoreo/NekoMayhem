using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public Transform player;
    public float patrolRadius = 5f;
    public float chaseRadius = 10f;
    public float attackRadius = 2f;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float AttackRange = 10f; 

    private Vector3 patrolPoint;
    private bool isChasing;
    private bool isAttacking;

    [HideInInspector] public NavMeshAgent agent;
    [HideInInspector] public PatrolState patrolState;
    [HideInInspector] public ChaseState chaseState;
    [HideInInspector] public AttackState attackState;

    private EnemyState currentState;
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        SetRandomPatrolPoint();

        patrolState = new PatrolState(this);
        chaseState = new ChaseState(this);
        attackState = new AttackState(this);

        TransitionToState(patrolState);
    }

    void Update()
    {
        if (currentState != null)
        {
            currentState.Update();
        }
    }
    public void ChangeState(EnemyState newState)
    {
        if (currentState != null)
        {
            currentState.Exit();
        }
        currentState = newState;
        currentState.Enter();
    }
    public void TransitionToState(EnemyState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }
    public void SetRandomPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(randomDirection, out navHit, patrolRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);
        }
    }
    public void AttackPlayer()
    {
        Debug.Log("Attacco il giocatore!");
    }
}
