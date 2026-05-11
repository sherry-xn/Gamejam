# 场景Prefab背景图片添加实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为Unity项目中的5个场景prefab添加静态背景图片

**Architecture:** 在每个场景prefab中创建Background子对象，使用SpriteRenderer组件显示对应的场景图片

**Tech Stack:** Unity编辑器, SpriteRenderer, Prefab系统

---

## 文件结构

**修改的文件：**
- `Assets/Prefabs/Classroom.prefab`
- `Assets/Prefabs/Corridor1.prefab`
- `Assets/Prefabs/Corridor2.prefab`
- `Assets/Prefabs/Hall.prefab`
- `Assets/Prefabs/Toilet.prefab`

**使用的资源：**
- `Assets/Sprites/场景/Classroom.png`
- `Assets/Sprites/场景/Corridor1.png`
- `Assets/Sprites/场景/Corridor2.jpg`
- `Assets/Sprites/场景/Hall.png`
- `Assets/Sprites/场景/Toilet.png`

---

### Task 1: 为Classroom.prefab添加背景图片

**Files:**
- Modify: `Assets/Prefabs/Classroom.prefab`

- [ ] **Step 1: 打开Classroom.prefab编辑模式**

在Unity编辑器中：
1. 在Project窗口中找到`Assets/Prefabs/Classroom.prefab`
2. 双击prefab进入编辑模式

- [ ] **Step 2: 创建Background子对象**

在prefab编辑模式中：
1. 在Hierarchy窗口中选择"Classroom"根对象
2. 右键点击选择"Create Empty Child"
3. 将新对象命名为"Background"

- [ ] **Step 3: 添加SpriteRenderer组件**

1. 选择"Background"对象
2. 在Inspector窗口中点击"Add Component"
3. 搜索并添加"SpriteRenderer"组件

- [ ] **Step 4: 设置背景图片**

1. 在SpriteRenderer组件中，点击"Sprite"字段旁边的圆圈图标
2. 在弹出的选择窗口中找到并选择`Assets/Sprites/场景/Classroom.png`
3. 确保Color设置为白色（r:1, g:1, b:1, a:1）

- [ ] **Step 5: 调整Transform属性**

1. 设置Position为{x: 0, y: 0, z: 0}
2. 设置Scale为{x: 30, y: 30, z: 1}
3. 设置Rotation为{x: 0, y: 0, z: 0}

- [ ] **Step 6: 设置渲染层级**

1. 在SpriteRenderer组件中，找到"Order in Layer"字段
2. 设置为-100

- [ ] **Step 7: 保存prefab**

1. 在prefab编辑窗口中点击"Save"按钮
2. 退出prefab编辑模式

- [ ] **Step 8: 验证背景显示**

1. 在Scene视图中查看Classroom场景
2. 确认背景图片正确显示
3. 确认背景在其他元素下方

---

### Task 2: 为Corridor1.prefab添加背景图片

**Files:**
- Modify: `Assets/Prefabs/Corridor1.prefab`

- [ ] **Step 1: 打开Corridor1.prefab编辑模式**

在Unity编辑器中：
1. 在Project窗口中找到`Assets/Prefabs/Corridor1.prefab`
2. 双击prefab进入编辑模式

- [ ] **Step 2: 创建Background子对象**

在prefab编辑模式中：
1. 在Hierarchy窗口中选择根对象
2. 右键点击选择"Create Empty Child"
3. 将新对象命名为"Background"

- [ ] **Step 3: 添加SpriteRenderer组件**

1. 选择"Background"对象
2. 在Inspector窗口中点击"Add Component"
3. 搜索并添加"SpriteRenderer"组件

- [ ] **Step 4: 设置背景图片**

1. 在SpriteRenderer组件中，点击"Sprite"字段旁边的圆圈图标
2. 在弹出的选择窗口中找到并选择`Assets/Sprites/场景/Corridor1.png`
3. 确保Color设置为白色（r:1, g:1, b:1, a:1）

- [ ] **Step 5: 调整Transform属性**

1. 设置Position为{x: 0, y: 0, z: 0}
2. 设置Scale为{x: 30, y: 30, z: 1}
3. 设置Rotation为{x: 0, y: 0, z: 0}

- [ ] **Step 6: 设置渲染层级**

1. 在SpriteRenderer组件中，找到"Order in Layer"字段
2. 设置为-100

- [ ] **Step 7: 保存prefab**

1. 在prefab编辑窗口中点击"Save"按钮
2. 退出prefab编辑模式

- [ ] **Step 8: 验证背景显示**

1. 在Scene视图中查看Corridor1场景
2. 确认背景图片正确显示
3. 确认背景在其他元素下方

---

### Task 3: 为Corridor2.prefab添加背景图片

**Files:**
- Modify: `Assets/Prefabs/Corridor2.prefab`

- [ ] **Step 1: 打开Corridor2.prefab编辑模式**

在Unity编辑器中：
1. 在Project窗口中找到`Assets/Prefabs/Corridor2.prefab`
2. 双击prefab进入编辑模式

- [ ] **Step 2: 创建Background子对象**

在prefab编辑模式中：
1. 在Hierarchy窗口中选择根对象
2. 右键点击选择"Create Empty Child"
3. 将新对象命名为"Background"

- [ ] **Step 3: 添加SpriteRenderer组件**

