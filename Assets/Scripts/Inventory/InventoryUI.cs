using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public InventorySystem inventorySystem; 
    public GameObject slotPrefab;
    public GameObject inventoryDescriptionPanel;
    public Transform inventoryPanel;
    public Text inventoryDescriptionText;
    private bool isInventoryOpen = false;
    private CatInputActions controls;

    void Start()
    {
        // both closed by default
        inventoryPanel.gameObject.SetActive(false);
        inventoryDescriptionPanel.SetActive(false);
    }

    void Awake()
    {
        // initialize input actions
        controls = new CatInputActions();
        controls.ToggleInventory.Newaction.performed += _ => ToggleInventory();
    }

    void OnEnable()
    {
        // Enable input actions
        controls.Enable();
        
    }

    void OnDisable()
    {
        // Disable the Input Actions
        controls.Disable();
    }

    private void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        inventoryPanel.gameObject.SetActive(isInventoryOpen); // Show/hide the inventory

        if (isInventoryOpen)
        {
            UpdateUI(); // If open, refresh inventory UI
        } else
        {
            inventoryDescriptionPanel.SetActive(false); // Force-hide description panel when inventory is closed
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
            Image itemImage = slot.GetComponent<Image>();
            slot.transform.GetChild(0).GetComponent<Text>().text = "";

            if (item.icon != null) 
            {
                itemImage.sprite = item.icon;  
            } else
            {
                itemImage.color = itemImage.color = new Color(1, 1, 1, 0);  // Hide if no sprite
            }

            // manage hover description functionality
            InventorySlotHover inventorySlotHover = slot.AddComponent<InventorySlotHover>();
            inventorySlotHover.inventoryDescriptionPanel = inventoryDescriptionPanel;
            inventorySlotHover.inventoryDescriptionText = inventoryDescriptionText;
            inventorySlotHover.SetItem(item);
        }
    }
}