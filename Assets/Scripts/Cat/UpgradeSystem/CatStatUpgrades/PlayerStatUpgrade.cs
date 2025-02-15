using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerStatUpgrade", menuName = "Upgrades/PlayerStatUpgrade")]
public class PlayerStatUpgrade : UpgradeBase
{
    public enum StatType { Forza, Critico, Velocit‡Attacco, Difesa }
    public StatType statType;
    public float upgradeValue;

    public override void ApplyUpgrade()
    {
        base.ApplyUpgrade();
        PlayerStats.Instance.ApplyStatUpgrade(statType.ToString(), upgradeValue);
    }
}
