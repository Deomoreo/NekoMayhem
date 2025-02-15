using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;
    public int animePoints = 0;
    public Text pointsText;
    public Text statsText; 
    public List<Upgrade> upgrades;
    private string currentWeapon = "Spada"; 

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        UpdateUI();
        UpdateStatsUI(); 
        PlayerStats.StatsUpdated += UpdateStatsUI;
        WeaponStats.WeaponUpdated += UpdateStatsUI;
    }

    private void OnDestroy()
    {
        PlayerStats.StatsUpdated -= UpdateStatsUI;
        WeaponStats.WeaponUpdated -= UpdateStatsUI;
    }

    public void AddPoints(int amount)
    {
        animePoints += amount;
        UpdateUI();
    }

    public bool SpendPoints(int cost)
    {
        if (animePoints >= cost)
        {
            animePoints -= cost;
            UpdateUI();
            return true;
        }
        return false;
    }

    public void PurchaseUpgrade(Upgrade upgrade)
    {
        if (SpendPoints(upgrade.cost))
        {
            upgrade.ApplyUpgrade();
        }
    }

    public void ChangeWeapon(string newWeapon)
    {
        currentWeapon = newWeapon;
        UpdateStatsUI();
    }

    private void UpdateUI()
    {
        if (pointsText != null)
            pointsText.text = "Anime: " + animePoints;
    }

    private void UpdateStatsUI()
    {
        if (statsText != null)
        {
            string statsInfo = "<b>Statistiche Player:</b>\n";
            foreach (var stat in PlayerStats.Instance.stats)
            {
                statsInfo += $"{stat.Key}: {Mathf.RoundToInt(stat.Value)}\n";
            }

            statsInfo += "\n<b>Arma Equipaggiata:</b> " + currentWeapon + "\n";

            statsInfo += "\n<b>Statistiche Arma Equipaggiata:</b>\n";
            if (WeaponStats.Instance.weaponStats.ContainsKey(currentWeapon))
            {
                statsInfo += $"Danno Base: {Mathf.RoundToInt(WeaponStats.Instance.weaponStats[currentWeapon])}\n";
            }

            int playerDamage = PlayerStats.Instance.stats.ContainsKey("Forza") ? Mathf.RoundToInt(PlayerStats.Instance.stats["Forza"]) : 0;
            int weaponDamage = WeaponStats.Instance.weaponStats.ContainsKey(currentWeapon) ? Mathf.RoundToInt(WeaponStats.Instance.weaponStats[currentWeapon]) : 0;
            int totalDamage = playerDamage + weaponDamage;

            statsInfo += "\n<b>Danni Separati:</b>\n";
            statsInfo += "Danno Player: " + playerDamage + "\n";
            statsInfo += "Danno Arma: " + weaponDamage + "\n";
            statsInfo += "\n<b>Danno Totale:</b> " + totalDamage + "\n";
            
            statsText.text = statsInfo;
        }
    }
}
