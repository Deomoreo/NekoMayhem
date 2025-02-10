using UnityEngine;

[CreateAssetMenu(fileName = "NewPerk", menuName = "Upgrades/PerkUpgrade")]
public class PerkUpgrades : UpgradeBase
{
    public string perkName;
    public string description;

    public override void ApplyUpgrade()
    {
        base.ApplyUpgrade();
        PlayerPerks.Instance.UnlockPerk(perkName);
    }
}
