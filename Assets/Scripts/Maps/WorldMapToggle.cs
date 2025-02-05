using UnityEngine;

public class WorldMapToggle : MonoBehaviour
{
    private WorldMapManager worldMapManager;
    private CatInputActions controls;

    void Awake()
    {
        controls = new CatInputActions();
    }

    void Start()
    {
        // Cerca anche gli oggetti inattivi
        worldMapManager = FindObjectOfType<WorldMapManager>(true);
        if (worldMapManager == null)
        {
            Debug.LogError("WorldMapManager non trovato nella scena!");
        }

        controls.Map.Newaction.performed += _ => ToggleMap();
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    private void ToggleMap()
    {
        if (worldMapManager == null) return;
        if (worldMapManager.gameObject.activeSelf)
            worldMapManager.HideMap();
        else
            worldMapManager.ShowMap();
    }
}
