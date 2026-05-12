using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Monitor terminal controller. Press R to toggle monitor mode and Z/X to cycle rooms.
/// Uses CameraRoomManager room bindings first, then falls back to RoomBounds_* data.
/// </summary>
public class MonitorController : MonoBehaviour
{
    public static MonitorController Instance { get; private set; }

    public enum MonitorMode
    {
        Camera,
        Image
    }

    [System.Serializable]
    private class MonitorImageFeed
    {
        public string roomName;
        public Sprite image;
        public string imagePath;
        public bool enabled = true;
    }

    [Header("Monitor Settings")]
    [SerializeField] private float cameraHeight = 30f;
    [SerializeField] private float cameraOrthoSize = 12f;
    [SerializeField] private float cooldownDuration = 5f;
    [SerializeField] private float signalLostChance = 0.1f;
    [SerializeField] private float signalRecoverTime = 3f;
    [SerializeField] private MonitorMode currentMode = MonitorMode.Camera;

    [Header("Image Feeds")]
    [SerializeField] private bool preferImageFeeds = true;
    [SerializeField] private MonitorImageFeed[] imageFeeds = new MonitorImageFeed[0];

    [Header("UI")]
    [SerializeField] private MonitorCameraUI monitorUIPrefab;
    private MonitorCameraUI monitorUIInstance;

    private struct MonitorView
    {
        public string roomName;
        public Vector3 position;
        public Quaternion rotation;
        public bool orthographic;
        public float orthographicSize;
        public float fieldOfView;
        public Sprite image;
        public bool usesImage;
    }

    private readonly List<MonitorView> monitorViews = new List<MonitorView>();
    private int currentCameraIndex;
    private bool isMonitorOpen;
    private bool isOnCooldown;
    private bool isSignalLost;
    private float lastCloseTime = -999f;
    private string savedRoom;
    private PlayerController playerRef;
    private MonsterController monsterRef;
    private Camera mainCamera;
    private Camera monitorCamera;
    private GameObject monitorCameraObject;

