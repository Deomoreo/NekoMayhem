using UnityEngine;
using UnityEngine.UI;

public class Interactable : MonoBehaviour
{
    // ID = game object name
    public string id { get; private set; }
    public string interactionMessage = "Press F to interact"; // Customizable message
    public GameObject interactionPopup; // Reference to UI popup
    private Text interactionText; // Reference UI text
    private Renderer renderer;
    private CatInputActions controls;
    private Color originalSphereColor;
    private Color interactedColor = Color.red;
    private bool isPlayerNear = false; // Track if player is in range
    public bool isInteracted = false; // Track interaction state
    public SaveNotification saveNotification;

    void Awake()
    {
        // initialize input actions
        controls = new CatInputActions();
        controls.Interact.Newaction.performed += _ => Interact();

    }

    private void OnEnable()
    {
        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Disable();
    }

    void Start()
    { 

        // Assign the GameObject's name as persisent ID
        id = gameObject.name;

        // Find the text component inside the panel
        interactionText = interactionPopup.GetComponentInChildren<Text>();
        interactionPopup.SetActive(false); // Hide by default

        // Find the 3D object
        renderer = GetComponent<Renderer>();
        originalSphereColor = renderer.material.color;
    }

    private void Interact()
    {
        if (isPlayerNear)
        {
            if (interactionPopup != null)
            {
                // Save game and display message -> first time the checkpoint unlocked popup is shown
                SaveCheckpoint();
                HidePopup();
                renderer.material.color = interactedColor;              
            }
        }
        else return;
    }

    private void SaveCheckpoint()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 savePosition = transform.position + (player.transform.forward * 2f);
            SaveSystem.SaveGame(player.transform, player.GetComponent<CatHealth>().GetCurrentHealth(), saveNotification);
            Debug.Log("game saved at checkpoint");

            // Show unlock message
            if (!isInteracted)
            {
                MessagePanel messagePanel = FindObjectOfType<MessagePanel>();
                messagePanel.ShowMessage("Checkpoint Unlocked");
                isInteracted = true;
            }

        } else
        {
            Debug.LogWarning("Save checkpoint: player not found");
        }
    }

    void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Player"))
        {
            isPlayerNear = true; // Player is in range
            ShowPopup();
            if (interactionText != null)
            {
                interactionText.text = interactionMessage;

            }
        }
    }

    void OnTriggerExit(Collider other)
    {

        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            HidePopup();
        }
    }

    private void ShowPopup()
    {
        if(interactionPopup != null)
        {
            interactionPopup.transform.parent.gameObject.SetActive(true);
            interactionPopup.SetActive(true);
        }
    }

    private void HidePopup()
    {
        interactionPopup.transform.parent.gameObject.SetActive(false);
        interactionPopup.SetActive(false);
    }

    // Method to apply the saved state and color after loading the game
    public void ApplySavedData(InteractableData data)
    {
        if (data != null)
        {
            // Apply interaction state
            isInteracted = data.isInteracted;
            // Apply the saved color
            renderer.material.color = data.colorData.ToColor();
        }
    }
}
