using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public float cellSize = 5f;
    public Room startingRoom;
    private Dictionary<Vector2Int, Room> rooms = new Dictionary<Vector2Int, Room>();
    private Room currentRoom;
    public bool isTransitioning = false;

    void Start()
    {
        InitializeRooms();
        SetActiveRoom(startingRoom);
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

    public void SetActiveRoom(Room newRoom)
    {
        if (isTransitioning) return; // Evita transizioni multiple
        StartCoroutine(TransitionRooms(newRoom));
    }

    IEnumerator TransitionRooms(Room newRoom)
    {
        isTransitioning = true;
        if (currentRoom != null)
        {
            currentRoom.FadeOut();
            yield return new WaitForSeconds(0.5f);
            currentRoom.gameObject.SetActive(false);
        }
        newRoom.gameObject.SetActive(true);
        newRoom.DiscoverRoom();
        newRoom.FadeIn();
        currentRoom = newRoom;
        yield return new WaitForSeconds(0.5f);
        isTransitioning = false;
    }

    public void TryMoveToRoom(Vector2Int direction)
    {
        if (isTransitioning) return; // Evita cambi di stanza multipli durante la transizione
        Vector2Int newRoomPos = currentRoom.gridPosition + direction;
        if (rooms.ContainsKey(newRoomPos))
        {
            Room nextRoom = rooms[newRoomPos];
            if (!nextRoom.gameObject.activeSelf)
            {
                nextRoom.gameObject.SetActive(true);
            }
            SetActiveRoom(nextRoom);
        }
    }
}

