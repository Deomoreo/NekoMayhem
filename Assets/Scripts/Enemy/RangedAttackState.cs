using UnityEngine;

public class RangedAttackState : EnemyState
{
    private float attackCooldown = 2f; // Tempo tra un attacco e l'altro
    private float nextAttackTime = 0f; // Quando può attaccare di nuovo
    private Transform player; // Riferimento al giocatore
    private GameObject projectilePrefab; // Prefab del proiettile
    private Transform firePoint; // Punto di origine del proiettile

    public RangedAttackState(EnemyAI enemy, GameObject projectile, Transform firePoint) : base(enemy)
    {
        this.projectilePrefab = projectile;
        this.firePoint = firePoint;
        this.player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    public override void Enter()
    {
        Debug.Log("Entra nello stato di attacco a distanza.");
        //enemy.Animator.SetTrigger("RangedAttack");
    }

    public override void Update()
    {
        // Controlla la distanza dal giocatore
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, player.position);
        if (distanceToPlayer > enemy.AttackRange)
        {
            // Torna a inseguire se il giocatore è fuori range
            enemy.ChangeState(new ChaseState(enemy));
            return;
        }

        // Attacca se possibile
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
            projectile.GetComponent<Projectile>().Initialize(player.position - firePoint.position);
        }
    }

    public override void Exit()
    {
        Debug.Log("Esce dallo stato di attacco a distanza.");
    }
}
