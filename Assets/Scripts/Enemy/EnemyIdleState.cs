using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private Animator animator;

    public EnemyIdleState(EnemyBase enemy) : base(enemy)
    {
        animator = enemy.GetComponent<Animator>();
    }

    public override void Enter()
    {
        enemy.agent.isStopped = true;
        if (animator != null)
        {
            animator.SetBool("IsIdle", true);
            animator.SetBool("IsRunning", false);
        }
        enemy.agent.isStopped = true;
    }

    public override void Update()
    {
        Transform target = enemy.ActivePlayerTarget;
        if (target == null)
        {
            enemy.TransitionToState(new PatrolState(enemy));
            return;
        }
        // Se il cooldown è terminato, passa allo stato di inseguimento o attacco
        if (enemy is EnemyMelee melee)
        {
            float timeSinceLastAttack = Time.time - melee.lastAttackTime;
            if (timeSinceLastAttack >= melee.attackCooldown)
            {
                float distance = Vector3.Distance(enemy.transform.position, target.position);
                if (distance <= enemy.attackRadius)
                    enemy.TransitionToState(new AttackState(enemy));
                else
                    enemy.TransitionToState(new ChaseStateMelee(enemy));
            }
        }
        // Permetti all'enemy di ruotare verso il target anche in idle
        RotateTowardsTarget(target);
    }

    public override void Exit()
    {
        if (animator != null)
            animator.SetBool("IsIdle", false);
    }

    private void RotateTowardsTarget(Transform target)
    {
        Vector3 direction = (target.position - enemy.transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, targetRotation, Time.deltaTime * 10f);
    }
}
