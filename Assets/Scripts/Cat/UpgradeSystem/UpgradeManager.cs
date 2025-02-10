using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;
    public int animePoints = 0;
    public Text pointsText;
    public List<Upgrade> upgrades;

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

    private void UpdateUI()
    {
        if (pointsText != null)
            pointsText.text = "Anime: " + animePoints;
    }
}
