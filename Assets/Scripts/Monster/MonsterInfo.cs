using System;
using UnityEngine;

[Serializable]
public class MonsterInfo
{
    [Header("移动设置")]
    [SerializeField] public float moveSpeed = 3f;
    
    [Header("追踪设置")]
    [SerializeField] public float trackingRange = 10f;
    [SerializeField] public float attackRange = 1.5f;
    [SerializeField] public float attackDamage = 1f;
    [SerializeField] public float attackCooldown = 1.5f;
    
    [Header("漫游设置")]
    [SerializeField] public float wanderRadius = 8f;
    [SerializeField] public float wanderInterval = 3f;
}
