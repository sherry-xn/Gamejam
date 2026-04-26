using System;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("交互范围")]
    [Tooltip("子物体上用于主动检测的范围（通常为 Trigger 的 BoxCollider2D）。不赋值则无法通过 TryGetObjectInRange 在范围内按 Tag 取物。")]
    [SerializeField] private BoxCollider2D interactionBox;

    [SerializeField] private string bedTag = "Bed";

    // 缓存交互对象
    private GameObject targetObject = null;
    private Vector2 moveInput;
    private Camera mainCamera;
    private bool isInteracting = false;
    private readonly Collider2D[] interactionOverlapArray = new Collider2D[32];

    private void Awake()
    {
        Input = GetComponent<PlayerInput>();
        Rigidbody = GetComponent<Rigidbody2D>();
        InteractionIcon = transform.Find("InteractionIcon");
        // Dynamic + gravity 0：与静态 Collider 正常阻挡（Kinematic 默认不碰静态体）。
        Rigidbody.bodyType = RigidbodyType2D.Dynamic;
        Rigidbody.gravityScale = 0f;
        mainCamera = Camera.main;
    }

    private void Start()
    {
        AddInputActionCallbacks();
    }

    private void OnDisable()
    {
        RemoveInputActionCallbacks();
    }

    private void Update()
    {
        // 读取移动输入，后续在物理帧中统一处理移动和旋转。
        moveInput = Input.PlayerActions.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        MovePlayer();
        RotatePlayer();
        // ClampToCamera();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        InteractionIcon.gameObject.SetActive(true);
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        InteractionIcon.gameObject.SetActive(false);
    }

    private void MovePlayer()
    {
        // 直接设置速度，保证按键后能立刻产生位移。
        // 如果后续需要更“飘”的手感，可以再改回加速度式移动。
        Rigidbody.velocity = moveInput.normalized * moveSpeed;
    }

    private void RotatePlayer(float minimum = 0.0001f)
    {
        // 只有在确实有移动输入时才更新朝向。
        if (moveInput.sqrMagnitude < minimum)
        {
            return;
        }

        // 先把输入方向转换成角度，再叠加贴图朝向偏移。
        float targetAngle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg + rotationOffset;
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
        }
        // 如果交互图标可见，则触发交互逻辑
        if (InteractionIcon.gameObject.activeSelf)
        {
            // 尝试获取床
            if (TryGetObjectInRange(bedTag, out var target))
            {
                StartInteraction(target);
                // 获取床的子对象Point
                GameObject point = target.transform.Find("Point").gameObject;
                // 玩家躲到指定的床底
                transform.position = point.transform.position;


            }
        }
    }

    


    #endregion
}
