using UnityEngine;

public class RangedAttackState : EnemyState
{
    private float attackCooldown = 2f;
    private float nextAttackTime = 0f;
    private Transform firePoint;
    private GameObject projectilePrefab;

    public RangedAttackState(EnemyRanged enemy, GameObject projectile, Transform firePoint) : base(enemy)
    {
        this.projectilePrefab = projectile;
        this.firePoint = firePoint;
    }

    public override void Enter()
    {
        Debug.Log("Entra nello stato di attacco a distanza.");
    }

    public override void Update()
    {
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.player.position);
        if (distanceToPlayer > enemy.attackRadius)
        {
            enemy.TransitionToState(new ChaseState(enemy));
            return;
        }

        if (Time.time >= nextAttackTime)
        {
            Attack();
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    private void Attack()
    {
        Debug.Log("Attacco a distanza eseguito.");
        if (projectilePrefab != null && firePoint != null)
        {
            GameObject projectile = GameObject.Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            projectile.GetComponent<Rigidbody>().velocity = (enemy.player.position - firePoint.position).normalized * 10f;
        }
    }
}
