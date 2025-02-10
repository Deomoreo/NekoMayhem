using UnityEngine;
using System.Collections.Generic;

public class WeaponStats : MonoBehaviour
{
    public static WeaponStats Instance;

    public Dictionary<string, float> weaponStats = new Dictionary<string, float>()
    {
        { "Spada", 15 },
        { "Arco", 10 },
        { "Bastone", 12 }
    };

    public delegate void OnWeaponUpdated();
    public static event OnWeaponUpdated WeaponUpdated; // 🔥 Evento per aggiornare i danni dell'arma

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void ApplyWeaponUpgrade(string weaponName, float value)
    {
        if (weaponStats.ContainsKey(weaponName))
        {
            weaponStats[weaponName] += value;
            Debug.Log($"Upgrade arma: {weaponName} aumentato di {value}. Nuovo danno: {weaponStats[weaponName]}");

            // 🔥 Emettiamo un evento ogni volta che un'arma viene potenziata
            WeaponUpdated?.Invoke();
        }
        else
        {
            Debug.LogError($"Arma {weaponName} non trovata!");
        }
    }
}
