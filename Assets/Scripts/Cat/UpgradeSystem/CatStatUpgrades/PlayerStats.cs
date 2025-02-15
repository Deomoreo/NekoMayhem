using UnityEngine;
using System.Collections.Generic;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance;

    public Dictionary<string, float> stats = new Dictionary<string, float>()
    {
        { "Forza", 10 },
        { "Critico", 5 },
        { "Velocità Attacco", 1.2f },
        { "Difesa", 5 }
    };

    public delegate void OnStatsUpdated();
    public static event OnStatsUpdated StatsUpdated; 

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void ApplyStatUpgrade(string statName, float value)
    {
        if (stats.ContainsKey(statName))
        {
            stats[statName] += value;
            Debug.Log($"Upgrade: {statName} aumentato di {value}. Nuovo valore: {stats[statName]}");

            StatsUpdated?.Invoke();
        }
        else
        {
            Debug.LogError($"Statistica {statName} non trovata!");
        }
    }
}
