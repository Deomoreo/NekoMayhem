using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    CatInputActions controls;

    public MessagePanel messagePanel;
    public SaveNotification saveNotification;
    private float delayTime = 2f; 

    // Start is called before the first frame update
    void Start()
    {
       string saveFilePath = Application.persistentDataPath + "/savefile.json";

        if (File.Exists(saveFilePath))
        {
            SaveSystem.LoadGame();
        } else
        {
            if (messagePanel != null)
            {
                StartCoroutine(ShowMessageAfterDelay());
            }
        }

       

       controls = new CatInputActions();
       controls.Enable();
       controls.SaveGame.Newaction.performed += _ => SaveGameData();
    }


    public void SaveGameData()
    {
        Transform catTransform = GameObject.FindObjectOfType<CatController>().transform;
        float playerHealth = catTransform.GetComponent<CatHealth>().GetCurrentHealth();
        SaveSystem.SaveGame(catTransform, playerHealth, saveNotification);
    }

    private IEnumerator ShowMessageAfterDelay()
    {
        yield return new WaitForSeconds(delayTime);
        messagePanel.ShowMessage("Welcome to the summoners' rift. Sugoi!!!");
    }
    
}
