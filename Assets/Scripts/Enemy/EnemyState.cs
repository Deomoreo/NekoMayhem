using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyState
{
    protected EnemyBase enemy;

    public EnemyState(EnemyBase enemy)
    {
        this.enemy = enemy;
    }
    public virtual void Enter() { Debug.Log($"Entrato nello stato: {this.GetType().Name}"); }
    public virtual void Update() { }
    public virtual void Exit() { Debug.Log($"Uscito dallo stato: {this.GetType().Name}"); }
}
