using System.Collections.Generic;
using UnityEngine;

public class InventorySystem : MonoBehaviour
{
    public List<InventoryItem> inventory = new List<InventoryItem>();

    void Start()
    {
        InventoryItem testItem = new InventoryItem
        {
            id = "testItem1",
            name = "test item 1",
            icon = Resources.Load<Sprite>("Square"),
            quantity = 1,
            description = "Test item"
        };

        InventoryItem testItem2 = new InventoryItem
        {
            id = "testItem2",
            name = "test item 2",
            icon = Resources.Load<Sprite>("Circle"),
            quantity = 2,
            description = "Test item 2"
        };

        AddItem(testItem);
        AddItem(testItem2);

        DisplayInventory();
    }

    public void AddItem(InventoryItem newItem)
    {
        InventoryItem existingItem = inventory.Find(item => item.id == newItem.id);
        // if exists -> increase qty else add item
        if (existingItem != null)
        {
            existingItem.quantity += newItem.quantity; 
        }
        else
        {
            inventory.Add(newItem);
        }
    }

    public void RemoveItem(string itemId, int quantity)
    {
        InventoryItem itemToRemove = inventory.Find(item => item.id == itemId);
        if (itemToRemove != null)
        {
            itemToRemove.quantity -= quantity;

            //remove if zero or less qty
            if (itemToRemove.quantity <= 0)
            {
                inventory.Remove(itemToRemove); 
            }
        }
    }

    public void DisplayInventory()
    {
        foreach (InventoryItem item in inventory)
        {
            //TODO remove
            Debug.Log($"Item: {item.name}, Quantity: {item.quantity}, Description: {item.description}");
        }
    }
}