    public bool IsMonitorOpen => isMonitorOpen;
    public MonitorMode CurrentMode => currentMode;
    public int CurrentCameraIndex => currentCameraIndex;
    public int CurrentCameraCount => monitorViews.Count;
    public string CurrentCameraRoomName => GetCameraRoomName(currentCameraIndex);
    public bool IsOnCooldown => isOnCooldown;
    public float CooldownDuration => cooldownDuration;
    public float CooldownRemaining => isOnCooldown ? Mathf.Max(0f, cooldownDuration - (Time.unscaledTime - lastCloseTime)) : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        CleanupUI();
        CleanupMonitorCamera();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.R))
            return;

        ToggleMonitor();
    }

    public void ToggleMonitor()
    {
        if (isMonitorOpen)
            CloseMonitor();
        else
            OpenMonitor();
    }

    private void OpenMonitor()
    {
        if (isOnCooldown)
        {
            return;
        }

        BuildViews();
        if (monitorViews.Count == 0)
        {
            Debug.LogWarning("[MonitorController] No monitor rooms available.");
            return;
        }

        mainCamera = Camera.main;
        if (mainCamera == null && HasCameraViews())
        {
            Debug.LogWarning("[MonitorController] Main Camera not found.");
            return;
        }

        isMonitorOpen = true;
        AudioManager.Instance.Play(SFX.MonitorOpen);

        var camRoomManager = FindObjectOfType<CameraRoomManager>();
        if (camRoomManager != null)
            savedRoom = camRoomManager.currentRoom;

        monsterRef = FindObjectOfType<MonsterController>();
        if (monsterRef != null)
            monsterRef.enabled = false;

        playerRef = FindObjectOfType<PlayerController>();
        if (playerRef != null)
            playerRef.Input.DisablePlayerMoveInput();

        SetVisionMaskEnabled(false);

        ShowUI();

        currentCameraIndex = 0;
        ShowCamera(0);

        if (Random.value < signalLostChance)
            StartCoroutine(SignalLostCoroutine());
    }

    private void CloseMonitor()
    {
        isMonitorOpen = false;
        isSignalLost = false;
        AudioManager.Instance.Play(SFX.MonitorClose);

        HideUI();
        SetVisionMaskEnabled(true);
        DisableMonitorCamera();

        var camRoomManager = FindObjectOfType<CameraRoomManager>();
        if (camRoomManager != null && !string.IsNullOrEmpty(savedRoom))
            camRoomManager.SwitchRoom(savedRoom);

        if (monsterRef != null)
            monsterRef.enabled = true;
        monsterRef = null;

        if (playerRef != null)
            playerRef.Input.EnablePlayerMoveInput();
        playerRef = null;

        isOnCooldown = true;
        lastCloseTime = Time.unscaledTime;
        StartCoroutine(CooldownCoroutine());
    }

    public void NextCamera()
    {
        if (!isMonitorOpen || isSignalLost || monitorViews.Count == 0)
            return;

        currentCameraIndex = (currentCameraIndex + 1) % monitorViews.Count;
        ShowCamera(currentCameraIndex);
        AudioManager.Instance.Play(SFX.MonitorStatic);
    }

    public void PrevCamera()
    {
        if (!isMonitorOpen || isSignalLost || monitorViews.Count == 0)
            return;

        currentCameraIndex = (currentCameraIndex - 1 + monitorViews.Count) % monitorViews.Count;
        ShowCamera(currentCameraIndex);
        AudioManager.Instance.Play(SFX.MonitorStatic);
    }

    private void ShowCamera(int index)
    {
        if (index < 0 || index >= monitorViews.Count)
            return;

        var view = monitorViews[index];

        if (view.usesImage)
        {
            DisableMonitorCamera();
            if (monitorUIInstance != null)
                monitorUIInstance.SetCameraFeed(view.image);

            return;
        }

        if (monitorUIInstance != null)
            monitorUIInstance.SetCameraFeed(null);

        if (mainCamera == null)
        {
            Debug.LogWarning("[MonitorController] Cannot show camera view because Main Camera is missing.");
            return;
        }

        CreateOrEnableMonitorCamera();
        if (monitorCamera == null)
            return;

        monitorCamera.transform.SetPositionAndRotation(view.position, view.rotation);
        monitorCamera.orthographic = view.orthographic;
        if (view.orthographic)
            monitorCamera.orthographicSize = view.orthographicSize;
        else
            monitorCamera.fieldOfView = view.fieldOfView;
    }

    private void BuildViews()
    {
        monitorViews.Clear();

        if ((preferImageFeeds || currentMode == MonitorMode.Image) && TryBuildViewsFromImageFeeds())
        {
            return;
        }

        if (currentMode == MonitorMode.Image)
        {
            Debug.LogWarning("[MonitorController] Monitor is in Image mode but no image feeds are configured.");
            return;
        }

        if (TryBuildViewsFromRoomManager())
        {
            return;
        }

        BuildViewsFromRoomBounds();
    }

    private bool TryBuildViewsFromImageFeeds()
    {
        if (imageFeeds == null || imageFeeds.Length == 0)
            return false;

        foreach (var feed in imageFeeds)
        {
            if (feed == null || !feed.enabled)
                continue;

            Sprite feedImage = feed.image != null ? feed.image : LoadSpriteFromPath(feed.imagePath);
            if (feedImage == null)
            {
                Debug.LogWarning($"[MonitorController] Image feed '{feed.roomName}' has no sprite: {feed.imagePath}");
                continue;
            }

            monitorViews.Add(new MonitorView
            {
                roomName = string.IsNullOrWhiteSpace(feed.roomName) ? feedImage.name : feed.roomName,
                image = feedImage,
                usesImage = true,
                rotation = Quaternion.identity,
                orthographic = true,
                orthographicSize = cameraOrthoSize,
                fieldOfView = 60f
            });
        }

        return monitorViews.Count > 0;
    }

    private static Sprite LoadSpriteFromPath(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
#else
        return null;
#endif
    }

    private bool TryBuildViewsFromRoomManager()
    {
        var camRoomManager = FindObjectOfType<CameraRoomManager>();
        if (camRoomManager == null)
        {
            return false;
        }

        var roomCameras = camRoomManager.GetValidRoomCameras();
        if (roomCameras == null || roomCameras.Length == 0)
        {
            return false;
        }

        foreach (var roomCamera in roomCameras)
        {
            if (roomCamera == null || roomCamera.vcam == null)
                continue;

            var vcam = roomCamera.vcam;
            monitorViews.Add(new MonitorView
            {
                roomName = roomCamera.roomName,
                position = vcam.transform.position,
                rotation = vcam.transform.rotation,
                orthographic = vcam.m_Lens.Orthographic,
                orthographicSize = vcam.m_Lens.OrthographicSize,
                fieldOfView = vcam.m_Lens.FieldOfView
            });
        }

        monitorViews.Sort((a, b) => string.Compare(a.roomName, b.roomName, System.StringComparison.Ordinal));
        return monitorViews.Count > 0;
    }

    private void BuildViewsFromRoomBounds()
    {
        var roomBounds = GetSceneRoomBounds();
        foreach (var col in roomBounds)
        {
            string roomName = col.name.Replace("RoomBounds_", "");
            Vector2 center = col.bounds.center;
            monitorViews.Add(new MonitorView
            {
                roomName = roomName,
                position = new Vector3(center.x, center.y, -cameraHeight),
                rotation = Quaternion.identity,
                orthographic = true,
                orthographicSize = cameraOrthoSize,
                fieldOfView = 60f
            });
        }

        monitorViews.Sort((a, b) => string.Compare(a.roomName, b.roomName, System.StringComparison.Ordinal));
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
                    if (col == null || !col.name.StartsWith("RoomBounds_"))
                        continue;

                    roomBounds.Add(col);
                }
            }
        }

        return roomBounds.ToArray();
    }

    private bool HasCameraViews()
    {
        for (int i = 0; i < monitorViews.Count; i++)
        {
            if (!monitorViews[i].usesImage)
                return true;
        }

        return false;
    }

    private void CreateOrEnableMonitorCamera()
    {
        if (monitorCameraObject == null)
        {
            monitorCameraObject = new GameObject("[MonitorViewCamera]");
            monitorCamera = monitorCameraObject.AddComponent<Camera>();
        }
        else if (monitorCamera == null)
        {
            monitorCamera = monitorCameraObject.GetComponent<Camera>();
            if (monitorCamera == null)
                monitorCamera = monitorCameraObject.AddComponent<Camera>();
        }

        monitorCamera.CopyFrom(mainCamera);
        monitorCamera.depth = mainCamera.depth + 100f;
        monitorCamera.targetTexture = null;
        monitorCamera.enabled = true;
        monitorCameraObject.SetActive(true);
    }

    private void DisableMonitorCamera()
    {
        if (monitorCamera != null)
            monitorCamera.enabled = false;

        if (monitorCameraObject != null)
            monitorCameraObject.SetActive(false);
    }

    private void CleanupMonitorCamera()
    {
        if (monitorCameraObject == null)
            return;

        Destroy(monitorCameraObject);
        monitorCameraObject = null;
        monitorCamera = null;
    }

    private void SetVisionMaskEnabled(bool enabled)
    {
        var mask = FindObjectOfType<PlayerVisionMaskSystem>();
        if (mask == null)
            return;

        mask.SetForceHidden(!enabled);
    }

    private string GetCameraRoomName(int index)
    {
        if (index < 0 || index >= monitorViews.Count)
            return "<out_of_range>";

        return monitorViews[index].roomName;
    }

    private void ShowUI()
    {
        if (monitorUIPrefab == null)
        {
            Debug.LogWarning("[MonitorController] monitorUIPrefab is null.");
            return;
        }

        if (monitorUIInstance == null)
        {
            var canvasGo = new GameObject("[MonitorCanvas]");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            monitorUIInstance = Instantiate(monitorUIPrefab, canvas.transform);
        }

        monitorUIInstance.Show();
    }

    private void HideUI()
    {
        if (monitorUIInstance != null)
            monitorUIInstance.Hide();
    }

    private void CleanupUI()
    {
        if (monitorUIInstance == null)
            return;

        Destroy(monitorUIInstance.gameObject);
        monitorUIInstance = null;
    }

    private IEnumerator SignalLostCoroutine()
    {
        isSignalLost = true;
        AudioManager.Instance.Play(SFX.MonitorSignalLost);

        yield return new WaitForSecondsRealtime(signalRecoverTime);

        if (!isMonitorOpen)
            yield break;

        isSignalLost = false;
        ShowCamera(currentCameraIndex);
    }

    private IEnumerator CooldownCoroutine()
    {
        yield return new WaitForSecondsRealtime(cooldownDuration);
        isOnCooldown = false;
        AudioManager.Instance.Play(SFX.MonitorCooldownDone);
    }
}
