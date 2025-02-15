using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public float playerPositionX;
    public float playerPositionY;  
    public float playerPositionZ;
    public int roomPositionX; 
    public int roomPositionY;
    public float playerHP;

    // Upgrade system
    public int upgradePoints;
    public List<UpgradeData> upgrades = new List<UpgradeData>();

    // Interactable object data
    public List<InteractableData> interactableObjects = new List<InteractableData>();
}
