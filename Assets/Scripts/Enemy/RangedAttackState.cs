using UnityEngine;

public class RangedAttackState : EnemyState
{
    private float attackCooldown = 2f;
    private float nextAttackTime = 0f;
    private Transform firePoint;
    private GameObject projectilePrefab;

    public RangedAttackState(EnemyBase enemy, GameObject projectilePrefab, Transform firePoint) : base(enemy)
    {
        this.projectilePrefab = projectilePrefab;
        this.firePoint = firePoint;
    }

    public override void Enter()
    {
        enemy.agent.isStopped = true;
    }

    public override void Update()
    {
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);

        if (!enemy.CanSeePlayer())
        {
            enemy.TransitionToState(new PatrolState(enemy));
            return;
        }
        else if (distanceToPlayer > enemy.rangedAttackRadius && distanceToPlayer <= enemy.chaseRadius)
        {
            enemy.TransitionToState(new ChaseStateRanged(enemy));
            return;
        }

        Vector3 directionToPlayer = (enemy.player.position - enemy.transform.position).normalized;
        enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, Quaternion.LookRotation(directionToPlayer), Time.deltaTime * 5f);

        if (Time.time >= nextAttackTime)
        {
            Attack();
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    private void Attack()
    {
        if (projectilePrefab != null && firePoint != null)
        {
            GameObject projectile = GameObject.Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 shootDirection = (enemy.player.position - firePoint.position).normalized;
                rb.velocity = shootDirection * 10f;
            }
            GameObject.Destroy(projectile, 5f);
        }
    }

    public override void Exit()
    {
        enemy.agent.isStopped = false;
    }
}
