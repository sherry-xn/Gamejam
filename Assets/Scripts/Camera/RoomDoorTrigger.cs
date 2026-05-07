using UnityEngine;

public class RoomDoorTrigger : MonoBehaviour
{
    public CameraRoomManager roomManager;
    public string targetRoom;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (roomManager == null || string.IsNullOrEmpty(targetRoom)) return;

        roomManager.SwitchRoom(targetRoom);
    }
}
