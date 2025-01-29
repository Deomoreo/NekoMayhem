using UnityEngine;

public class PatrolState : EnemyState
{
    private float patrolTimer = 0f;
    private float maxPatrolTime = 5f;

    public PatrolState(EnemyBase enemy) : base(enemy) { }

    public override void Update()
    {
        patrolTimer += Time.deltaTime;
        Debug.Log("Stato: Pattugliamento, Timer: " + patrolTimer);

        if (Vector3.Distance(enemy.transform.position, enemy.player.position) <= enemy.chaseRadius)
        {
            enemy.TransitionToState(new ChaseState(enemy));
        }
        else if (!enemy.agent.pathPending && enemy.agent.remainingDistance < 0.5f)
        {
            if (patrolTimer >= maxPatrolTime)
            {
                enemy.SetRandomPatrolPoint();
                patrolTimer = 0f;
            }
        }
    }
}