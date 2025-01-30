using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject inventoryDescriptionPanel; // Reference to the description panel
    public Text inventoryDescriptionText; // Reference to the description text
    private InventoryItem item; // The item to display

    // Method to set the item for this slot
    public void SetItem(InventoryItem newItem)
    {
        item = newItem;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item != null)
        {
            // Set the description text
            inventoryDescriptionText.text = item.description;

            // Show the description panel
            inventoryDescriptionPanel.SetActive(true);

            // Get the RectTransform of the hovered inventory slot (this game object)
            RectTransform slotRectTransform = GetComponent<RectTransform>();

            // Get the position of the inventory slot in world space
            Vector3 slotWorldPosition = slotRectTransform.position;

            // Adjust the position to be above the slot (add vertical offset)
            Vector3 tooltipPosition = slotWorldPosition + new Vector3(0, slotRectTransform.rect.height / 2 + 50, 0);

            // Convert world position to local position in the Canvas (use Canvas's RectTransform)
            RectTransform canvasRectTransform = inventoryDescriptionPanel.transform.root.GetComponent<RectTransform>();
            Vector2 localPosition = canvasRectTransform.InverseTransformPoint(tooltipPosition);

            // Set the position of the description panel
            inventoryDescriptionPanel.GetComponent<RectTransform>().anchoredPosition = localPosition;

            // Force update the layout to resize the panel
            LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryDescriptionPanel.GetComponent<RectTransform>());
        }
    }

    // This method is called when the pointer exits the inventory slot
    public void OnPointerExit(PointerEventData eventData)
    {
        // Hide the description panel
        inventoryDescriptionPanel.SetActive(false);
    }
}
