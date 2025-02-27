using UnityEngine;
using System.Collections;

public class AttackState : EnemyState
{
    private Animator animator;
    private bool canAttack = true;
    private bool isExitingAttack = false;
    private static readonly int AttackLayer = 1;
    private float attackCooldown = 3f;
    private bool isOnCooldown = false;
    private float attackDuration = 0.5f; // Durata stimata dell'animazione d'attacco

    public AttackState(EnemyBase enemy) : base(enemy)
    {
        animator = enemy.GetComponent<Animator>();
        if (enemy is EnemyMelee melee)
        {
            attackCooldown = melee.attackCooldown;
        }
    }

    public override void Enter()
    {
        // Ferma l'agente e rallenta la velocità per l'attacco
        enemy.agent.isStopped = true;
        enemy.agent.speed *= 0.5f;

        // Aggiorna subito lastAttackTime per i nemici melee, così da far partire il cooldown
        if (enemy is EnemyMelee meleeEnemy)
        {
            meleeEnemy.lastAttackTime = Time.time;
            Debug.Log("AttackState.Enter: lastAttackTime aggiornato a " + meleeEnemy.lastAttackTime);
        }

        isExitingAttack = false;
        StartAttack();
    }

    public override void Update()
    {
        if (!canAttack || isExitingAttack) return;
        Transform target = enemy.ActivePlayerTarget;
        if (target == null || Vector3.Distance(enemy.transform.position, target.position) > enemy.attackRadius)
        {
            ExitAttack();
            return;
        }
        RotateTowardsTarget(target);
    }

    public override void Exit()
    {
        ExitAttack();
    }

    private void StartAttack()
    {
        if (!canAttack || isOnCooldown) return;
        canAttack = false;
        isOnCooldown = true;

        Vector3 direction = (enemy.ActivePlayerTarget.position - enemy.transform.position).normalized;
        direction.y = 0;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, lookRotation, 0.2f);

        enemy.agent.isStopped = true;
        enemy.agent.velocity = Vector3.zero;

        if (AttackLayer == -1)
        {
            Debug.LogError("Layer 'UpperBodyLayer' non trovato!");
            return;
        }
        animator.SetBool("IsAttacking", true);

        if (animator.HasState(AttackLayer, Animator.StringToHash("AttackAnimation")))
        {
            enemy.StartCoroutine(AdjustLayerWeight(1f, 0.2f));
            animator.Play("AttackAnimation", AttackLayer);
        }
        else
        {
            Debug.LogError("Lo stato 'AttackAnimation' non esiste nel layer UpperBodyLayer!");
        }

        enemy.StartCoroutine(AttackRoutine());
    }

    private void ExitAttack()
    {
        if (isExitingAttack) return;
        enemy.StartCoroutine(AdjustLayerWeight(0f, 0.2f));
        animator.SetBool("IsAttacking", false);
        isExitingAttack = true;
        enemy.agent.isStopped = false;
        enemy.agent.speed *= 2f; // Ripristina la velocità normale
    }

    private IEnumerator AdjustLayerWeight(float targetWeight, float duration)
    {
        float startWeight = animator.GetLayerWeight(AttackLayer);
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float newWeight = Mathf.Lerp(startWeight, targetWeight, elapsedTime / duration);
            animator.SetLayerWeight(AttackLayer, newWeight);
            yield return null;
        }
        animator.SetLayerWeight(AttackLayer, targetWeight);
    }

    private void RotateTowardsTarget(Transform target)
    {
        Vector3 direction = (target.position - enemy.transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, targetRotation, Time.deltaTime * 10f);
    }

    private IEnumerator AttackRoutine()
    {
        // Attende la durata dell'attacco
        yield return new WaitForSeconds(attackDuration);
        // (Qui si assume che l'animazione o un evento esterno gestisca l'applicazione del danno)
        enemy.StartCoroutine(AdjustLayerWeight(0f, 0.2f));
        enemy.TransitionToState(new ChaseStateMelee(enemy));
        isOnCooldown = false;
        canAttack = true;
    }
}
