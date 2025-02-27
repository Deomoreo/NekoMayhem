using UnityEngine;
using UnityEngine.AI;

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

    [Header("Movement & Attack Settings")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float attackRadius = 2f;
    public float chaseRadius = 10f;
    public float rangedAttackRadius = 5f;

    [Header("Layer")]
    public LayerMask obstacleLayer;
    protected EnemyState currentState;

    [HideInInspector] public NavMeshAgent agent;

    protected virtual void Start()
    {
        if (obstacleLayer == 0)
            obstacleLayer = LayerMask.GetMask("Walls");
        agent = GetComponent<NavMeshAgent>();
    }

    protected virtual void Update()
    {
        currentState?.Update();
    }

    public abstract void AttackPlayer();

    public void TransitionToState(EnemyState newState)
    {
        if (currentState == newState) return;  // Previene loop infinito
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
        Vector3 startPos = transform.position + Vector3.up * 0.5f;
        Vector3 dir = (target.position - startPos).normalized;
        float dist = Vector3.Distance(startPos, target.position);
        if (Physics.Raycast(startPos, dir, out hit, dist, obstacleLayer))
            return false;
        return true;
    }

    public void SetRandomPatrolPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * 5f;
        randomDir += transform.position;
        if (NavMesh.SamplePosition(randomDir, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
            agent.SetDestination(navHit.position);
        else
            SetRandomPatrolPoint();
    }

    // Metodo comune per ruotare verso il target
    public void RotateTowardsTarget(Transform target)
    {
        Vector3 direction = (target.position - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
    }
}
