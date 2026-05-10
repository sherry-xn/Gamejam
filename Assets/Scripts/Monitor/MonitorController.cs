using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;

/// <summary>
/// 监控终端组件。按 R 键全局开关监控，按 Z/X 切换房间视角。
/// 自动根据 RoomBounds 碰撞体生成俯视相机，不依赖 CameraRoomManager.roomCameras。
/// 挂在场景中任意 GameObject 上即可。
/// </summary>
public class MonitorController : MonoBehaviour
{
    public static MonitorController Instance { get; private set; }

    [Header("监控设置")]
    [SerializeField, Tooltip("监控相机高度（俯视距离）")]
    private float cameraHeight = 30f;
    [SerializeField, Tooltip("监控相机正交大小")]
    private float cameraOrthoSize = 12f;
    [SerializeField, Tooltip("监控使用后冷却时间（秒）")]
    private float cooldownDuration = 5f;
    [SerializeField, Tooltip("信号丢失概率（0-1）")]
    private float signalLostChance = 0.1f;
    [SerializeField, Tooltip("信号恢复时间（秒）")]
    private float signalRecoverTime = 3f;
    [SerializeField, Tooltip("监控模式下的相机 Priority")]
    private int monitorPriority = 200;

    private struct MonitorCam
    {
        public string roomName;
        public CinemachineVirtualCamera vcam;
    }

    private List<MonitorCam> monitorCameras = new List<MonitorCam>();
    private int currentCameraIndex = 0;
    private bool isMonitorOpen = false;
    private bool isOnCooldown = false;
    private bool isSignalLost = false;
    private float lastCloseTime = -999f;
    private string savedRoom;
    private PlayerController playerRef;
    private MonsterController monsterRef;
    private GameObject monitorCameraRoot;
    private CinemachineBrain brainRef;
    private CinemachineBlendDefinition savedBlend;
    private Coroutine restoreBlendCoroutine;

