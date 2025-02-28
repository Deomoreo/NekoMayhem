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
        enemy.agent.speed = enemy.chaseSpeed;
        if (animator != null)
        {
            animator.SetBool("IsRunning", true);
            animator.SetBool("IsAttacking", false);
        }
    }

    public override void Update()
    {
        Transform target = enemy.ActivePlayerTarget;
        if (target == null)
        {
            enemy.TransitionToState(new PatrolState(enemy));
            return;
        }
        if (!enemy.CanSeePlayer())
        {
            enemy.TransitionToState(new PatrolState(enemy));
            return;
        }

        float distance = Vector3.Distance(enemy.transform.position, target.position);

        if (enemy is EnemyMelee melee)
        {
            float timeSinceLastAttack = Time.time - melee.lastAttackTime;
            if (timeSinceLastAttack < melee.attackCooldown)
            {
                Debug.Log("ChaseStateMelee: Cooldown attivo (" + (melee.attackCooldown - timeSinceLastAttack) + " sec rimanenti). Rallento il nemico.");
                enemy.agent.isStopped = false;
                enemy.agent.speed = slowSpeed;
                if (distance < melee.stopDistance)
                {
                    enemy.agent.SetDestination(enemy.transform.position);
                }
                else
                {
                    enemy.agent.SetDestination(target.position);
                }
                RotateTowardsTarget(target);
                return;
            }
        }

        enemy.agent.isStopped = false;
        enemy.agent.speed = enemy.chaseSpeed;
        enemy.agent.SetDestination(target.position);
        RotateTowardsTarget(target);

        if (distance <= enemy.attackRadius)
        {
            Debug.Log("ChaseStateMelee: Distanza di attacco raggiunta. Transizione ad AttackState.");
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
