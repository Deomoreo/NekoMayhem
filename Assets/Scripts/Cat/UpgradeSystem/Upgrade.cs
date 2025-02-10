using UnityEngine;

[System.Serializable]
public class Upgrade
{
    public enum UpgradeType { Statistica, Arma, Perk }
    public UpgradeType type;

    public string upgradeName;
    public int cost;
    public bool isUnlocked = false;
    public float value; // Il valore che modifica (es. +5% critico, +10 danno, ecc.)

    public void ApplyUpgrade()
    {
        switch (type)
        {
            case UpgradeType.Statistica:
                PlayerStats.Instance.ApplyStatUpgrade(upgradeName, value);
                break;
            case UpgradeType.Arma:
                WeaponStats.Instance.ApplyWeaponUpgrade(upgradeName, value);
                break;
            case UpgradeType.Perk:
                PlayerPerks.Instance.UnlockPerk(upgradeName);
                break;
        }
        Debug.Log("Upgrade applicato: " + upgradeName);
    }
}
