using System.Collections.Generic;
using UnityEngine;

public class InventorySystem : MonoBehaviour
{
    public List<InventoryItem> inventory = new List<InventoryItem>();

    void Start()
    {
        InventoryItem testItem = new InventoryItem
        {
            id = "1",
            name = "Bamba",
            icon = Resources.Load<Sprite>("InventoryItems/solt10moon"),
            quantity = 1,
            description = "A vista d'occhio sembra essere bamba di bassissima qualità."
        };

        InventoryItem testItem2 = new InventoryItem
        {
            id = "2",
            name = "Metanfetamina blu",
            icon = Resources.Load<Sprite>("InventoryItems/solt10-kold"),
            quantity = 2,
            description = "La metanfetamina più pura del pianeta. Porta il marchio di Heisenberg."
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

    //TODO REMOVE
    public void DisplayInventory()
    {
    }
}