using UnityEngine;

public class RoomTrigger : MonoBehaviour
{
    private RoomManager roomManager;

    private void Start()
    {
        roomManager = FindObjectOfType<RoomManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            roomManager.OnPlayerEnterNextRoom();
        }
    }
}
