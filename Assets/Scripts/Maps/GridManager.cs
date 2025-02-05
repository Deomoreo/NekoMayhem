using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Tooltip("Stanza iniziale (deve avere gridPosition impostato, ad esempio (0,0))")]
    public Room startingRoom;

    [Tooltip("Dimensione della cella. Per semplicità, impostata a 1 (1 unità per cella).")]
    public float cellSize = 1f;

    public Dictionary<Vector2Int, Room> rooms = new Dictionary<Vector2Int, Room>();
    public Room currentRoom;

    void Start()
    {
        InitializeRooms();

        if (startingRoom != null)
        {
            startingRoom.gameObject.SetActive(true);
            currentRoom = startingRoom;
            Debug.Log("Stanza di partenza impostata a: " + currentRoom.gridPosition);
        }
        else
        {
            Debug.LogError("StartingRoom non è stato assegnato!");
        }
    }

    void InitializeRooms()
    {
        Room[] allRooms = FindObjectsOfType<Room>();

        foreach (Room room in allRooms)
        {
            if (!rooms.ContainsKey(room.gridPosition))
            {
                rooms.Add(room.gridPosition, room);
                room.gameObject.SetActive(false);
                Debug.Log("Trovata stanza in posizione: " + room.gridPosition);
            }
            else
            {
                Debug.LogWarning("Posizione duplicata per la stanza: " + room.gridPosition);
            }
        }
    }

    /// <summary>
    /// Esegue una transizione istantanea: disattiva la stanza corrente e attiva quella target.
    /// Il vettore moveDirection deve essere:
    /// - (0, 1) per salire (sopra, lungo Z positivo)
    /// - (1, 0) per andare a destra (lungo X positivo)
    /// - (0, -1) per scendere (sotto, lungo Z negativo)
    /// - (-1, 0) per andare a sinistra (lungo X negativo)
    /// </summary>
    /// <param name="moveDirection"></param>
    public void InstantTransitionRoom(Vector2Int moveDirection)
    {
        if (currentRoom == null)
        {
            Debug.LogError("Nessuna stanza corrente impostata!");
            return;
        }

        // Calcola la nuova posizione in griglia
        Vector2Int targetPos = currentRoom.gridPosition + moveDirection;
        Debug.Log("Transizione da " + currentRoom.gridPosition + " a " + targetPos);

        if (rooms.TryGetValue(targetPos, out Room targetRoom))
        {
            currentRoom.gameObject.SetActive(false);
            targetRoom.gameObject.SetActive(true);
            currentRoom = targetRoom;
        }
        else
        {
            Debug.LogWarning("Nessuna stanza trovata in posizione: " + targetPos);
        }
    }
}