    public bool IsMonitorOpen => isMonitorOpen;

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
        if (Instance == this) Instance = null;
        CleanupCameras();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.R)) return;

        if (isMonitorOpen)
            CloseMonitor();
        else
            OpenMonitor();
    }

    private void OpenMonitor()
    {
        if (isOnCooldown)
        {
            float remaining = cooldownDuration - (Time.unscaledTime - lastCloseTime);
            Debug.Log($"[Monitor] 冷却中，剩余 {remaining:F1} 秒");
            return;
        }

        BuildCameras();

        if (monitorCameras.Count == 0)
        {
            Debug.LogWarning("[MonitorController] 没有找到房间边界 (RoomBounds_XXX)");
            return;
        }

        isMonitorOpen = true;
        AudioManager.Instance.Play(SFX.MonitorOpen);

        // 保存当前房间
        var camRoomManager = FindObjectOfType<CameraRoomManager>();
        if (camRoomManager != null)
            savedRoom = camRoomManager.currentRoom;

        // 暂停怪物
        monsterRef = FindObjectOfType<MonsterController>();
        if (monsterRef != null) monsterRef.enabled = false;

        // 禁用玩家
        playerRef = FindObjectOfType<PlayerController>();
        if (playerRef != null) playerRef.Input.DisablePlayerMoveInput();

        // 隐藏玩家视觉遮罩
        SetVisionMaskEnabled(false);

        // 切换为瞬切模式
        brainRef = Camera.main.GetComponent<CinemachineBrain>();
        if (brainRef != null)
        {
            savedBlend = brainRef.m_DefaultBlend;
            brainRef.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
        }

        ShowCamera(0);

        if (Random.value < signalLostChance)
            StartCoroutine(SignalLostCoroutine());
    }

    private void CloseMonitor()
    {
        isMonitorOpen = false;
        isSignalLost = false;
        AudioManager.Instance.Play(SFX.MonitorClose);

        // 隐藏所有监控相机
        HideAllCameras();

        // 恢复玩家视觉遮罩
        SetVisionMaskEnabled(true);

        // 关闭监控时强制瞬切回角色相机，避免沿用 monitor 的 blend 过渡
        if (brainRef != null)
        {
            brainRef.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
        }

        // 恢复原来房间视角
        var camRoomManager = FindObjectOfType<CameraRoomManager>();
        if (camRoomManager != null && !string.IsNullOrEmpty(savedRoom))
            camRoomManager.SwitchRoom(savedRoom);

        // 下一帧再恢复原本的混合模式，保证当前切换是瞬切
        if (brainRef != null)
        {
            if (restoreBlendCoroutine != null)
                StopCoroutine(restoreBlendCoroutine);
            restoreBlendCoroutine = StartCoroutine(RestoreBlendNextFrame(savedBlend));
        }

        // 恢复怪物
        if (monsterRef != null) monsterRef.enabled = true;
        monsterRef = null;

        // 恢复玩家
        if (playerRef != null) playerRef.Input.EnablePlayerMoveInput();
        playerRef = null;

        isOnCooldown = true;
        lastCloseTime = Time.unscaledTime;
        StartCoroutine(CooldownCoroutine());
    }

    private IEnumerator RestoreBlendNextFrame(CinemachineBlendDefinition blend)
    {
        yield return null;

        if (brainRef != null)
        {
            brainRef.m_DefaultBlend = blend;
        }

        restoreBlendCoroutine = null;
    }

    public void NextCamera()
    {
        if (!isMonitorOpen || isSignalLost) return;
        if (monitorCameras.Count == 0) return;

        currentCameraIndex = (currentCameraIndex + 1) % monitorCameras.Count;
        ShowCamera(currentCameraIndex);
        AudioManager.Instance.Play(SFX.MonitorStatic);
    }

    public void PrevCamera()
    {
        if (!isMonitorOpen || isSignalLost) return;
        if (monitorCameras.Count == 0) return;

        currentCameraIndex = (currentCameraIndex - 1 + monitorCameras.Count) % monitorCameras.Count;
        ShowCamera(currentCameraIndex);
        AudioManager.Instance.Play(SFX.MonitorStatic);
    }

    private void ShowCamera(int index)
    {
        for (int i = 0; i < monitorCameras.Count; i++)
        {
            var cam = monitorCameras[i];
            if (cam.vcam != null)
                cam.vcam.Priority = (i == index) ? monitorPriority : 0;
        }
    }

    private void HideAllCameras()
    {
        foreach (var cam in monitorCameras)
        {
            if (cam.vcam != null)
                cam.vcam.Priority = 0;
        }
    }

    private void SetVisionMaskEnabled(bool enabled)
    {
        var mask = FindObjectOfType<PlayerVisionMaskSystem>();
        if (mask == null) return;

        mask.SetForceHidden(!enabled);
    }

    private void BuildCameras()
    {
        // 每次打开时清理重建，确保和场景同步
        CleanupCameras();

        monitorCameraRoot = new GameObject("[MonitorCameras]");

        // 找到场景中所有 RoomBounds_XXX
        var roomBounds = FindObjectsOfType<PolygonCollider2D>();
        foreach (var col in roomBounds)
        {
            if (!col.name.StartsWith("RoomBounds_")) continue;

            string roomName = col.name.Replace("RoomBounds_", "");
            Vector2 center = col.bounds.center;
            Vector2 size = col.bounds.size;

            // 创建 CinemachineVirtualCamera
            var camGo = new GameObject($"MonitorCam_{roomName}");
            camGo.transform.SetParent(monitorCameraRoot.transform);
            camGo.transform.position = new Vector3(center.x, center.y, -cameraHeight);

            var vcam = camGo.AddComponent<CinemachineVirtualCamera>();
            vcam.Priority = 0;
            vcam.m_Lens.OrthographicSize = cameraOrthoSize;
            vcam.m_Lens.Orthographic = true;

            // 添加 Confiner2D 限制在房间边界内
            var confiner = camGo.AddComponent<CinemachineConfiner2D>();
            confiner.m_BoundingShape2D = col;
            confiner.m_Damping = 0f;
            confiner.m_MaxWindowSize = 0;
            confiner.InvalidateCache();

            monitorCameras.Add(new MonitorCam
            {
                roomName = roomName,
                vcam = vcam
            });
        }

        // 按房间名排序，保证顺序一致
        monitorCameras.Sort((a, b) => string.Compare(a.roomName, b.roomName, System.StringComparison.Ordinal));
    }

    private void CleanupCameras()
    {
        if (monitorCameraRoot != null)
            Destroy(monitorCameraRoot);
        monitorCameras.Clear();
    }

    private IEnumerator SignalLostCoroutine()
    {
        isSignalLost = true;
        AudioManager.Instance.Play(SFX.MonitorSignalLost);

        yield return new WaitForSecondsRealtime(signalRecoverTime);

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
