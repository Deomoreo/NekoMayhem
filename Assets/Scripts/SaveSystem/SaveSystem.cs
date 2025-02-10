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

            // Set the player data with the data loaded from the save file
            CatController player = GameObject.FindObjectOfType<CatController>();
            if (player != null)
            {
                player.transform.position = new Vector3(data.playerPositionX, data.playerPositionY, data.playerPositionZ);
                CatHealth playerHealth = player.GetComponent<CatHealth>();
                playerHealth.SetHealth(data.playerHP);
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
