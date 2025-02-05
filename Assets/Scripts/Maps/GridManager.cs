using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public float cellSize = 5f;
    public Room startingRoom;
    public Dictionary<Vector2Int, Room> rooms = new Dictionary<Vector2Int, Room>();
    public Room currentRoom;
    public bool isTransitioning = false;

    void Start()
    {
        InitializeRooms();

        if (startingRoom != null)
        {
            startingRoom.gameObject.SetActive(true);
            currentRoom = startingRoom;
        }
    }

    void InitializeRooms()
    {
        Room[] allRooms = FindObjectsOfType<Room>();
        foreach (Room room in allRooms)
        {
            rooms.Add(room.gridPosition, room);
            room.gameObject.SetActive(false);
        }
    }

    // Transizione istantanea tra stanze senza eseguire fade locali (usata in combinazione con il fade globale)
    public void InstantTransitionRoom(Vector2Int direction)
    {
        if (isTransitioning)
            return;
        isTransitioning = true;

        Vector2Int newRoomPos = currentRoom.gridPosition + direction;
        if (rooms.TryGetValue(newRoomPos, out Room newRoom))
        {
            if (currentRoom != null)
                currentRoom.gameObject.SetActive(false);

            newRoom.gameObject.SetActive(true);
            currentRoom = newRoom;
        }
        isTransitioning = false;
    }
}
