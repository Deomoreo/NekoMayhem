using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public float playerPositionX;
    public float playerPositionY;  
    public float playerPositionZ;
    public float playerHP;

    // Interactable object data
    public List<InteractableData> interactableObjects = new List<InteractableData>();
}
