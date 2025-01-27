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

    private NavMeshAgent agent;
    private Vector3 patrolPoint;
    private bool isChasing;
    private bool isAttacking;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        SetRandomPatrolPoint();
    }

    void Update()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRadius)
        {
            StopAndAttack();
        }
        else if (distanceToPlayer <= chaseRadius)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
    }

    void Patrol()
    {
        if (isChasing || isAttacking)
        {
            isChasing = false;
            isAttacking = false;
            agent.speed = patrolSpeed;
            SetRandomPatrolPoint();
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            SetRandomPatrolPoint();
        }
    }

    void ChasePlayer()
    {
        isChasing = true;
        isAttacking = false;
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);
    }

    void StopAndAttack()
    {
        if (!isAttacking)
        {
            isAttacking = true;
            isChasing = false;
            agent.ResetPath(); 

            Debug.Log("Attacco il giocatore!");
        }

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(directionToPlayer);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }

    void SetRandomPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(randomDirection, out navHit, patrolRadius, NavMesh.AllAreas))
        {
            patrolPoint = navHit.position;
            agent.SetDestination(patrolPoint);
        }
    }
}
