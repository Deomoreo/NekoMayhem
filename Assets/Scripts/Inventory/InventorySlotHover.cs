using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject inventoryDescriptionPanel; 
    public Text inventoryDescriptionText; 

    private InventoryItem item; 

    public void SetItem(InventoryItem newItem)
    {
        item = newItem;
    }

    // Show description panel on hover
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item != null)
        {
            // Set description text
            inventoryDescriptionText.text = item.description;

            // Show the description panel
            inventoryDescriptionPanel.SetActive(true);

            // Position near cursor
            Vector2 mousePosition = eventData.position;
            Vector2 panelPosition = inventoryDescriptionPanel.transform.position = mousePosition + new Vector2(20, -20);

            // Make sure panel is within the screen
            RectTransform panelRect = inventoryDescriptionPanel.GetComponent<RectTransform>();
            Vector2 clampedPosition = new Vector2(
                Mathf.Clamp(panelPosition.x, 0, Screen.width - panelRect.rect.width),
                Mathf.Clamp(panelPosition.y, panelRect.rect.height, Screen.height)
            );
        }
    }

    // Hide the description panel on exit
    public void OnPointerExit(PointerEventData eventData)
    {
        inventoryDescriptionPanel.SetActive(false);
    }
}