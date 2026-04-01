using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    [System.Serializable]
    public class Room
    {
        public string roomName;
        public GameObject roomObject;
        public Transform playerSpawnPoint;
        public Transform[] enemySpawnPoints;
        public GameObject[] enemyPrefabs;
        public GameObject exitDoor;
    }

    [SerializeField] private Room[] rooms;
    private int currentRoomIndex = -1;
    private List<EnemyBase> aliveEnemies = new List<EnemyBase>();

    private void Start()
    {
        foreach (var room in rooms)
        {
            room.roomObject.SetActive(false);
            if (room.exitDoor != null)
            {
                room.exitDoor.SetActive(false);
            }
        }

        ActivateRoom(0);
    }

    public void ActivateRoom(int index)
    {
        if (index >= rooms.Length)
        {
            GameManager.Instance.OnAllRoomsCleared();
            return;
        }

        currentRoomIndex = index;
        Room room = rooms[currentRoomIndex];

        room.roomObject.SetActive(true);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = room.playerSpawnPoint.position;
        cc.enabled = true;

        SpawnEnemies(room);
    }

    private void SpawnEnemies(Room room)
    {
        aliveEnemies.Clear();

        for (int i = 0; i < room.enemySpawnPoints.Length; i++)
        {
            GameObject prefab = room.enemyPrefabs[i % room.enemyPrefabs.Length];
            Transform spawnPoint = room.enemySpawnPoints[i];

            GameObject enemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
            aliveEnemies.Add(enemyBase);
        }
    }

    private void Update()
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= rooms.Length) return;

        aliveEnemies.RemoveAll(e => e == null || e.IsDead);

        if (aliveEnemies.Count == 0 && rooms[currentRoomIndex].exitDoor != null)
        {
            rooms[currentRoomIndex].exitDoor.SetActive(true);
        }
    }

    public void OnPlayerEnterNextRoom()
    {
        if (currentRoomIndex >= 0 && currentRoomIndex < rooms.Length)
        {
            rooms[currentRoomIndex].roomObject.SetActive(false);
            rooms[currentRoomIndex].exitDoor.SetActive(false);
        }

        ActivateRoom(currentRoomIndex + 1);
    }
}
