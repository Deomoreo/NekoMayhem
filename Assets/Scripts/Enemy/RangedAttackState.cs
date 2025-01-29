using UnityEngine;

public class RangedAttackState : EnemyState
{
    private float attackCooldown = 2f;
    private float nextAttackTime = 0f;
    private Transform firePoint;
    private GameObject projectilePrefab;

    public RangedAttackState(EnemyBase enemy, GameObject projectile, Transform firePoint) : base(enemy)
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
        if (Time.time >= nextAttackTime && enemy.CanSeePlayer())
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
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = (enemy.player.position - firePoint.position).normalized * 10f;
            }
            GameObject.Destroy(projectile, 5f);
        }
    }
}
