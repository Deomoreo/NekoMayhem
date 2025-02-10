using UnityEngine;

public abstract class UpgradeBase : ScriptableObject
{
    public string upgradeName;
    public int cost;
    public int maxLevel;
    public int currentLevel;

    public virtual void ApplyUpgrade()
    {
        Debug.Log($"Upgrade applicato: {upgradeName} - Livello {currentLevel}");
    }
}
