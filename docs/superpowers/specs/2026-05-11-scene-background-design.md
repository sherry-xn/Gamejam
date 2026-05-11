# 场景Prefab背景图片添加设计文档

## 概述

为Unity项目中的场景prefab添加背景图片功能，使用项目中已有的场景图片资源。

## 目标

- 为特定场景prefab添加静态背景图片
- 使用项目中已有的图片资源
- 保持良好的代码组织结构

## 设计方案

### 实现方法

采用**创建专门的Background子对象**方法：

- 在每个场景prefab中创建名为"Background"的子对象
- 为Background对象添加SpriteRenderer组件
- 设置对应的背景图片和合适的渲染层级

### 资源位置

图片资源位于：`Assets/Sprites/场景/` 目录

### 场景与图片对应关系

| 场景Prefab | 背景图片 |
|------------|----------|
| Classroom.prefab | Classroom.png |
| Corridor1.prefab | Corridor1.png |
| Corridor2.prefab | Corridor2.jpg |
| Hall.prefab | Hall.png |
| Toilet.prefab | Toilet.png |

### 实现步骤

1. **打开场景prefab编辑模式**
   - 在Unity编辑器中选择目标prefab
   - 进入prefab编辑模式

2. **创建Background子对象**
   - 在prefab根对象下创建新的空GameObject
   - 命名为"Background"

3. **添加SpriteRenderer组件**
   - 为Background对象添加SpriteRenderer组件
   - 设置对应的背景图片

4. **调整属性**
   - 设置合适的位置（通常在场景中心）
   - 设置合适的缩放（覆盖整个场景）
   - 设置渲染层级（确保在其他元素下方）

5. **保存prefab**
   - 保存对prefab的修改

### 技术细节

**SpriteRenderer设置：**
- Sprite：对应的场景图片
- Color：白色（不改变图片颜色）
- Sorting Layer：使用默认层
- Order in Layer：设置为-100（确保在其他元素下方）

**Transform设置：**
- Position：{x: 0, y: 0, z: 0}（场景中心）
- Scale：{x: 30, y: 30, z: 1}（覆盖整个场景）
- Rotation：{x: 0, y: 0, z: 0}（保持默认）

## 验证标准

- 每个场景prefab都有对应的Background子对象
- Background对象正确显示对应的场景图片
- 背景图片在正确的渲染层级
- 不影响现有场景功能

## 依赖项

- Unity编辑器
- 项目中已有的场景图片资源

## 风险与缓解

**风险1：图片尺寸不匹配**
- 缓解：根据场景大小调整Background对象的Scale

**风险2：渲染层级冲突**
- 缓解：使用负的Order in Layer值确保背景在最底层

## 后续工作

- 测试所有场景的背景显示效果
- 根据需要调整背景图片的位置和缩放
- 考虑是否需要为不同分辨率提供不同背景图片