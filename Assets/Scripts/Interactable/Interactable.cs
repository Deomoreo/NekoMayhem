using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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

                if (!isInteracted)
                {
                    isInteracted = true;
                    renderer.material.color = interactedColor;
                } else
                {
                    isInteracted = false;
                    renderer.material.color = originalSphereColor;
                }
            }
        }
        else return;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true; // Player is in range

            if (interactionPopup != null)
            {
                // Show popup
                ManagePopup();
                interactionText.text = interactionMessage;
                
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            if (interactionPopup != null)
            {
                // Disable popup
                ManagePopup();
            }
        }
    }

    // Reverses the state of the popup
    private void ManagePopup()
    {
        if(interactionPopup.activeInHierarchy == true)
        {
            interactionPopup.transform.parent.gameObject.SetActive(false);
            interactionPopup.SetActive(false); 
        } else
        {
            interactionPopup.transform.parent.gameObject.SetActive(true);
            interactionPopup.SetActive(true);
        }
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
