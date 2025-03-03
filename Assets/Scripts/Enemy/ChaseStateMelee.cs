using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public class ChaseStateMelee : EnemyState
{
    private Animator animator;
    private float slowSpeed = 0.65f;

    public ChaseStateMelee(EnemyBase enemy) : base(enemy)
    {
        animator = enemy.GetComponent<Animator>();
    }

    public override void Enter()
    {
        enemy.agent.isStopped = false;

        if (enemy is EnemyMelee melee)
        {
            float timeSinceLastAttack = Time.time - melee.lastAttackTime;
            if (timeSinceLastAttack < melee.attackCooldown)
            {
                // Durante il cooldown: impostiamo velocità ridotta e animazione Idle.
                enemy.agent.speed = slowSpeed;
                if (animator != null)
                {
                    animator.SetBool("IsIdle", true);
                    animator.SetBool("IsRunning", false);
                    animator.SetBool("IsAttacking", false);
                }
            }
            else
            {
                // Nessun cooldown: modalità inseguimento attivo.
                enemy.agent.speed = enemy.chaseSpeed;
                if (animator != null)
                {
                    animator.SetBool("IsRunning", true);
                    animator.SetBool("IsIdle", false);
                    animator.SetBool("IsAttacking", false);
                }
            }
        }
        else
        {
            // Comportamento per altri tipi di enemy.
            enemy.agent.speed = enemy.chaseSpeed;
            if (animator != null)
            {
                animator.SetBool("IsRunning", true);
                animator.SetBool("IsIdle", false);
                animator.SetBool("IsAttacking", false);
            }
        }
    }

    public override void Update()
    {
        Transform target = enemy.ActivePlayerTarget;
        if (target == null || !enemy.CanSeePlayer())
        {
            enemy.TransitionToState(new PatrolState(enemy));
            return;
        }

        float distance = Vector3.Distance(enemy.transform.position, target.position);

        if (enemy is EnemyMelee melee)
        {
            EnemyController controller = enemy.GetComponent<EnemyController>();
            if (controller != null && controller.isDead)
                return;

            float timeSinceLastAttack = Time.time - melee.lastAttackTime;
            if (timeSinceLastAttack < melee.attackCooldown)
            {
                // Durante il cooldown: manteniamo velocità ridotta e animazione Idle.
                enemy.agent.speed = slowSpeed;
                if (animator != null)
                {
                    animator.SetBool("IsIdle", true);
                    animator.SetBool("IsRunning", false);
                }
                // Se il target è troppo vicino, fermiamo il movimento, altrimenti continuiamo a seguire.
                if (distance < melee.stopDistance)
                {
                    enemy.agent.SetDestination(enemy.transform.position);
                }
                else
                {
                    enemy.agent.SetDestination(target.position);
                }
                RotateTowardsTarget(target);
                // Rimaniamo nello stesso stato fino a che il cooldown non è terminato.
                return;
            }
            else
            {
                // Cooldown terminato: attiviamo l'inseguimento attivo.
                enemy.agent.speed = enemy.chaseSpeed;
                if (animator != null)
                {
                    animator.SetBool("IsRunning", true);
                    animator.SetBool("IsIdle", false);
                }
            }
        }

        // Logica di inseguimento per quando il cooldown non è attivo (o per altri tipi di enemy).
        enemy.agent.isStopped = false;
        enemy.agent.SetDestination(target.position);
        RotateTowardsTarget(target);

        // Se il nemico è in portata, transizione allo stato di attacco.
        if (distance <= enemy.attackRadius)
        {
            enemy.TransitionToState(new AttackState(enemy));
            return;
        }

        if (animator != null)
            animator.SetFloat("Speed", enemy.agent.velocity.magnitude);
    }

    public override void Exit()
    {
        if (animator != null)
            animator.SetBool("IsRunning", false);
    }

    private void RotateTowardsTarget(Transform target)
    {
        Vector3 direction = (target.position - enemy.transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, targetRotation, Time.deltaTime * 10f);
    }
}
