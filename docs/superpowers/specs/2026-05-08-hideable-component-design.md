# Hideable 组件设计

## 背景

当前 Bed 和 Wardrobe 的躲藏逻辑完全一致（传送→禁用碰撞→半透明→锁定移动），但代码在 `PlayerController.cs` 里用两个独立 if-block 硬编码，区别仅在于 tag 名和子物体点名。新增可躲藏物体需要改 PlayerController 代码。

## 目标

将可躲藏物体的交互逻辑从 PlayerController 解耦到独立的 `Hideable` 组件，实现零代码新增可躲藏物体。

## 设计

### 1. 新建 `Assets/Scripts/Interaction/Hideable.cs`

```csharp
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hideable : MonoBehaviour
{
    [SerializeField] private Transform hidePoint;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0.7f;

    private Collider2D col;
    private SpriteRenderer sr;
    private float originalAlpha;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) originalAlpha = sr.color.a;
    }

    public void OnEnter(PlayerController player)
    {
        col.enabled = false;
        if (sr != null)
        {
            Color c = sr.color;
            c.a = hiddenAlpha;
            sr.color = c;
        }
        if (hidePoint != null)
            player.SetPlayerPosition(hidePoint.position);
    }

    public void OnExit(PlayerController player)
    {
        col.enabled = true;
        if (sr != null)
        {
            Color c = sr.color;
            c.a = originalAlpha;
            sr.color = c;
        }
    }
}
```

### 2. PlayerController 改动

**移除：**
- `bedTag`、`wardrobeTag` 字段
- `OnInteractionStarted` 中 bed/wardrobe 的独立 if-block
- `StartInteraction`、`ExitInteraction` 方法

**新增：**
- `TryGetHideableInRange(out Hideable hideable)` — 遍历范围内碰撞体，用 `GetComponent<Hideable>` 检测
- `OnInteractionStarted` 中统一调用 `TryGetHideableInRange`，调用 `hideable.OnEnter(this)` / `hideable.OnExit(this)`
- `UpdateInteractionIcon` 中用 `TryGetHideableInRange` 替代 bed/wardrobe 的 tag 检测

**`SetPlayerPosition` 改为 public**（供 Hideable 调用）

### 3. 场景配置

- Bed 和 Wardrobe 物体上挂 `Hideable` 组件
- 将原有的 `bedPoint` / `wardrobePoint` 子物体拖到 `hidePoint` 字段
- 保留物体的 Tag（可选，不再用于交互检测）

### 4. 不受影响的部分

- Door、TravelBag 逻辑保持不变
- MonsterController 通过 `playerController.IsInteracting` 读取状态，不受影响
- Input System 绑定不变

## 文件变更清单

| 文件 | 操作 |
|------|------|
| `Assets/Scripts/Interaction/Hideable.cs` | 新建 |
| `Assets/Scripts/Player/PlayerController.cs` | 修改 |
| 场景中 Bed/Wardrobe 物体 | 挂 Hideable 组件 |
