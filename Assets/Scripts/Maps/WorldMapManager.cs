using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WorldMapManager : MonoBehaviour
{
    [Header("Prefab e Container")]
    [Tooltip("Prefab per rappresentare l'icona di una stanza")]
    public GameObject roomIconPrefab;

    [Tooltip("Transform del contenitore in cui verranno istanziate le icone")]
    public Transform mapContainer;

    [Header("Colori icone")]
    public Color discoveredColor = Color.white;
    public Color undiscoveredColor = Color.gray;
    public Color currentRoomColor = Color.green;

    [Header("Impostazioni della Mappa")]
    [Tooltip("Spaziatura (in pixel) tra le icone della mappa")]
    public float iconSpacing = 50f;

    private GridManager gridManager;
    private Dictionary<Vector2Int, GameObject> roomIcons = new Dictionary<Vector2Int, GameObject>();

    IEnumerator Start()
    {
        gridManager = FindObjectOfType<GridManager>(true);
        if (gridManager == null)
        {
            Debug.LogError("GridManager non trovato nella scena!");
            yield break;
        }

        yield return new WaitUntil(() => gridManager.rooms.Count > 0);

        GenerateMap();
        HideMap(); // La mappa parte nascosta
    }
    public void GenerateMap()
    {
        // Rimuove eventuali icone esistenti
        foreach (Transform child in mapContainer)
        {
            Destroy(child.gameObject);
        }
        roomIcons.Clear();

        foreach (KeyValuePair<Vector2Int, Room> entry in gridManager.rooms)
        {
            Vector2Int pos = entry.Key;
            //Debug.Log("Generazione icona per stanza in posizione: " + pos);

            GameObject icon = Instantiate(roomIconPrefab, mapContainer);
            RectTransform rt = icon.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(pos.x * iconSpacing, pos.y * iconSpacing);
            }
            else
            {
                Debug.LogWarning("L'icona non ha un RectTransform: " + icon.name);
            }
            roomIcons.Add(pos, icon);
        }
        UpdateMap();
    }

    public void UpdateMap()
    {
        foreach (KeyValuePair<Vector2Int, GameObject> entry in roomIcons)
        {
            Vector2Int pos = entry.Key;
            GameObject icon = entry.Value;
            Image image = icon.GetComponent<Image>();

            if (gridManager.rooms.TryGetValue(pos, out Room room))
            {
                image.color = room.isDiscovered ? discoveredColor : undiscoveredColor;
            }
        }
        // Evidenzia la stanza corrente
        if (gridManager.currentRoom != null)
        {
            Vector2Int curPos = gridManager.currentRoom.gridPosition;
            if (roomIcons.ContainsKey(curPos))
            {
                roomIcons[curPos].GetComponent<Image>().color = currentRoomColor;
            }
        }
    }
    public void ShowMap()
    {
        gameObject.SetActive(true);
        UpdateMap();
    }
    public void HideMap()
    {
        gameObject.SetActive(false);
    }
}
