using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public PlayerInput Input { get; private set; }
    public Rigidbody2D Rigidbody { get; private set; }
    public Transform InteractionIcon { get; private set; }

    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("旋转设置")]
    [SerializeField] private float rotateSpeed = 720f;

    [Tooltip("角色贴图默认朝向与右方向的夹角偏移。比如贴图默认朝上，就填 -90。")]
    [SerializeField] private float rotationOffset = 0f;

    [Tooltip("鼠标移动超过这个像素距离才更新朝向，避免隐藏光标后轻微抖动。")]
    [SerializeField, Min(0f)] private float mouseMovePixelThreshold = 0.1f;

    [Tooltip("鼠标灵敏度倍率，从设置菜单读取。")]
    private float mouseSensitivity = 1f;

    [Header("交互范围")]
    [Tooltip("子物体上用于主动检测的范围（通常为 Trigger 的 BoxCollider2D）。不赋值则无法通过 TryGetObjectInRange 在范围内按 Tag 取物。")]
    [SerializeField] private BoxCollider2D interactionBox;

    [SerializeField] private string doorTag = "Door";

    [Header("门")]
    [Tooltip("开门后门 SpriteRenderer 的透明度。")]
    [SerializeField, Range(0f, 1f)] private float doorOpenedSpriteAlpha = 0.7f;

    [field: Header("UI控件")]
    [field: SerializeField] public Slider HealthBar { get; private set; }
    [field: SerializeField] public TextMeshProUGUI KeyNumberText { get; private set; }

    [field: Header("玩家数据")]
    [field: SerializeField] public PlayerInfo Data { get; private set; }

    [Header("暂停菜单")]
    [SerializeField] private PauseMenu pauseMenuPrefab;

    [Header("2D 平面修正")]
    [Tooltip("2D 游戏中玩家固定使用的 Z 值，避免启动时被放到错误深度导致看不见。")]
    [SerializeField] private float fixedWorldZ = 0f;

    [Header("音效")]
    [Tooltip("脚步声播放间隔（秒）")]
    [SerializeField, Min(0.1f)] private float footstepInterval = 0.4f;

    // 缓存交互对象
    private GameObject targetObject = null;
    private Vector2 moveInput;
    private Vector2 lastMouseScreenPosition;
    private Vector2 cachedLookDirection;
    private Camera mainCamera;
    private bool isInteracting = false;
    /// <summary>
    /// 玩家是否正在与床/柜子交互（隐藏状态）
    /// </summary>
    public bool IsInteracting => isInteracting;
    private bool hasLastMouseScreenPosition = false;
    private bool hasCachedLookDirection = false;
    private readonly Collider2D[] interactionOverlapArray = new Collider2D[32];
    private readonly HashSet<int> openedDoorIds = new HashSet<int>();
    private float lastFootstepTime;

    public int CurrentHealth { get; private set; }
    public int CurrentKey { get; private set; }

    private void Awake()
    {
        Cursor.visible = false;
        Input = GetComponent<PlayerInput>();
        Rigidbody = GetComponent<Rigidbody2D>();
        InteractionIcon = transform.Find("InteractionIcon");
        // Dynamic + gravity 0：与静态 Collider 正常阻挡（Kinematic 默认不碰静态体）。
        Rigidbody.bodyType = RigidbodyType2D.Dynamic;
        Rigidbody.gravityScale = 0f;
        mainCamera = Camera.main;
        NormalizeTo2DPlane();
        Initialize();

        // 加载鼠标灵敏度
        mouseSensitivity = SettingsMenu.GetMouseSensitivity();

        // 自动创建暂停菜单
        if (FindObjectOfType<PauseMenu>() == null)
        {
            if (!TryCreatePauseMenuFromPrefab())
            {
                var pauseObj = new GameObject("[PauseMenu]");
                pauseObj.AddComponent<PauseMenu>();
            }
        }
    }

    private void Start()
    {
        AddInputActionCallbacks();
    }

    private void OnEnable()
    {
        hasLastMouseScreenPosition = false;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.visible = true;
        RemoveInputActionCallbacks();
    }

    private bool TryCreatePauseMenuFromPrefab()
    {
        if (pauseMenuPrefab == null)
        {
#if UNITY_EDITOR
            pauseMenuPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<PauseMenu>("Assets/Prefabs/UI/PauseMenu.prefab");
#endif
        }

        if (pauseMenuPrefab == null)
            return false;

        Instantiate(pauseMenuPrefab.gameObject);
        return true;
    }

    private void Update()
    {
        if (PauseMenu.IsPaused) return;

        // 读取移动输入，后续在物理帧中统一处理位移。
        moveInput = Input.PlayerActions.Move.ReadValue<Vector2>();
        UpdateCachedLookDirection();
        UpdateInteractionIcon();

        // 实时更新灵敏度（用户可能在设置中修改）
        mouseSensitivity = SettingsMenu.GetMouseSensitivity();
    }

    private void FixedUpdate()
    {
        if (PauseMenu.IsPaused) return;

        MovePlayer();
        RotatePlayer();
        // ClampToCamera();
    }


    public void TakeDamage(int damage = 1)
    {
        damage = Mathf.Clamp(damage, 0, CurrentHealth);
        CurrentHealth -= damage;
        SyncHealthBarUI();
        HurtUI.Show();

        if (CurrentHealth <= 0)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPlayerDeath();
            else
                AudioManager.Instance.Play(SFX.GameOver);
        }
    }

    public void AddKey(int key = 1)
    {
        CurrentKey += key;
        KeyNumberText.text = CurrentKey.ToString();
    }


    private void Initialize()
    {
        CurrentHealth = Data.MaxHealth;
        CurrentKey = 0;
        SyncHealthBarUI();
        if (KeyNumberText != null)
            KeyNumberText.text = CurrentKey.ToString();
    }

    private void SyncHealthBarUI()
    {
        if (HealthBar == null) return;
        int max = Mathf.Max(1, Data.MaxHealth);
        HealthBar.minValue = 0f;
        HealthBar.maxValue = max;
        HealthBar.value = Mathf.Clamp(CurrentHealth, 0, max);
    }

    /// <summary>与 <see cref="TryGetObjectInRange"/> 使用同一套范围检测，避免 Trigger 与 Box 不同步导致按键无效。</summary>
    private void UpdateInteractionIcon()
    {
        if (InteractionIcon == null)
        {
            return;
        }

        if (PauseMenu.IsPaused || StoryPanel.IsShowing)
        {
            InteractionIcon.gameObject.SetActive(false);
            return;
        }

        bool show = isInteracting
            || TryGetHideableInRange(out _)
            || TryGetInteractableItemInRange(out _)
            || TryGetObjectInRange(doorTag, out _);
        InteractionIcon.gameObject.SetActive(show);
    }

    private void MovePlayer()
    {
        Rigidbody.velocity = moveInput.normalized * moveSpeed;

        // 脚步声
        if (moveInput.sqrMagnitude > 0.01f && Time.time - lastFootstepTime >= footstepInterval)
        {
            lastFootstepTime = Time.time;
            AudioManager.Instance.PlayAtPosition(SFX.PlayerFootstep, transform.position);
        }
    }

    private void UpdateCachedLookDirection(float minimum = 0.0001f)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null || Mouse.current == null)
        {
            return;
        }

        Vector2 currentMouseScreenPosition = Mouse.current.position.ReadValue();
        if (!hasLastMouseScreenPosition)
        {
            lastMouseScreenPosition = currentMouseScreenPosition;
            hasLastMouseScreenPosition = true;
            return;
        }

        Vector2 mouseDelta = currentMouseScreenPosition - lastMouseScreenPosition;
        lastMouseScreenPosition = currentMouseScreenPosition;

        if (mouseDelta.sqrMagnitude < mouseMovePixelThreshold * mouseMovePixelThreshold)
        {
            return;
        }

        Vector3 mouseScreenPosition = currentMouseScreenPosition;
        mouseScreenPosition.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        Vector2 direction = (Vector2)mouseWorldPosition - Rigidbody.position;

        // 鼠标贴近玩家中心时不更新缓存方向，避免方向在极小距离内抖动。
        if (direction.sqrMagnitude < minimum)
        {
            return;
        }

        cachedLookDirection = direction.normalized;
        hasCachedLookDirection = true;
    }

    private void RotatePlayer()
    {
        if (PauseMenu.IsPaused) return;
        if (StoryPanel.IsShowing) return;
        if (!hasCachedLookDirection)
        {
            return;
        }

        // 先把缓存的鼠标朝向转换成角度，再叠加贴图朝向偏移。
        float targetAngle = Mathf.Atan2(cachedLookDirection.y, cachedLookDirection.x) * Mathf.Rad2Deg + rotationOffset;
        float adjustedRotateSpeed = rotateSpeed * mouseSensitivity;
        float nextAngle = Mathf.MoveTowardsAngle(Rigidbody.rotation, targetAngle, adjustedRotateSpeed * Time.fixedDeltaTime);
        Rigidbody.MoveRotation(nextAngle);
    }

    // private void ClampToCamera()
    // {
    //     if (mainCamera == null)
    //     {
    //         return;
    //     }

    //     // 将玩家限制在当前摄像机可视范围内，避免角色移动出屏幕。
    //     float camHeight = mainCamera.orthographicSize;
    //     float camWidth = camHeight * mainCamera.aspect;

    //     Vector2 position = Rigidbody.position;
    //     position.x = Mathf.Clamp(position.x, -camWidth, camWidth);
    //     position.y = Mathf.Clamp(position.y, -camHeight, camHeight);

    //     Rigidbody.position = position;
    // }


    private void AddInputActionCallbacks()
    {
        Input.PlayerActions.Interaction.started += OnInteractionStarted;
    }

    private void RemoveInputActionCallbacks()
    {
        Input.PlayerActions.Interaction.started -= OnInteractionStarted;
    }

    /// <summary>在 <see cref="interactionBox"/> 与当前重叠的碰撞体中，取第一个 <paramref name="tag"/> 匹配的物体。tag 需已在 Tag 管理器注册。</summary>
    public bool TryGetObjectInRange(string tag, out GameObject gameObject)
    {
        gameObject = null;
        if (string.IsNullOrEmpty(tag) || interactionBox == null)
        {
            return false;
        }

        Vector2 center = interactionBox.transform.TransformPoint(interactionBox.offset);
        var lossy = interactionBox.transform.lossyScale;
        var size = new Vector2(
            interactionBox.size.x * Mathf.Abs(lossy.x),
            interactionBox.size.y * Mathf.Abs(lossy.y));
        float angle = interactionBox.transform.eulerAngles.z;
        // 第二个 size 为半宽/半高，与 BoxCollider2D 的整宽整高（已乘 lossyScale）一致
        int count = Physics2D.OverlapBoxNonAlloc(
            center,
            size * 0.5f,
            angle,
            interactionOverlapArray,
            ~0);
        for (int i = 0; i < count; i++)
        {
            var c = interactionOverlapArray[i];
            if (c == null)
            {
                continue;
            }
            if (c.attachedRigidbody == Rigidbody)
            {
                continue;
            }
            if (c.CompareTag(tag))
            {
                gameObject = c.gameObject;
                return true;
            }
        }
        return false;
    }

    public bool TryGetHideableInRange(out Hideable hideable)
    {
        hideable = null;
        if (interactionBox == null) return false;

        Vector2 center = interactionBox.transform.TransformPoint(interactionBox.offset);
        var lossy = interactionBox.transform.lossyScale;
        var size = new Vector2(
            interactionBox.size.x * Mathf.Abs(lossy.x),
            interactionBox.size.y * Mathf.Abs(lossy.y));
        float angle = interactionBox.transform.eulerAngles.z;

        int count = Physics2D.OverlapBoxNonAlloc(
            center, size * 0.5f, angle, interactionOverlapArray, ~0);

        for (int i = 0; i < count; i++)
        {
            var c = interactionOverlapArray[i];
            if (c == null) continue;
            if (c.attachedRigidbody == Rigidbody) continue;
            var h = c.GetComponent<Hideable>();
            if (h != null) { hideable = h; return true; }
        }
        return false;
    }

    public bool TryGetInteractableItemInRange(out InteractableItem item)
    {
        item = null;
        if (interactionBox == null) return false;

        Vector2 center = interactionBox.transform.TransformPoint(interactionBox.offset);
        var lossy = interactionBox.transform.lossyScale;
        var size = new Vector2(
            interactionBox.size.x * Mathf.Abs(lossy.x),
            interactionBox.size.y * Mathf.Abs(lossy.y));
        float angle = interactionBox.transform.eulerAngles.z;

        int count = Physics2D.OverlapBoxNonAlloc(
            center, size * 0.5f, angle, interactionOverlapArray, ~0);

        for (int i = 0; i < count; i++)
        {
            var c = interactionOverlapArray[i];
            if (c == null) continue;
            if (c.attachedRigidbody == Rigidbody) continue;
            var ii = c.GetComponent<InteractableItem>();
            if (ii != null) { item = ii; return true; }
        }
        return false;
    }

    // 和门交互：按同一交互键切换开/关门。
    private void InteractWithDoor(GameObject door)
    {
        int doorId = door.GetInstanceID();
        bool isOpened = openedDoorIds.Contains(doorId);
        if (isOpened)
        {
            SetDoorOpenedState(door, false);
            openedDoorIds.Remove(doorId);
            AudioManager.Instance.Play(SFX.DoorClose);
            return;
        }

        // if (CurrentKey < keysRequiredToOpenDoor)
        // {
        //     Debug.Log(
        //         $"钥匙不足：需要 {keysRequiredToOpenDoor} 把钥匙才能开门，当前有 {CurrentKey:F0} 把。");
        //     return;
        // }

        // Debug.Log($"门已打开。");
        SetDoorOpenedState(door, true);
        openedDoorIds.Add(doorId);
        AudioManager.Instance.Play(SFX.DoorOpen);
    }

    private void SetDoorOpenedState(GameObject door, bool isOpened)
    {
        var sr = door.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null)
        {
            Color c = sr.color;
            c.a = isOpened ? doorOpenedSpriteAlpha : 1f;
            sr.color = c;
        }

        foreach (var col in door.GetComponentsInChildren<Collider2D>(true))
        {
            // 只禁用物理碰撞体（非 trigger），保留 trigger 用于交互检测
            if (!col.isTrigger)
                col.enabled = !isOpened;
        }
        
        // 通知门更新寻路网格
        var doorComponent = door.GetComponent<Door>();
        if (doorComponent != null)
        {
            doorComponent.OnDoorStateChanged();
        }
    }

    #region Input Methods

    // 玩家按下交互键时触发的回调方法，可以在这里处理交互逻辑，比如打开门、拾取物品等。
    private void OnInteractionStarted(InputAction.CallbackContext obj)
    {
        if (PauseMenu.IsPaused || StoryPanel.IsShowing) return;

        if (isInteracting)
        {
            if (targetObject == null) return;
            var currentHideable = targetObject.GetComponent<Hideable>();
            if (currentHideable != null) currentHideable.OnExit(this);
            isInteracting = false;
            Input.EnablePlayerMoveInput();
            targetObject = null;
            return;
        }

        if (TryGetHideableInRange(out var hideable))
        {
            targetObject = hideable.gameObject;
            isInteracting = true;
            Input.DisablePlayerMoveInput();
            hideable.OnEnter(this);
            return;
        }

        if (TryGetInteractableItemInRange(out var item))
        {
            item.OnInteracted(this);
            return;
        }

        if (TryGetObjectInRange(doorTag, out targetObject))
        {
            InteractWithDoor(targetObject);
        }
    }

    private void NormalizeTo2DPlane()
    {
        SetPlayerPosition(transform.position);
    }

    public void SetPlayerPosition(Vector3 position)
    {
        Vector3 fixedPosition = new Vector3(position.x, position.y, fixedWorldZ);
        transform.position = fixedPosition;

        // Rigidbody2D 只管理 XY，手动同步一次避免 Transform / Rigidbody 状态不一致。
        if (Rigidbody != null)
        {
            Rigidbody.position = fixedPosition;
        }
    }




    #endregion
}
