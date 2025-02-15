using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string savePath = Application.persistentDataPath + "/savefile.json";

    // Save the game to a file
    public static void SaveGame(Transform playerTransform, float health, SaveNotification saveNotification)
    {
        SaveData saveData = new SaveData
        {
            playerPositionX = playerTransform.position.x,
            playerPositionY = playerTransform.position.y,
            playerPositionZ = playerTransform.position.z,
            playerHP = health
        };

        // Get the current room from GridManager
        GridManager gridManager = GameObject.FindObjectOfType<GridManager>();
        if (gridManager != null && gridManager.currentRoom != null)
        {
            saveData.roomPositionX = gridManager.currentRoom.gridPosition.x;
            saveData.roomPositionY = gridManager.currentRoom.gridPosition.y;
        }

        // Manage upgrade system state
        UpgradeSystem upgradeSystem = UpgradeSystem.Instance;
        if (upgradeSystem != null)
        {
            saveData.upgradePoints = upgradeSystem.upgradePoints;
            foreach(var upgrade in upgradeSystem.upgrades)
            {
                UpgradeData upgradeData = new UpgradeData(upgrade.upgradeName, upgrade.isUnlocked);
                saveData.upgrades.Add(upgradeData);
            }
        }

        // Manage interactable objects states
        Interactable[] interactables = GameObject.FindObjectsOfType<Interactable>();
        foreach (Interactable interactableObject in interactables)
        {
            InteractableData interactableData = new InteractableData
            (
                interactableObject.id,
                interactableObject.isInteracted,
                new ColorData(interactableObject.GetComponent<Renderer>().material.color)
            );
            saveData.interactableObjects.Add(interactableData);
        }

        string json = JsonUtility.ToJson(saveData, true); 
        File.WriteAllText(savePath, json);

        //Display save notification
        if (saveNotification != null)
        {
            saveNotification.ShowMessage("Game Saved Successfully");
        }
    }

    // Load the game from file
    public static SaveData LoadGame()
    {

        if (File.Exists(savePath))
        { 
            string json = File.ReadAllText(savePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // Activate the correct room before placing player character
            GridManager gridManager = GameObject.FindObjectOfType<GridManager>();
            if (gridManager != null)
            {
                Vector2Int savedRoomPosition = new Vector2Int(data.roomPositionX, data.roomPositionY);

                // Disable all rooms first
                foreach (Room room in gridManager.rooms.Values)
                {
                    room.gameObject.SetActive(false);
                }

                // Activate the saved room and set it as the current room
                if (gridManager.rooms.TryGetValue(savedRoomPosition, out Room savedRoom))
                {
                    savedRoom.gameObject.SetActive(true);
                    gridManager.currentRoom = savedRoom;
                }
                else
                {
                    Debug.LogError("Saved room position does not exist in GridManager!");
                    return new SaveData(); // Prevent loading if the room is invalid
                }
            }


            // Set the player data with the data loaded from the save file
            CatController player = GameObject.FindObjectOfType<CatController>();
            if (player != null)
            {
                player.transform.position = new Vector3(data.playerPositionX, data.playerPositionY, data.playerPositionZ);
                CatHealth playerHealth = player.GetComponent<CatHealth>();
                playerHealth.SetHealth(data.playerHP);
            }

            // Manage load upgrade system
            UpgradeSystem upgradeSystem = UpgradeSystem.Instance;
            if (upgradeSystem != null)
            {
                upgradeSystem.upgradePoints = data.upgradePoints;
                // find each upgrade and load its state
                foreach (var upgrade in upgradeSystem.upgrades)
                {
                    UpgradeData upgradeData = data.upgrades.Find(u => u.upgradeName == upgrade.upgradeName);
                    if(upgradeData != null)
                    {
                        upgrade.isUnlocked = upgradeData.isUnlocked;
                    }
                }
            }

            // Create a dictionary for fast lookup.
            Dictionary<string, InteractableData> interactableLookup = new Dictionary<string, InteractableData>();
            foreach (InteractableData interactableData in data.interactableObjects)
            {
                interactableLookup[interactableData.id] = interactableData;
            }

            // Set interactable object states with the data loaded from the save file
            Interactable[] interactables = GameObject.FindObjectsOfType<Interactable>();
            foreach (Interactable interactableObject in interactables)
            {

                if(interactableLookup.TryGetValue(interactableObject.id, out InteractableData interactableData))
                {
                    interactableObject.ApplySavedData(interactableData);
                }           
            }
            return data;
        }
        else
        {
            return new SaveData(); // return default data if there is no save file
        }
    }
}
