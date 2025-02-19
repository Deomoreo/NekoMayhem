using UnityEngine;
using UnityEngine.EventSystems;

public class CursorManager : MonoBehaviour
{
    private bool isCursorLocked = true;

    public CursorManager(bool isCursorLocked)
    {
        this.isCursorLocked = isCursorLocked;
    }

    private void Start()
    {
        HideCursor(); 
    }

    private void Update()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            ShowCursor();
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            ShowCursor();
        }

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
