using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Utility class to store InteractableObjects data
[System.Serializable]
public class InteractableData
{
    public string id;
    public bool isInteracted;
    public ColorData colorData;

    public InteractableData(string id, bool isInteracted, ColorData colorData)
    {
        this.id = id;
        this.isInteracted = isInteracted;
        this.colorData = colorData;
    }
}
