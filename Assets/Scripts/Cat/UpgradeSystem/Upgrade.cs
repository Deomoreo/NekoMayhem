using UnityEngine;

[System.Serializable]
public class Upgrade
{
    public string upgradeName;
    public int cost;
    public bool isUnlocked = false;

    public void ApplyUpgrade()
    {
        Debug.Log("Upgrade sbloccato: " + upgradeName);
    }
}
