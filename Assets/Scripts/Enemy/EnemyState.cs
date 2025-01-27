using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyState
{
    protected EnemyAI enemy;

    public EnemyState(EnemyAI enemy)
    {
        this.enemy = enemy;
    }
    public virtual void Enter() { } 
    public virtual void Update() { }
    public virtual void Exit() { } 
}