1. 选择"Background"对象
2. 在Inspector窗口中点击"Add Component"
3. 搜索并添加"SpriteRenderer"组件

- [ ] **Step 4: 设置背景图片**

1. 在SpriteRenderer组件中，点击"Sprite"字段旁边的圆圈图标
2. 在弹出的选择窗口中找到并选择`Assets/Sprites/场景/Corridor2.jpg`
3. 确保Color设置为白色（r:1, g:1, b:1, a:1）

- [ ] **Step 5: 调整Transform属性**

1. 设置Position为{x: 0, y: 0, z: 0}
2. 设置Scale为{x: 30, y: 30, z: 1}
3. 设置Rotation为{x: 0, y: 0, z: 0}

- [ ] **Step 6: 设置渲染层级**

1. 在SpriteRenderer组件中，找到"Order in Layer"字段
2. 设置为-100

- [ ] **Step 7: 保存prefab**

1. 在prefab编辑窗口中点击"Save"按钮
2. 退出prefab编辑模式

- [ ] **Step 8: 验证背景显示**

1. 在Scene视图中查看Corridor2场景
2. 确认背景图片正确显示
3. 确认背景在其他元素下方

---

### Task 4: 为Hall.prefab添加背景图片

**Files:**
- Modify: `Assets/Prefabs/Hall.prefab`

- [ ] **Step 1: 打开Hall.prefab编辑模式**

在Unity编辑器中：
1. 在Project窗口中找到`Assets/Prefabs/Hall.prefab`
2. 双击prefab进入编辑模式

- [ ] **Step 2: 创建Background子对象**

在prefab编辑模式中：
1. 在Hierarchy窗口中选择根对象
2. 右键点击选择"Create Empty Child"
3. 将新对象命名为"Background"

- [ ] **Step 3: 添加SpriteRenderer组件**

1. 选择"Background"对象
2. 在Inspector窗口中点击"Add Component"
3. 搜索并添加"SpriteRenderer"组件

- [ ] **Step 4: 设置背景图片**

1. 在SpriteRenderer组件中，点击"Sprite"字段旁边的圆圈图标
2. 在弹出的选择窗口中找到并选择`Assets/Sprites/场景/Hall.png`
3. 确保Color设置为白色（r:1, g:1, b:1, a:1）

- [ ] **Step 5: 调整Transform属性**

1. 设置Position为{x: 0, y: 0, z: 0}
2. 设置Scale为{x: 30, y: 30, z: 1}
3. 设置Rotation为{x: 0, y: 0, z: 0}

- [ ] **Step 6: 设置渲染层级**

1. 在SpriteRenderer组件中，找到"Order in Layer"字段
2. 设置为-100

- [ ] **Step 7: 保存prefab**

1. 在prefab编辑窗口中点击"Save"按钮
2. 退出prefab编辑模式

- [ ] **Step 8: 验证背景显示**

1. 在Scene视图中查看Hall场景
2. 确认背景图片正确显示
3. 确认背景在其他元素下方

---

### Task 5: 为Toilet.prefab添加背景图片

**Files:**
- Modify: `Assets/Prefabs/Toilet.prefab`

- [ ] **Step 1: 打开Toilet.prefab编辑模式**

在Unity编辑器中：
1. 在Project窗口中找到`Assets/Prefabs/Toilet.prefab`
2. 双击prefab进入编辑模式

- [ ] **Step 2: 创建Background子对象**

在prefab编辑模式中：
1. 在Hierarchy窗口中选择根对象
2. 右键点击选择"Create Empty Child"
3. 将新对象命名为"Background"

- [ ] **Step 3: 添加SpriteRenderer组件**

1. 选择"Background"对象
2. 在Inspector窗口中点击"Add Component"
3. 搜索并添加"SpriteRenderer"组件

- [ ] **Step 4: 设置背景图片**

1. 在SpriteRenderer组件中，点击"Sprite"字段旁边的圆圈图标
2. 在弹出的选择窗口中找到并选择`Assets/Sprites/场景/Toilet.png`
3. 确保Color设置为白色（r:1, g:1, b:1, a:1）

- [ ] **Step 5: 调整Transform属性**

1. 设置Position为{x: 0, y: 0, z: 0}
2. 设置Scale为{x: 30, y: 30, z: 1}
3. 设置Rotation为{x: 0, y: 0, z: 0}

- [ ] **Step 6: 设置渲染层级**

1. 在SpriteRenderer组件中，找到"Order in Layer"字段
2. 设置为-100

- [ ] **Step 7: 保存prefab**

1. 在prefab编辑窗口中点击"Save"按钮
2. 退出prefab编辑模式

- [ ] **Step 8: 验证背景显示**

1. 在Scene视图中查看Toilet场景
2. 确认背景图片正确显示
3. 确认背景在其他元素下方

---

## 验证测试

完成所有任务后，进行以下验证：

- [ ] **验证所有场景都有Background子对象**
  1. 在Unity编辑器中打开每个场景prefab
  2. 确认每个prefab都有名为"Background"的子对象

- [ ] **验证背景图片正确显示**
  1. 在Scene视图中查看每个场景
  2. 确认背景图片正确显示且无拉伸变形

- [ ] **验证渲染层级正确**
  1. 确认背景图片在其他场景元素下方
  2. 确认不影响现有场景功能

- [ ] **运行游戏测试**
  1. 运行游戏
  2. 访问每个场景
  3. 确认背景图片正常显示