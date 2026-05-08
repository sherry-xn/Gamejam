# Hideable 组件实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [`) syntax for tracking.

**Goal:** 将可躲藏物体（Bed、Wardrobe）的交互逻辑从 PlayerController 解耦到独立的 Hideable 组件。

**Architecture:** 新建 Hideable MonoBehaviour 组件，挂载在可躲藏物体上，持有 hidePoint 引用。PlayerController 通过 GetComponent<Hideable> 检测范围内可躲藏物体，调用组件的 OnEnter/OnExit 方法。

**Tech Stack:** Unity 2D, C#

---

## 文件变更清单

| 文件 | 操作 |
|------|------|
| `Assets/Scripts/Interaction/Hideable.cs` | 新建 |
| `Assets/Scripts/Player/PlayerController.cs` | 修改 |

---

### Task 1: 创建 Hideable 组件

**Files:**
- Create: `Assets/Scripts/Interaction/Hideable.cs`

- [ ] **Step 1: 创建 Hideable.cs**

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

- [ ] **Step 2: 确认文件创建成功**

Run: `Test-Path "Assets\Scripts\Interaction\Hideable.cs"`
Expected: True

---

### Task 2: 修改 PlayerController — 新增检测方法

**Files:**
- Modify: `Assets/Scripts/Player/PlayerController.cs`

- [ ] **Step 1: 将 SetPlayerPosition 改为 public**

将 `PlayerController.cs:466` 的 `private void SetPlayerPosition` 改为 `public void SetPlayerPosition`，供 Hideable 调用。

- [ ] **Step 2: 新增 TryGetHideableInRange 方法**

在 `TryGetObjectInRange` 方法之后添加：

```csharp
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
```

---

### Task 3: 修改 PlayerController — 更新交互逻辑

**Files:**
- Modify: `Assets/Scripts/Player/PlayerController.cs`

- [ ] **Step 1: 移除 bedTag 和 wardrobeTag 字段**

删除 `PlayerController.cs:32-33` 的：
```csharp
[SerializeField] private string bedTag = "Bed";
```
和
```csharp
[SerializeField] private string wardrobeTag = "Wardrobe";
```

- [ ] **Step 2: 重构 OnInteractionStarted**

将 `OnInteractionStarted` 中的 bed/wardrobe if-blocks 替换为统一的 Hideable 检测：

```csharp
private void OnInteractionStarted(InputAction.CallbackContext obj)
{
    if (isInteracting)
    {
        if (targetObject == null) return;
        var hideable = targetObject.GetComponent<Hideable>();
        if (hideable != null) hideable.OnExit(this);
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
```

- [ ] **Step 3: 移除 StartInteraction 和 ExitInteraction 方法**

删除 `PlayerController.cs:259-293` 的 `StartInteraction` 和 `ExitInteraction` 方法（已被 Hideable 组件替代）。

- [ ] **Step 4: 更新 UpdateInteractionIcon**

将 `UpdateInteractionIcon` 中的 bed/wardrobe tag 检测替换为：

```csharp
private void UpdateInteractionIcon()
{
    if (InteractionIcon == null) return;
    bool show = isInteracting
        || TryGetHideableInRange(out _)
        || TryGetObjectInRange(travelBagTag, out _)
        || TryGetObjectInRange(doorTag, out _);
    InteractionIcon.gameObject.SetActive(show);
}
```

---

### Task 4: 清理与验证

- [ ] **Step 1: 检查 PlayerController 中无残留的 bed/wardrobe 引用**

Grep: `bedTag\|wardrobeTag\|bedPoint\|wardrobePoint` in `Assets/Scripts/Player/PlayerController.cs`
Expected: 无匹配

- [ ] **Step 2: 检查 Hideable.cs 无编译错误**

检查代码中的类型引用是否正确（PlayerController、Collider2D、SpriteRenderer）

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Interaction/Hideable.cs Assets/Scripts/Player/PlayerController.cs
git commit -m "feat: extract hideable object logic into Hideable component"
```

---

## 场景配置说明（手动）

完成代码后需要在 Unity Editor 中：
1. 在 Bed 物体上添加 `Hideable` 组件，将 `bedPoint` 子物体拖到 `hidePoint` 字段
2. 在 Wardrobe 物体上添加 `Hideable` 组件，将 `wardrobePoint` 子物体拖到 `hidePoint` 字段
3. 删除 `bedPoint` / `wardrobePoint` 的旧 tag（可选）
