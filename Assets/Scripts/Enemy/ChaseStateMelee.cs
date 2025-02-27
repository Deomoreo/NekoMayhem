using UnityEngine;

public class ChaseStateMelee : EnemyState
{
    private Animator animator;

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
        }
    }

    public override void Update()
    {
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.ActivePlayerTarget.position);

        if (!enemy.CanSeePlayer())
        {
            enemy.TransitionToState(new PatrolState(enemy));
            return;
        }
        if (distanceToPlayer <= enemy.attackRadius)
        {
            enemy.TransitionToState(new AttackState(enemy));
            return;
        }
        enemy.agent.SetDestination(enemy.ActivePlayerTarget.position);
        // Aggiorna il parametro "Speed" in base alla velocità corrente
        if (animator != null)
        {
            animator.SetFloat("Speed", enemy.agent.velocity.magnitude);
        }
    }
    public override void Exit()
    {
        if (animator != null)
            animator.SetBool("IsRunning", false);
    }
}
