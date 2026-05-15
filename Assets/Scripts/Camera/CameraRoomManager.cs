using UnityEngine;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CameraRoomManager : MonoBehaviour
{
    [System.Serializable]
    public class RoomCamera
    {
        public string roomName;
        public CinemachineVirtualCamera vcam;
    }

    public RoomCamera[] roomCameras;
    public string currentRoom;

    [Header("Startup Camera Control")]
    [SerializeField, Tooltip("Switch to the room camera that contains the player when the scene starts. Leave off when the player should keep using the normal follow camera.")]
    private bool switchToPlayerRoomOnStart = false;
    [SerializeField, Tooltip("Let this manager raise/lower Cinemachine room camera priorities. Leave off when the player should keep using the normal follow camera.")]
    private bool controlRoomCameraPriorities = false;
    [SerializeField, Tooltip("Use only the normal player-follow camera. Door triggers still update room state, hints, and ambient audio, but never switch cameras or camera bounds.")]
    private bool useSingleFollowCameraOnly = false;
    [SerializeField, Tooltip("Disable the main follow camera confiner while using single-camera mode. This is useful as a build-safe fallback when CameraBounds causes jumps.")]
    private bool disableConfinerInSingleFollowMode = false;
    [SerializeField, Tooltip("Use a room-local confiner for selected rooms after a door crossing. This avoids narrow global CameraBounds dead zones without letting RoomBounds detection move the camera early.")]
    private bool constrainMainCameraAfterDoor = true;
    [SerializeField, Tooltip("Room names that should use their RoomBounds on the normal player-follow camera after crossing a door.")]
    private string[] roomsUsingMainCameraRoomBounds = { "Corridor1", "Hall", "Corridor2", "Corridor3", "Classroom", "Toilet", "Dorm", "Guardroom" };

    [Header("Room Camera Auto Setup")]
    [SerializeField, Tooltip("Automatically place configured room cameras at matching RoomBounds centers and clear Follow/LookAt.")]
    private bool autoAlignRoomCamerasToBounds = true;
    [SerializeField, Tooltip("Z position used when auto-aligning room cameras.")]
    private float roomCameraZ = -30f;
    [SerializeField, Min(0f), Tooltip("Damping used by room camera Confiner2D components when auto-aligning room cameras.")]
    private float roomCameraConfinerDamping = 0.5f;

    [Header("Room Key Hint")]
    [SerializeField, Tooltip("Show how many uncollected keys are in a room when the player enters it.")]
    private bool showRoomKeyHint = true;
    [SerializeField]
    private string roomKeyHintFormat = "这个房间里有 {0} 把钥匙";
    [SerializeField, Tooltip("Also detect the player's current room from RoomBounds, so hints still work if a door trigger misses.")]
    private bool detectPlayerRoomForKeyHint = true;
    [SerializeField, Tooltip("Allow RoomBounds polling to retarget the main follow camera confiner. Keep off so camera bounds only change through door triggers.")]
    private bool updateMainCameraConfinerFromRoomPolling = false;
    [SerializeField, Min(0.05f)]
    private float roomKeyHintCheckInterval = 0.15f;

    [Header("环境音映射")]
    [Tooltip("房间名到环境音的映射。未映射的房间不播放环境音。")]
    public RoomAmbientMapping[] ambientMappings;

    [System.Serializable]
    public class RoomAmbientMapping
    {
        public string roomName;
        public AmbientRoom ambient;
    }

    private Dictionary<string, CinemachineVirtualCamera> cameraMap;
    private CinemachineVirtualCamera mainFollowCamera;
    private CinemachineConfiner2D mainFollowCameraConfiner;
    private Collider2D defaultMainCameraBounds;
    private Transform playerTransform;
    private float nextRoomKeyHintCheckTime;

    private void Start()
    {
        cameraMap = BuildCameraMap(roomCameras);
        CachePlayerTransform();
        ConfigureSingleFollowCameraIfNeeded();
        InitializeMainCameraAtPlayer();
        StartCoroutine(InitializeMainCameraAfterSceneStart());

        if (useSingleFollowCameraOnly)
            UpdateCurrentRoomForPlayerOnly();
        else if (switchToPlayerRoomOnStart)
            SwitchToPlayerRoomAtStart();
        else
            UpdateMainCameraConfinerForCurrentPosition();
    }

    private void Update()
    {
        UpdatePlayerRoomForKeyHint();
    }

    private void SwitchToPlayerRoomAtStart()
    {
        CachePlayerTransform();
        if (playerTransform == null)
            return;

        string initRoom = FindRoomAtPosition(playerTransform.position);
        if (!string.IsNullOrEmpty(initRoom))
            SwitchRoom(initRoom);
    }

    public RoomCamera[] GetValidRoomCameras()
    {
        AlignRoomCamerasToBoundsIfNeeded();

        if (roomCameras == null || roomCameras.Length == 0)
        {
            return new RoomCamera[0];
        }

        var validRoomCameras = new List<RoomCamera>();
        foreach (var roomCamera in roomCameras)
        {
            if (roomCamera == null)
                continue;

            if (string.IsNullOrEmpty(roomCamera.roomName))
                continue;

            if (roomCamera.vcam == null)
                continue;

            validRoomCameras.Add(roomCamera);
        }

        return validRoomCameras.ToArray();
    }

    public void AlignRoomCamerasToBoundsIfNeeded()
    {
        if (!autoAlignRoomCamerasToBounds)
            return;

        AlignRoomCamerasToBounds();
    }

    public void AlignRoomCamerasToBounds()
    {
        if (roomCameras == null || roomCameras.Length == 0)
            return;

        var boundsByRoom = BuildRoomBoundsMap();
        if (boundsByRoom.Count == 0)
        {
            return;
        }

        foreach (var roomCamera in roomCameras)
        {
            if (roomCamera == null || roomCamera.vcam == null || string.IsNullOrEmpty(roomCamera.roomName))
                continue;

            if (!boundsByRoom.TryGetValue(roomCamera.roomName, out var boundary) || boundary == null)
            {
                Debug.LogWarning($"[CameraRoomManager] Room camera '{roomCamera.roomName}' has no matching RoomBounds_{roomCamera.roomName}.");
                continue;
            }

            var vcam = roomCamera.vcam;
            Vector2 center = boundary.bounds.center;
            vcam.transform.position = new Vector3(center.x, center.y, roomCameraZ);
            vcam.m_Follow = null;
            vcam.m_LookAt = null;
            vcam.Priority = 0;

            var confiner = vcam.GetComponent<CinemachineConfiner2D>();
            if (confiner == null)
                confiner = vcam.gameObject.AddComponent<CinemachineConfiner2D>();

            confiner.m_BoundingShape2D = boundary;
            confiner.m_Damping = roomCameraConfinerDamping;
            confiner.m_MaxWindowSize = 0;
            confiner.InvalidateCache();
        }
    }

    public void SwitchRoom(string newRoom)
    {
        SwitchRoom(newRoom, true);
    }

    private void SwitchRoom(string newRoom, bool updateCamera)
    {
        if (string.IsNullOrEmpty(newRoom)) return;
        if (cameraMap == null)
            cameraMap = BuildCameraMap(roomCameras);

        bool enteredNewRoom = currentRoom != newRoom;

        if (updateCamera && !useSingleFollowCameraOnly && controlRoomCameraPriorities && cameraMap.ContainsKey(newRoom))
        {
            // 禁用所有房间相机
            foreach (var kvp in cameraMap)
            {
                kvp.Value.Priority = 0;
            }

            // 激活目标房间相机
            cameraMap[newRoom].Priority = 100;
        }

        currentRoom = newRoom;

        // 切换环境音
        SwitchAmbientForRoom(newRoom);

        if (enteredNewRoom)
        {
            ShowRoomKeyHint(newRoom);
        }
    }

    public void SwitchRoomFromDoor(string newRoom)
    {
        SwitchRoom(newRoom, true);
        if (!useSingleFollowCameraOnly)
            UpdateMainCameraConfiner(newRoom);
    }

    public void SetRoomKeyHintEnabled(bool enabled)
    {
        showRoomKeyHint = enabled;
    }

    public void ToggleRoomKeyHint()
    {
        showRoomKeyHint = !showRoomKeyHint;
    }

    private void UpdatePlayerRoomForKeyHint()
    {
        if (!showRoomKeyHint || !detectPlayerRoomForKeyHint)
            return;

        if (Time.time < nextRoomKeyHintCheckTime)
            return;

        nextRoomKeyHintCheckTime = Time.time + Mathf.Max(0.05f, roomKeyHintCheckInterval);

        CachePlayerTransform();
        if (playerTransform == null)
            return;

        string playerRoom = FindRoomAtPosition(playerTransform.position);
        if (string.IsNullOrEmpty(playerRoom) || playerRoom == currentRoom)
            return;

        SwitchRoom(playerRoom, false);
        if (updateMainCameraConfinerFromRoomPolling)
            UpdateMainCameraConfiner(playerRoom);
    }

    private void CachePlayerTransform()
    {
        if (playerTransform != null)
            return;

        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void ShowRoomKeyHint(string roomName)
    {
        if (!showRoomKeyHint || string.IsNullOrEmpty(roomName))
            return;

        CachePlayerTransform();
        if (playerTransform != null)
        {
            string playerRoom = FindRoomAtPosition(playerTransform.position);
            if (!string.IsNullOrEmpty(playerRoom) && playerRoom != roomName)
            {
                return;
            }
        }

        int keyCount = CountKeysInRoom(roomName);
        string message = string.IsNullOrWhiteSpace(roomKeyHintFormat)
            ? keyCount.ToString()
            : string.Format(roomKeyHintFormat, keyCount);

        ScreenHintPanel.Show(message);
    }

    private int CountKeysInRoom(string roomName)
    {
        int count = 0;
        var bags = GetSceneTravelBags();

        foreach (var bag in bags)
        {
            if (bag == null || bag.IsOpened || !bag.HasKey)
                continue;

            string bagRoom = FindRoomAtPosition(bag.transform.position);
            if (bagRoom == roomName)
            {
                count++;
            }
        }

        return count;
    }

    private void SwitchAmbientForRoom(string roomName)
    {
        if (ambientMappings == null) return;

        foreach (var mapping in ambientMappings)
        {
            if (mapping.roomName == roomName)
            {
                AudioManager.Instance.PlayAmbient(mapping.ambient);
                return;
            }
        }
    }

    private void UpdateMainCameraConfinerForCurrentPosition()
    {
        if (useSingleFollowCameraOnly || !constrainMainCameraAfterDoor)
            return;

        CachePlayerTransform();
        if (playerTransform == null)
            return;

        string playerRoom = FindRoomAtPosition(playerTransform.position);
        if (!string.IsNullOrEmpty(playerRoom))
        {
            UpdateMainCameraConfiner(playerRoom, true);
        }
    }

    private void InitializeMainCameraAtPlayer()
    {
        CachePlayerTransform();
        if (playerTransform == null)
            return;

        CacheMainFollowCamera();
        if (mainFollowCamera == null)
            return;

        ConfigureSingleFollowCameraIfNeeded();

        Vector3 cameraPosition = mainFollowCamera.transform.position;
        cameraPosition.x = playerTransform.position.x;
        cameraPosition.y = playerTransform.position.y;
        mainFollowCamera.transform.position = cameraPosition;
        mainFollowCamera.PreviousStateIsValid = false;

        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 mainCameraPosition = mainCamera.transform.position;
            mainCameraPosition.x = playerTransform.position.x;
            mainCameraPosition.y = playerTransform.position.y;
            mainCamera.transform.position = mainCameraPosition;
        }

        if (mainFollowCameraConfiner != null)
            mainFollowCameraConfiner.InvalidateCache();
    }

    private IEnumerator InitializeMainCameraAfterSceneStart()
    {
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            InitializeMainCameraAtPlayer();
        }

        yield return new WaitForEndOfFrame();
        InitializeMainCameraAtPlayer();
    }

    private void UpdateMainCameraConfiner(string roomName)
    {
        UpdateMainCameraConfiner(roomName, false);
    }

    private void UpdateMainCameraConfiner(string roomName, bool forceRoomBounds)
    {
        if (useSingleFollowCameraOnly || !constrainMainCameraAfterDoor)
            return;

        CacheMainFollowCamera();
        if (mainFollowCameraConfiner == null)
            return;

        Collider2D boundary = defaultMainCameraBounds;
        if (forceRoomBounds || ShouldUseRoomBoundsForMainCamera(roomName))
        {
            var boundsByRoom = BuildRoomBoundsMap();
            if (!boundsByRoom.TryGetValue(roomName, out var roomBoundary) || roomBoundary == null)
                return;

            boundary = roomBoundary;
        }

        if (boundary == null || mainFollowCameraConfiner.m_BoundingShape2D == boundary)
            return;

        mainFollowCameraConfiner.m_BoundingShape2D = boundary;
        mainFollowCameraConfiner.InvalidateCache();
        if (mainFollowCamera != null)
            mainFollowCamera.PreviousStateIsValid = false;
    }

    private void CacheMainFollowCamera()
    {
        if (mainFollowCameraConfiner != null)
            return;

        if (mainFollowCamera == null)
            mainFollowCamera = FindMainFollowCamera();

        if (mainFollowCamera == null)
            return;

        mainFollowCameraConfiner = mainFollowCamera.GetComponent<CinemachineConfiner2D>();
        if (mainFollowCameraConfiner == null)
            mainFollowCameraConfiner = mainFollowCamera.gameObject.AddComponent<CinemachineConfiner2D>();

        if (defaultMainCameraBounds == null)
            defaultMainCameraBounds = mainFollowCameraConfiner.m_BoundingShape2D;
    }

    private void ConfigureSingleFollowCameraIfNeeded()
    {
        if (!useSingleFollowCameraOnly)
            return;

        CachePlayerTransform();
        if (mainFollowCamera == null)
            mainFollowCamera = FindMainFollowCamera();

        if (mainFollowCamera != null)
        {
            mainFollowCamera.enabled = true;
            if (playerTransform != null)
            {
                mainFollowCamera.m_Follow = playerTransform;
                mainFollowCamera.m_LookAt = playerTransform;
            }

            mainFollowCamera.Priority = Mathf.Max(mainFollowCamera.Priority, 100);
            mainFollowCamera.PreviousStateIsValid = false;
        }

        var confiner = mainFollowCamera != null ? mainFollowCamera.GetComponent<CinemachineConfiner2D>() : null;
        if (confiner != null)
            confiner.enabled = !disableConfinerInSingleFollowMode;

        if (cameraMap == null)
            cameraMap = BuildCameraMap(roomCameras);

        foreach (var kvp in cameraMap)
        {
            if (kvp.Value != null)
            {
                kvp.Value.Priority = 0;
                kvp.Value.enabled = false;
            }
        }

        var virtualCameras = FindObjectsOfType<CinemachineVirtualCamera>();
        foreach (var vcam in virtualCameras)
        {
            if (vcam == null || vcam == mainFollowCamera)
                continue;

            if (!IsRoomCamera(vcam))
                continue;

            vcam.Priority = 0;
            vcam.enabled = false;
        }
    }

    private void UpdateCurrentRoomForPlayerOnly()
    {
        CachePlayerTransform();
        if (playerTransform == null)
            return;

        string playerRoom = FindRoomAtPosition(playerTransform.position);
        if (!string.IsNullOrEmpty(playerRoom))
            SwitchRoom(playerRoom, false);
    }

    private bool ShouldUseRoomBoundsForMainCamera(string roomName)
    {
        if (roomsUsingMainCameraRoomBounds == null || string.IsNullOrEmpty(roomName))
            return false;

        foreach (var configuredRoom in roomsUsingMainCameraRoomBounds)
        {
            if (configuredRoom == roomName)
                return true;
        }

        return false;
    }

    private CinemachineVirtualCamera FindMainFollowCamera()
    {
        CachePlayerTransform();

        var virtualCameras = FindObjectsOfType<CinemachineVirtualCamera>();
        CinemachineVirtualCamera bestCamera = null;
        int bestPriority = int.MinValue;

        foreach (var vcam in virtualCameras)
        {
            if (vcam == null || IsRoomCamera(vcam))
                continue;

            bool followsPlayer = playerTransform != null &&
                (vcam.m_Follow == playerTransform || vcam.m_LookAt == playerTransform);
            bool isNamedMainCamera = vcam.name == "Virtual Camera";
            if (!followsPlayer && !isNamedMainCamera)
                continue;

            if (vcam.Priority > bestPriority)
            {
                bestPriority = vcam.Priority;
                bestCamera = vcam;
            }
        }

        return bestCamera;
    }

    private bool IsRoomCamera(CinemachineVirtualCamera vcam)
    {
        if (vcam == null)
            return false;

        if (vcam.name.StartsWith("VCam_"))
            return true;

        if (vcam.transform.parent != null && vcam.transform.parent.name == "RoomCameras")
            return true;

        if (roomCameras == null)
            return false;

        foreach (var roomCamera in roomCameras)
        {
            if (roomCamera != null && roomCamera.vcam == vcam)
                return true;
        }

        return false;
    }

    public string FindRoomAtPosition(Vector2 worldPos)
    {
        // 用场景中的 RoomBounds_Xxx 检测，包括失活对象
        var roomBounds = GetSceneRoomBounds();
        foreach (var col in roomBounds)
        {
            if (col.OverlapPoint(worldPos))
            {
                return col.name.Replace("RoomBounds_", "");
            }
        }
        return null;
    }

    public string FindClosestRoomAtPosition(Vector2 worldPos, float maxDistance)
    {
        return FindClosestRoomAtPosition(worldPos, maxDistance, null);
    }

    public string FindClosestRoomAtPosition(Vector2 worldPos, float maxDistance, string excludedRoom)
    {
        if (maxDistance <= 0f)
            return null;

        string closestRoom = null;
        float closestDistanceSqr = maxDistance * maxDistance;
        var roomBounds = GetSceneRoomBounds();

        foreach (var col in roomBounds)
        {
            if (col == null)
                continue;

            string roomName = col.name.Replace("RoomBounds_", "");
            if (!string.IsNullOrEmpty(excludedRoom) && roomName == excludedRoom)
                continue;

            Vector2 closestPoint = col.ClosestPoint(worldPos);
            float distanceSqr = (worldPos - closestPoint).sqrMagnitude;
            if (distanceSqr > closestDistanceSqr)
                continue;

            closestDistanceSqr = distanceSqr;
            closestRoom = roomName;
        }

        return closestRoom;
    }

    private Dictionary<string, CinemachineVirtualCamera> BuildCameraMap(RoomCamera[] cameras)
    {
        var map = new Dictionary<string, CinemachineVirtualCamera>();
        if (cameras == null)
            return map;

        foreach (var roomCamera in cameras)
        {
            if (roomCamera == null)
                continue;

            if (string.IsNullOrEmpty(roomCamera.roomName))
                continue;

            if (roomCamera.vcam == null)
                continue;

            map[roomCamera.roomName] = roomCamera.vcam;
        }

        return map;
    }

    private static Dictionary<string, PolygonCollider2D> BuildRoomBoundsMap()
    {
        var map = new Dictionary<string, PolygonCollider2D>();
        var roomBounds = GetSceneRoomBounds();
        foreach (var col in roomBounds)
        {
            if (col == null)
                continue;

            string roomName = col.name.Replace("RoomBounds_", "");
            map[roomName] = col;
        }

        return map;
    }

    private static PolygonCollider2D[] GetSceneRoomBounds()
    {
        var roomBounds = new List<PolygonCollider2D>();

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            var scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
                continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;

                var colliders = root.GetComponentsInChildren<PolygonCollider2D>(true);
                foreach (var col in colliders)
                {
                    if (col == null)
                        continue;

                    if (!col.name.StartsWith("RoomBounds_"))
                        continue;
                    roomBounds.Add(col);
                }
            }
        }

        return roomBounds.ToArray();
    }

    private static TravelBag[] GetSceneTravelBags()
    {
        var bags = new List<TravelBag>();

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            var scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;

                bags.AddRange(root.GetComponentsInChildren<TravelBag>(true));
            }
        }

        return bags.ToArray();
    }
}
