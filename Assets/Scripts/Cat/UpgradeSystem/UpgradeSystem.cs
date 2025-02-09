using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UpgradeSystem : MonoBehaviour
{
    public static UpgradeSystem Instance;
    public int upgradePoints = 0;
    public Text pointsText; // UI per mostrare i punti
    public GameObject upgradeShopPanel; // Pannello dello shop
    public Transform upgradeList; // Lista degli upgrade
    public GameObject upgradeButtonPrefab; // Prefab dei bottoni upgrade
    public Button closeShopButton; // Bottone per chiudere lo shop

    private bool playerInZone = false;

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

    public List<Upgrade> upgrades = new List<Upgrade>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        UpdateUI();
        GenerateUpgradeButtons();
        if (closeShopButton != null)
        {
            closeShopButton.onClick.AddListener(CloseShop);
        }
    }

    public void AddPoints(int amount)
    {
        upgradePoints += amount;
        Debug.Log("Punti upgrade attuali: " + upgradePoints);
        UpdateUI();
    }

    public bool SpendPoints(int cost)
    {
        if (upgradePoints >= cost)
        {
            upgradePoints -= cost;
            Debug.Log("Punti spesi. Punti rimanenti: " + upgradePoints);
            UpdateUI();
            return true;
        }
        Debug.Log("Non hai abbastanza punti per questo upgrade!");
        return false;
    }

    private void UpdateUI()
    {
        if (pointsText != null)
            pointsText.text = "Upgrade Points: " + upgradePoints;
    }

    private void GenerateUpgradeButtons()
    {
        foreach (Upgrade upgrade in upgrades)
        {
            GameObject buttonObj = Instantiate(upgradeButtonPrefab, upgradeList);
            Button button = buttonObj.GetComponent<Button>();
            Text buttonText = buttonObj.GetComponentInChildren<Text>();

            buttonText.text = $"{upgrade.upgradeName} - {upgrade.cost} punti";
            button.onClick.AddListener(() => TryPurchaseUpgrade(upgrade));
        }
    }

    private void TryPurchaseUpgrade(Upgrade upgrade)
    {
        if (!upgrade.isUnlocked && SpendPoints(upgrade.cost))
        {
            upgrade.isUnlocked = true;
            upgrade.ApplyUpgrade();
        }
    }

    public void OpenShop()
    {
        if (playerInZone)
        {
            upgradeShopPanel.SetActive(true);
        }
    }

    public void CloseShop()
    {
        upgradeShopPanel.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = true;
            OpenShop();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;
            CloseShop();
        }
    }
}
