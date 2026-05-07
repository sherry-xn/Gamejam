using UnityEngine;
using Cinemachine;
using System.Collections;

public class CameraRoomManager : MonoBehaviour
{
    [System.Serializable]
    public class RoomBoundary
    {
        public string roomName;
        public PolygonCollider2D boundary;
    }

    public RoomBoundary[] rooms;
    public CinemachineConfiner2D confiner;
    public float transitionDelay = 0.8f;

    private string currentRoom;
    private Coroutine transitionCoroutine;

    public string CurrentRoom => currentRoom;

    public void SwitchRoom(string newRoom)
    {
        if (string.IsNullOrEmpty(newRoom) || newRoom == currentRoom) return;
        if (rooms == null || rooms.Length == 0) return;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(TransitionRoom(newRoom));
    }

    private IEnumerator TransitionRoom(string newRoom)
    {
        var oldBound = GetBoundary(currentRoom);
        var newBound = GetBoundary(newRoom);
        if (newBound == null) yield break;

        // 创建临时合并碰撞体（新旧房间路径）
        var temp = gameObject.AddComponent<PolygonCollider2D>();
        temp.isTrigger = true;
        int idx = 0;
        if (oldBound != null)
            temp.SetPath(idx++, oldBound.GetPath(0));
        temp.SetPath(idx, newBound.GetPath(0));

        // 切换到合并边界
        confiner.m_BoundingShape2D = temp;
        confiner.InvalidateCache();

        // 等待相机平滑过渡
        yield return new WaitForSeconds(transitionDelay);

        // 切换到只有新房间的边界
        confiner.m_BoundingShape2D = newBound;
        confiner.InvalidateCache();
        currentRoom = newRoom;

        // 清理临时碰撞体
        Destroy(temp);
        transitionCoroutine = null;
    }

    private PolygonCollider2D GetBoundary(string roomName)
    {
        if (string.IsNullOrEmpty(roomName) || rooms == null) return null;
        foreach (var r in rooms)
            if (r.roomName == roomName) return r.boundary;
        return null;
    }

    public void SetInitialRoom(string roomName)
    {
        currentRoom = roomName;
        var bound = GetBoundary(roomName);
        if (bound != null && confiner != null)
        {
            confiner.m_BoundingShape2D = bound;
            confiner.InvalidateCache();
        }
    }

    public string FindRoomAtPosition(Vector2 worldPos)
    {
        if (rooms == null) return null;
        foreach (var r in rooms)
        {
            if (r.boundary == null) continue;
            if (r.boundary.OverlapPoint(worldPos))
                return r.roomName;
        }
        return null;
    }
}
