using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponUpgrade", menuName = "Upgrades/WeaponUpgrade")]
public class WeaponUpgrade : UpgradeBase
{
    public enum WeaponType { Spada, Arco, Bastone }
    public WeaponType weaponType;
    public float damageIncrease;

    public override void ApplyUpgrade()
    {
        base.ApplyUpgrade();
        WeaponStats.Instance.ApplyWeaponUpgrade(weaponType.ToString(), damageIncrease);
    }
}
