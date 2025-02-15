using UnityEngine;
using System.Collections.Generic;

public class PlayerPerks : MonoBehaviour
{
    public static PlayerPerks Instance;

    public List<string> unlockedPerks = new List<string>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void UnlockPerk(string perkName)
    {
        if (!unlockedPerks.Contains(perkName))
        {
            unlockedPerks.Add(perkName);
            Debug.Log($"Perk sbloccato: {perkName}");
        }
    }

    public bool HasPerk(string perkName)
    {
        return unlockedPerks.Contains(perkName);
    }
}
