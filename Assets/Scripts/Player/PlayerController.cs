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

    [Header("交互范围")]
    [Tooltip("子物体上用于主动检测的范围（通常为 Trigger 的 BoxCollider2D）。不赋值则无法通过 TryGetObjectInRange 在范围内按 Tag 取物。")]
    [SerializeField] private BoxCollider2D interactionBox;

    [SerializeField] private string bedTag = "Bed";
    [SerializeField] private string travelBagTag = "TravelBag";
    [SerializeField] private string doorTag = "Door";
    [SerializeField] private string wardrobeTag = "Wardrobe";

    [Header("门")]
    [SerializeField, Min(1)] private int keysRequiredToOpenDoor = 3;
    [Tooltip("开门后门 SpriteRenderer 的透明度。")]
    [SerializeField, Range(0f, 1f)] private float doorOpenedSpriteAlpha = 0.7f;

    [field: Header("UI控件")]
    [field: SerializeField] public Slider HealthBar { get; private set; }
    [field: SerializeField] public TextMeshProUGUI KeyNumberText { get; private set; }

    [field: Header("玩家数据")]
    [field: SerializeField] public PlayerInfo Data { get; private set; }

    [Header("2D 平面修正")]
    [Tooltip("2D 游戏中玩家固定使用的 Z 值，避免启动时被放到错误深度导致看不见。")]
    [SerializeField] private float fixedWorldZ = 0f;

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

    private void Update()
    {
        // 读取移动输入，后续在物理帧中统一处理位移。
        moveInput = Input.PlayerActions.Move.ReadValue<Vector2>();
        UpdateCachedLookDirection();
        UpdateInteractionIcon();
    }

    private void FixedUpdate()
    {
        MovePlayer();
        RotatePlayer();
        // ClampToCamera();
    }


    public void TakeDamage(int damage = 1)
    {
        damage = Mathf.Clamp(damage, 0, CurrentHealth);
        CurrentHealth -= damage;
        HealthBar.value *= (float)CurrentHealth / Data.MaxHealth;

        if (CurrentHealth <= 0)
        {
            Debug.Log("玩家死亡");
            return;
        }
        else
        {
            Debug.Log("玩家受伤，当前生命值：" + CurrentHealth);
        }
    }

    public void AddKey(int key = 1)
    {
        CurrentKey += key;
        KeyNumberText.text = "Key: " + CurrentKey.ToString();
    }


    private void Initialize()
    {
        CurrentHealth = Data.MaxHealth;
        CurrentKey = 0;
        Debug.Log("玩家初始化，当前生命值：" + CurrentHealth + "，当前钥匙数量：" + CurrentKey);
    }

    /// <summary>与 <see cref="TryGetObjectInRange"/> 使用同一套范围检测，避免 Trigger 与 Box 不同步导致按键无效。</summary>
    private void UpdateInteractionIcon()
    {
        if (InteractionIcon == null)
        {
            return;
        }

        bool show = isInteracting
            || TryGetObjectInRange(bedTag, out _)
            || TryGetObjectInRange(travelBagTag, out _)
            || TryGetObjectInRange(doorTag, out _)
            || TryGetObjectInRange(wardrobeTag, out _);
        InteractionIcon.gameObject.SetActive(show);
    }

    private void MovePlayer()
    {
        // 直接设置速度，保证按键后能立刻产生位移。
        // 如果后续需要更“飘”的手感，可以再改回加速度式移动。
        Rigidbody.velocity = moveInput.normalized * moveSpeed;
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
        if (!hasCachedLookDirection)
        {
            return;
        }

        // 先把缓存的鼠标朝向转换成角度，再叠加贴图朝向偏移。
        float targetAngle = Mathf.Atan2(cachedLookDirection.y, cachedLookDirection.x) * Mathf.Rad2Deg + rotationOffset;
        float nextAngle = Mathf.MoveTowardsAngle(Rigidbody.rotation, targetAngle, rotateSpeed * Time.fixedDeltaTime);
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

    // 进入交互状态
    private void StartInteraction(GameObject target)
    {
        isInteracting = true;
        targetObject = target;
        // 暂时关闭物体的碰撞器
        target.GetComponent<Collider2D>().enabled = false;
        // 物体的SpriteRenderer阿尔法值变为0.5，表示被遮挡了
        var sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a = 0.7f;
            sr.color = c;
        }
        // 关闭玩家输入
        Input.DisablePlayerMoveInput();
    }

    // 退出交互状态
    private void ExitInteraction(GameObject target)
    {
        isInteracting = false;
        // 打开物体的碰撞器
        target.GetComponent<Collider2D>().enabled = true;
        // 物体的SpriteRenderer阿尔法值变为1，表示被遮挡了
        var sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
        // 开启玩家输入
        Input.EnablePlayerMoveInput();
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

    // 与旅行包交互
    private void InteractWithTravelBag(GameObject travelBag)
    {
        var keyT = travelBag.transform.Find("Key");
        if (keyT == null)
        {
            Debug.Log("旅行包没有 Key 子物体");
            return;
        }

        if (!keyT.gameObject.activeSelf)
        {
            Debug.Log("旅行包没有钥匙");
            return;
        }

        keyT.gameObject.SetActive(false);
        AddKey();
        Debug.Log("获得钥匙，当前钥匙数量：" + CurrentKey);
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
            Debug.Log("门已关闭。");
            return;
        }

        if (CurrentKey < keysRequiredToOpenDoor)
        {
            Debug.Log(
                $"钥匙不足：需要 {keysRequiredToOpenDoor} 把钥匙才能开门，当前有 {CurrentKey:F0} 把。");
            return;
        }

        Debug.Log($"集齐了 {keysRequiredToOpenDoor} 把钥匙，门已打开。");
        SetDoorOpenedState(door, true);
        openedDoorIds.Add(doorId);
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
        if (isInteracting)
        {
            if (targetObject == null)
            {
                return;
            }

            ExitInteraction(targetObject);
            targetObject = null;
            // 同一键按下只处理“钻出”，否则本帧还会继续 TryGet 床，可能马上再次钻进
            return;
        }

        if (TryGetObjectInRange(bedTag, out targetObject))
        {
            StartInteraction(targetObject);
            var pointT = targetObject.transform.Find("bedPoint");
            if (pointT != null)
            {
                SetPlayerPosition(pointT.position);
            }

            return;
        }
        if (TryGetObjectInRange(wardrobeTag, out targetObject))
        {
            StartInteraction(targetObject);
            var pointT = targetObject.transform.Find("wardrobePoint");
            if (pointT != null)
            {
                SetPlayerPosition(pointT.position);
            }

            return;
        }

        if (TryGetObjectInRange(travelBagTag, out targetObject))
        {
            InteractWithTravelBag(targetObject);
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

    private void SetPlayerPosition(Vector3 position)
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
