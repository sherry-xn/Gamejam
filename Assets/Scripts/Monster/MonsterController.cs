using UnityEngine;
using Pathfinding;

public enum MonsterState
{
    Tracking,
    Wandering
}

[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(AIPath))]
public class MonsterController : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] private MonsterInfo data;
    
    [Header("引用")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerController playerController;
    
    [Header("调试")]
    [SerializeField] private MonsterState currentState = MonsterState.Wandering;
    
    private AIPath aiPath;
    private float lastAttackTime;
    private float lastWanderTime;
    private Vector2 wanderTarget;
    private bool hasWanderTarget = false;
    
    private void Awake()
    {
        aiPath = GetComponent<AIPath>();
    }
    
    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("[MonsterController] MonsterInfo data is not assigned!");
            enabled = false;
            return;
        }

        aiPath.maxSpeed = data.moveSpeed;
        aiPath.enableRotation = false;
        aiPath.canMove = true;
        aiPath.canSearch = true;
        
#if UNITY_EDITOR
        Debug.Log($"[MonsterController] 初始化完成 - maxSpeed: {aiPath.maxSpeed}, canMove: {aiPath.canMove}, canSearch: {aiPath.canSearch}");
        Debug.Log($"[MonsterController] 怪物位置: {transform.position}");
        
        // 检查是否在寻路网格上
        var node = AstarPath.active.GetNearest(transform.position);
        if (node.node != null)
        {
            Debug.Log($"[MonsterController] 最近节点位置: {(Vector3)node.node.position}");
        }
        else
        {
            Debug.LogError("[MonsterController] 找不到附近的寻路节点！怪物不在寻路网格上！");
        }
#endif
        
        SwitchToWandering();
    }
    
    private void Update()
    {
        switch (currentState)
        {
            case MonsterState.Tracking:
                TrackingUpdate();
                break;
            case MonsterState.Wandering:
                WanderingUpdate();
                break;
        }
    }
    
    private void CheckPlayerState()
    {
        if (playerController == null || player == null) return;
        
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        
        if (playerController.IsInteracting || distanceToPlayer > data.trackingRange)
        {
            if (currentState == MonsterState.Tracking)
            {
                SwitchToWandering();
            }
        }
        else
        {
            if (currentState == MonsterState.Wandering && distanceToPlayer <= data.trackingRange)
            {
                SwitchToTracking();
            }
        }
    }
    
    private void SwitchToTracking()
    {
        currentState = MonsterState.Tracking;
        hasWanderTarget = false;
#if UNITY_EDITOR
        Debug.Log("怪物切换到追踪状态");
#endif
    }
    
    private void SwitchToWandering()
    {
        currentState = MonsterState.Wandering;
        hasWanderTarget = false;
        lastWanderTime = Time.time;
        SetNewWanderPoint();
#if UNITY_EDITOR
        Debug.Log("怪物切换到漫游状态");
#endif
    }
    
    private void TrackingUpdate()
    {
        CheckPlayerState();
        
        if (currentState != MonsterState.Tracking) return;
        if (player == null) return;
        
        // 设置寻路目标为玩家位置
        aiPath.destination = player.position;
        
#if UNITY_EDITOR
        // 调试信息
        if (Time.frameCount % 60 == 0) // 每60帧输出一次
        {
            Debug.Log($"[Monster] State: {currentState}, Pos: {transform.position}, Dest: {aiPath.destination}, Velocity: {aiPath.velocity}, Remaining: {aiPath.remainingDistance}");
        }
#endif
        
        // 如果寻路失败（Remaining为Infinity），使用直接移动
        if (float.IsInfinity(aiPath.remainingDistance))
        {
            // 直接朝玩家方向移动
            Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
            transform.position += (Vector3)(direction * data.moveSpeed * Time.deltaTime);
        }
        
        // 检测是否到达攻击范围
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= data.attackRange)
        {
            TryAttackPlayer();
        }
    }
    
    private void WanderingUpdate()
    {
        CheckPlayerState();
        
        if (currentState != MonsterState.Wandering) return;
        
        if (aiPath.reachedDestination || !hasWanderTarget)
        {
            if (Time.time - lastWanderTime >= data.wanderInterval)
            {
                SetNewWanderPoint();
                lastWanderTime = Time.time;
            }
        }
    }
    
    private void SetNewWanderPoint()
    {
        // 在当前位置周围随机选择一个点
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        wanderTarget = (Vector2)transform.position + randomDirection * data.wanderRadius;
        
        // 设置寻路目标
        aiPath.destination = wanderTarget;
        hasWanderTarget = true;
    }
    
    private void TryAttackPlayer()
    {
        if (playerController == null) return;
        if (Time.time - lastAttackTime < data.attackCooldown) return;
        
        lastAttackTime = Time.time;
        playerController.TakeDamage((int)data.attackDamage);
#if UNITY_EDITOR
        Debug.Log("怪物攻击玩家");
#endif
    }
    
    private void OnDrawGizmosSelected()
    {
        // 绘制追踪范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.trackingRange);
        
        // 绘制攻击范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.attackRange);
        
        // 绘制漫游范围
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, data.wanderRadius);
    }
}
