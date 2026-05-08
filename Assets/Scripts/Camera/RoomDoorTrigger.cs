using UnityEngine;

public class RoomDoorTrigger : MonoBehaviour
{
    public CameraRoomManager roomManager;
    public string targetRoom;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (roomManager == null)
        {
            Debug.LogWarning($"[DoorTrigger] {gameObject.name}: roomManager 为空");
            return;
        }
        if (string.IsNullOrEmpty(targetRoom))
        {
            Debug.LogWarning($"[DoorTrigger] {gameObject.name}: targetRoom 为空");
            return;
        }

        Debug.Log($"[DoorTrigger] {gameObject.name}: 切换到 {targetRoom}");
        roomManager.SwitchRoom(targetRoom);
    }
}
