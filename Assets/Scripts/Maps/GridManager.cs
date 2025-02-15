using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Tooltip("Stanza iniziale")]
    public Room startingRoom;

    [Tooltip("Dimensione della cella")]
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
            currentRoom.DiscoverRoom();
            //Debug.Log("Stanza di partenza impostata a: " + currentRoom.gridPosition);
        }
        else
        {
            Debug.LogError("StartingRoom non è stato assegnato!");
        }
    }

    void InitializeRooms()
    {
        Room[] allRooms = FindObjectsOfType<Room>(true);
        foreach (Room room in allRooms)
        {
            if (!rooms.ContainsKey(room.gridPosition))
            {
                rooms.Add(room.gridPosition, room);
                room.gameObject.SetActive(false);
                //Debug.Log("Trovata stanza in posizione: " + room.gridPosition);
            }
            else
            {
                Debug.LogWarning("Posizione duplicata per la stanza: " + room.gridPosition);
            }
        }
    }

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
            currentRoom.DiscoverRoom();
        }
        else
        {
            Debug.LogWarning("Nessuna stanza trovata in posizione: " + targetPos);
        }
    }
}
