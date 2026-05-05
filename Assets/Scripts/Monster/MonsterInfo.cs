using System;
using UnityEngine;

[Serializable]
public class MonsterInfo
{
    [Header("移动设置")]
    [Range(0f, 100f)]
    public float moveSpeed = 3f;
    
    [Header("追踪设置")]
    [Range(0f, 100f)]
    public float trackingRange = 10f;
    [Range(0f, 100f)]
    public float attackRange = 1.5f;
    [Range(0f, 1000f)]
    public float attackDamage = 1f;
    [Range(0.01f, 60f)]
    public float attackCooldown = 1.5f;
    
    [Header("漫游设置")]
    [Range(0f, 100f)]
    public float wanderRadius = 8f;
    [Range(0f, 60f)]
    public float wanderInterval = 3f;
}
