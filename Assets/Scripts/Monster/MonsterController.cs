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
    private Seeker seeker;
    private float lastAttackTime;
    private Vector2 wanderTarget;
    private bool hasWanderTarget = false;
    
    private void Awake()
    {
        aiPath = GetComponent<AIPath>();
        seeker = GetComponent<Seeker>();
    }
    
    private void Start()
    {
        // 配置 AIPath 参数
        aiPath.maxSpeed = data.moveSpeed;
        aiPath.enableRotation = false; // 2D 游戏通常不需要 3D 旋转
        
        // 初始状态为漫游
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
        if (playerController == null) return;
        
        if (playerController.IsInteracting)
        {
            if (currentState == MonsterState.Tracking)
            {
                SwitchToWandering();
            }
        }
        else
        {
            if (currentState == MonsterState.Wandering)
            {
                SwitchToTracking();
            }
        }
    }
    
    private void SwitchToTracking()
    {
        currentState = MonsterState.Tracking;
        hasWanderTarget = false;
        Debug.Log("怪物切换到追踪状态");
    }
    
    private void SwitchToWandering()
    {
        currentState = MonsterState.Wandering;
        hasWanderTarget = false;
        SetNewWanderPoint();
        Debug.Log("怪物切换到漫游状态");
    }
    
    private void TrackingUpdate()
    {
        CheckPlayerState();
        
        if (currentState != MonsterState.Tracking) return;
        if (player == null) return;
        
        // 设置寻路目标为玩家位置
        aiPath.destination = player.position;
        
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
        
        // 检测是否到达当前巡逻点
        if (aiPath.reachedDestination || !hasWanderTarget)
        {
            SetNewWanderPoint();
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
        if (Time.time - lastAttackTime < data.attackCooldown) return;
        
        lastAttackTime = Time.time;
        playerController.TakeDamage((int)data.attackDamage);
        Debug.Log("怪物攻击玩家");
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
