public abstract class EnemyState
{
    protected EnemyBase enemy;

    public EnemyState(EnemyBase enemy)
    {
        this.enemy = enemy;
    }
    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void Exit() { }
}
