using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public InventorySystem inventorySystem; 
    public GameObject slotPrefab;
    public Transform inventoryPanel;

    private bool isInventoryOpen = false;

    void Start()
    {
        // closed by default
        inventoryPanel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        inventoryPanel.gameObject.SetActive(isInventoryOpen); // Show/hide inventory

        // if it's open, update the UI
        if (isInventoryOpen)
        {
            UpdateUI();
        }
    }


    // Refresh inventory UI
    public void UpdateUI()
    {
    
        foreach (Transform child in inventoryPanel)
        {
            Destroy(child.gameObject);
        }

        // New slot for each item in inventory
        foreach (InventoryItem item in inventorySystem.inventory)
        {
            GameObject slot = Instantiate(slotPrefab, inventoryPanel);
            slot.GetComponent<Image>().sprite = item.icon; 
            slot.transform.GetChild(0).GetComponent<Text>().text = item.quantity.ToString(); 
        }
    }
}