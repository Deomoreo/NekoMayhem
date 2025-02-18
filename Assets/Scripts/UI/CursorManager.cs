using UnityEngine;
using UnityEngine.EventSystems;

public class CursorManager : MonoBehaviour
{
    private bool isCursorLocked = true;

    private void Start()
    {
        HideCursor(); // 🔥 Nascondiamo il cursore all'avvio
    }

    private void Update()
    {
        // 🔥 Se clicchiamo su un elemento UI, mostriamo il cursore
        if (EventSystem.current.IsPointerOverGameObject())
        {
            ShowCursor();
        }

        // 🔥 Se premiamo ESC, il cursore appare
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            ShowCursor();
        }

        // 🔥 Se premiamo di nuovo per tornare al gioco, nascondiamo il cursore
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
        {
            HideCursor();
        }
    }

    public void HideCursor()
    {
        isCursorLocked = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ShowCursor()
    {
        isCursorLocked = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
