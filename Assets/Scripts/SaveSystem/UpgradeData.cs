using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UpgradeData
{
    public string upgradeName;
    public bool isUnlocked;

    public UpgradeData(string name, bool unlocked)
    {
        upgradeName = name;
        isUnlocked = unlocked;
    }
}
