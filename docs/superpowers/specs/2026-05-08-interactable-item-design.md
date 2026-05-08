# InteractableItem 组件设计

## 背景

当前有两种需要统一的交互物体：
1. **旅行袋（TravelBag）** — 已实现，按 E 拾取子物体 Key，逻辑硬编码在 PlayerController
2. **线索物品（纸条、日记）** — 未实现，交互后显示故事内容

两类交互的共同点：一次性触发式交互（按 E 触发行为，不进入/退出状态）。与 Hideable（进入/退出状态）不同。

## 设计

### 新建：`Assets/Scripts/Interaction/InteractableItem.cs`

```csharp
using UnityEngine;
using UnityEngine.Events;

public class InteractableItem : MonoBehaviour
{
    [SerializeField] private UnityEvent<PlayerController> onInteracted;

    public void OnInteracted(PlayerController player)
    {
        onInteracted?.Invoke(player);
    }
}
```

- `onInteracted`：Inspector 里拖拽绑定任意行为
- 泛型 `UnityEvent<PlayerController>`：回调可访问玩家实例（如调用 `player.AddKey()`）
- 无 tag 依赖，无硬编码逻辑

### 修改：`Assets/Scripts/Player/PlayerController.cs`

**新增方法：**
```csharp
public bool TryGetInteractableItemInRange(out InteractableItem item)
```
- 复用 `interactionOverlapArray`，`GetComponent<InteractableItem>()` 检测
- 与 `TryGetHideableInRange` 模式一致

**修改 `OnInteractionStarted`：**
- Hideable 检测之后、Door 检测之前，插入 InteractableItem 分支
- 检测到 → `item.OnInteracted(this)`，return

**修改 `UpdateInteractionIcon`：**
- 加入 `TryGetInteractableItemInRange(out _)` 条件

**移除：**
- `[SerializeField] private string travelBagTag` 字段
- `InteractWithTravelBag(GameObject)` 方法
- `UpdateInteractionIcon` 中 `TryGetObjectInRange(travelBagTag, out _)` 调用
- `OnInteractionStarted` 中 `TryGetObjectInRange(travelBagTag, out targetObject)` 分支

### 修改：`Assets/Scripts/TravelBag.cs`

新增公开方法：

```csharp
public void PickupKey(PlayerController player)
{
    var keyT = transform.Find("Key");
    if (keyT == null || !keyT.gameObject.activeSelf) return;
    keyT.gameObject.SetActive(false);
    player.AddKey();
}
```

从 PlayerController 迁移过来的拾取逻辑，保持 TravelBag 自身行为内聚。

### 场景配置（手动）

**TravelBag 物体：**
1. 确保有 `TravelBag` 组件（已有）
2. 添加 `InteractableItem` 组件
3. `onInteracted` → 拖 TravelBag 自身 → 选 `TravelBag.PickupKey`

**线索物品（纸条/日记）：**
1. 挂 `InteractableItem` 组件
2. `onInteracted` → 绑定显示 UI 的逻辑（如 `StoryPanel.Show(string)`）

## 交互优先级（OnInteractionStarted 顺序）

1. Hideable（进入/退出状态，禁用移动）
2. **InteractableItem（一次性触发）** ← 新增
3. Door（开关门）

## 与 Hideable 的对比

| | InteractableItem | Hideable |
|---|---|---|
| 交互方式 | 一次性触发 | 进入/退出状态 |
| 玩家移动 | 不影响 | 禁用移动 |
| 检测方式 | GetComponent | GetComponent |
| 配置方式 | UnityEvent | hidePoint + alpha |

## 约束

- UnityEvent 在 Inspector 里配置，代码不硬编码具体行为
- `player` 参数可选使用（线索物品可能不需要玩家引用）
- InteractableItem 不管理状态，触发后即完成
