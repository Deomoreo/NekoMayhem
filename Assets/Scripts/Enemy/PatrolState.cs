using UnityEngine;

public class PatrolState : EnemyState
{
    public PatrolState(EnemyBase enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.agent.speed = enemy.patrolSpeed;
        enemy.SetRandomPatrolPoint();
    }

    public override void Update()
    {
        Debug.Log("PatrolState - aggiornamento");

        if (Vector3.Distance(enemy.transform.position, enemy.player.position) <= enemy.chaseRadius)
        {
            Debug.Log("Nemico ha avvistato il giocatore, passa a ChaseState");
            enemy.TransitionToState(new ChaseState(enemy));
        }
        else if (!enemy.agent.pathPending && enemy.agent.remainingDistance < 0.5f)
        {
            Debug.Log("Nemico ha raggiunto il punto di pattuglia, selezionando un nuovo punto");
            enemy.SetRandomPatrolPoint();
        }
    }
}