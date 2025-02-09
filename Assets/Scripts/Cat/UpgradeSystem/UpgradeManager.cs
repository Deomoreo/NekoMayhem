using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UpgradeManager : MonoBehaviour
{
    public List<Upgrade> upgrades = new List<Upgrade>();
    public Transform upgradePanel; // UI dove mostriamo gli upgrade
    public GameObject upgradeButtonPrefab; // Prefab dei bottoni

    private void Start()
    {
        GenerateUpgradeButtons();
    }

    void GenerateUpgradeButtons()
    {
        foreach (Upgrade upgrade in upgrades)
        {
            GameObject buttonObj = Instantiate(upgradeButtonPrefab, upgradePanel);
            Button button = buttonObj.GetComponent<Button>();
            Text buttonText = buttonObj.GetComponentInChildren<Text>();

            buttonText.text = $"{upgrade.upgradeName} - {upgrade.cost} punti";
            button.onClick.AddListener(() => TryPurchaseUpgrade(upgrade));
        }
    }

    void TryPurchaseUpgrade(Upgrade upgrade)
    {
        if (!upgrade.isUnlocked && UpgradeSystem.Instance.SpendPoints(upgrade.cost))
        {
            upgrade.isUnlocked = true;
            upgrade.ApplyUpgrade();
        }
    }
}
