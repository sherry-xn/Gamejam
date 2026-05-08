using UnityEditor;
using UnityEngine;
using Cinemachine;
using System.Collections.Generic;
using System.Linq;

public class CameraBoundsSetup : EditorWindow
{
    private PolygonCollider2D boundsCollider;
    private string prefabFolder = "Assets/Prefabs";
    private string[] wallKeywords = { "boundary", "Wall", "wall" };
    private float cameraPadding = 0.5f;
    private float maxWallSize = 50f;

    [MenuItem("Tools/相机边界设置")]
    public static void ShowWindow()
    {
        GetWindow<CameraBoundsSetup>("相机边界设置");
    }

    private void OnGUI()
    {
        GUILayout.Label("相机边界自动配置", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        GUILayout.Label("第0步：给 Prefab 墙体添加碰撞体", EditorStyles.boldLabel);
        prefabFolder = EditorGUILayout.TextField("Prefab 文件夹", prefabFolder);
        if (GUILayout.Button("扫描并添加碰撞体到 Prefab"))
        {
            AddCollidersToPrefabs();
        }

        EditorGUILayout.Space(10);

        GUILayout.Label("第1步：生成相机边界", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "每个房间/区域生成一个矩形路径，\n" +
            "合并到 PolygonCollider2D 中。",
            MessageType.Info);

        cameraPadding = EditorGUILayout.FloatField("边界内缩量", cameraPadding);
        maxWallSize = EditorGUILayout.FloatField("最大墙体尺寸", maxWallSize);
        EditorGUILayout.HelpBox("超过此尺寸的 boundary sprite 会被跳过（避免超大 sprite 拉大矩形）", MessageType.None);

        if (GUILayout.Button("一键生成相机边界"))
        {
            GenerateCameraBounds();
        }

        EditorGUILayout.Space(10);
        boundsCollider = (PolygonCollider2D)EditorGUILayout.ObjectField(
            "当前边界碰撞体", boundsCollider, typeof(PolygonCollider2D), true);

        EditorGUILayout.Space(20);

        // ========== 第2步：房间切换系统 ==========
        GUILayout.Label("第2步：房间切换系统", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "生成独立房间边界 + 门 Trigger + CameraRoomManager，\n" +
            "实现玩家跨房间时相机平滑过渡。",
            MessageType.Info);

        if (GUILayout.Button("2a. 生成独立房间边界"))
        {
            GenerateIndividualRoomBounds();
        }

        if (GUILayout.Button("2b. 生成门 Trigger"))
        {
            GenerateDoorTriggers();
        }

        if (GUILayout.Button("2c. 配置 CameraRoomManager"))
        {
            SetupCameraRoomManager();
        }
    }

    private void AddCollidersToPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        if (prefabGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", $"在 {prefabFolder} 中未找到任何 Prefab", "确定");
            return;
        }

        int totalAdded = 0;
        int totalSkipped = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            Transform[] allChildren = prefab.GetComponentsInChildren<Transform>(true);
            bool prefabModified = false;

            foreach (Transform child in allChildren)
            {
                if (child == prefab.transform) continue;
                string objName = child.gameObject.name;

                bool isWall = false;
                foreach (string keyword in wallKeywords)
                {
                    if (objName.Contains(keyword)) { isWall = true; break; }
                }
                if (!isWall) continue;

                var sr = child.GetComponent<SpriteRenderer>();
                if (sr == null) continue;

                var existingCol = child.GetComponent<BoxCollider2D>();
                if (existingCol != null) { totalSkipped++; continue; }

                Sprite sprite = sr.sprite;
                Vector2 spriteSize;
                if (sprite != null && sprite.rect.width > 0 && sprite.rect.height > 0)
                {
                    spriteSize = new Vector2(
                        sprite.rect.width / sprite.pixelsPerUnit,
                        sprite.rect.height / sprite.pixelsPerUnit);
                }
                else
                {
                    spriteSize = sr.localBounds.size;
                }

                if (spriteSize.x < 0.01f || spriteSize.y < 0.01f)
                {
                    totalSkipped++;
                    continue;
                }

                var col = child.gameObject.AddComponent<BoxCollider2D>();
                col.size = spriteSize;
                col.offset = Vector2.zero;
                prefabModified = true;
                totalAdded++;
            }

            if (prefabModified) EditorUtility.SetDirty(prefab);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成",
            $"新增: {totalAdded}，已有: {totalSkipped}", "确定");
    }

    private void GenerateCameraBounds()
    {
        // 1. 收集墙体碰撞体，按所在 prefab 实例的根对象分组
        var allBoxCols = FindObjectsOfType<BoxCollider2D>();
        var allCompositeCols = FindObjectsOfType<CompositeCollider2D>();
        var groups = new Dictionary<Transform, List<Rect>>();
        var processedObjects = new HashSet<GameObject>();

        // 先处理 CompositeCollider2D（如果其名称匹配墙体关键词）
        foreach (var composite in allCompositeCols)
        {
            // 注意：CompositeCollider2D 的 isTrigger 可能为 true，
            // 但其子 BoxCollider2D 可能不是 trigger，所以不检查 isTrigger
            if (composite.GetComponentInParent<PlayerController>() != null) continue;
            if (composite.GetComponentInParent<MonsterController>() != null) continue;

            string name = composite.gameObject.name;
            bool isWall = false;
            foreach (string kw in wallKeywords)
            {
                if (name.Contains(kw)) { isWall = true; break; }
            }
            if (!isWall) continue;

            processedObjects.Add(composite.gameObject);

            Vector2 size = composite.bounds.size;
            if (size.x < 0.01f || size.y < 0.01f) continue;

            // 跳过尺寸过大的碰撞体
            if (size.x > maxWallSize || size.y > maxWallSize)
            {
                Debug.Log($"[跳过-过大-Composite] {name} root={composite.transform.root.name} size={size}");
                continue;
            }

            Transform root = composite.transform.root;
            if (!groups.ContainsKey(root))
                groups[root] = new List<Rect>();

            groups[root].Add(new Rect(
                composite.bounds.min.x, composite.bounds.min.y,
                size.x, size.y));

            Debug.Log($"[墙体-Composite] {name} root={root.name} center={composite.bounds.center} size={size}");
        }

        foreach (var col in allBoxCols)
        {
            if (col.isTrigger) continue;
            if (col.GetComponentInParent<PlayerController>() != null) continue;
            if (col.GetComponentInParent<MonsterController>() != null) continue;

            string name = col.gameObject.name;
            bool isWall = false;
            foreach (string kw in wallKeywords)
            {
                if (name.Contains(kw)) { isWall = true; break; }
            }
            if (!isWall) continue;

            // 检查是否是 CompositeCollider2D 的子碰撞体（已被上面处理过）
            var compositeParent = col.GetComponentInParent<CompositeCollider2D>();
            if (compositeParent != null)
            {
                // 如果父级 CompositeCollider2D 已被处理，跳过
                if (processedObjects.Contains(compositeParent.gameObject))
                    continue;

                // 否则使用 CompositeCollider2D 的 bounds
                processedObjects.Add(compositeParent.gameObject);
                Vector2 compositeSize = compositeParent.bounds.size;
                if (compositeSize.x >= 0.01f && compositeSize.y >= 0.01f &&
                    compositeSize.x <= maxWallSize && compositeSize.y <= maxWallSize)
                {
                    Transform parentRoot = compositeParent.transform.root;
                    if (!groups.ContainsKey(parentRoot))
                        groups[parentRoot] = new List<Rect>();

                    groups[parentRoot].Add(new Rect(
                        compositeParent.bounds.min.x, compositeParent.bounds.min.y,
                        compositeSize.x, compositeSize.y));

                    Debug.Log($"[墙体-Composite子] {compositeParent.gameObject.name} root={parentRoot.name} center={compositeParent.bounds.center} size={compositeSize}");
                }
                continue;
            }

            Vector2 size = col.bounds.size;
            if (size.x < 0.01f || size.y < 0.01f) continue;

            // 跳过尺寸过大的 boundary sprite（视觉装饰用，不适合做碰撞边界）
            if (size.x > maxWallSize || size.y > maxWallSize)
            {
                Debug.Log($"[跳过-过大] {name} root={col.transform.root.name} size={size}");
                continue;
            }

            // 找到所属 prefab 实例的根对象
            Transform root = col.transform.root;

            if (!groups.ContainsKey(root))
                groups[root] = new List<Rect>();

            groups[root].Add(new Rect(
                col.bounds.min.x, col.bounds.min.y,
                size.x, size.y));

            Debug.Log($"[墙体] {name} root={root.name} center={col.bounds.center} size={size}");
        }

        Debug.Log($"[相机边界] 有效墙体: {groups.Values.Sum(g => g.Count)}，分组: {groups.Count}");

        if (groups.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "未找到有效墙体。请先执行第0步。", "确定");
            return;
        }

        // 2. 每个 prefab 实例计算 AABB 矩形
        var allPaths = new List<Vector2[]>();

        foreach (var kvp in groups)
        {
            string rootName = kvp.Key.name;
            var rects = kvp.Value;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var r in rects)
            {
                if (r.xMin < minX) minX = r.xMin;
                if (r.yMin < minY) minY = r.yMin;
                if (r.xMax > maxX) maxX = r.xMax;
                if (r.yMax > maxY) maxY = r.yMax;
            }

            minX += cameraPadding;
            minY += cameraPadding;
            maxX -= cameraPadding;
            maxY -= cameraPadding;

            if (maxX - minX < 0.1f || maxY - minY < 0.1f)
            {
                Debug.LogWarning($"[{rootName}] 矩形太小，跳过");
                continue;
            }

            var rect = new Vector2[]
            {
                new Vector2(minX, minY),
                new Vector2(maxX, minY),
                new Vector2(maxX, maxY),
                new Vector2(minX, maxY)
            };

            allPaths.Add(rect);
            Debug.Log($"[{rootName}] 矩形: ({minX:F1},{minY:F1}) - ({maxX:F1},{maxY:F1}) size=({maxX-minX:F1},{maxY-minY:F1})");
        }

        if (allPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "未能生成有效矩形", "确定");
            return;
        }

        // 4. 创建 PolygonCollider2D（多路径）
        var boundsGo = GameObject.Find("CameraBounds");
        if (boundsGo == null)
        {
            boundsGo = new GameObject("CameraBounds");
            Undo.RegisterCreatedObjectUndo(boundsGo, "Create CameraBounds");
        }

        boundsCollider = boundsGo.GetComponent<PolygonCollider2D>();
        if (boundsCollider == null)
            boundsCollider = Undo.AddComponent<PolygonCollider2D>(boundsGo);

        boundsCollider.isTrigger = true;
        boundsCollider.pathCount = allPaths.Count;
        for (int i = 0; i < allPaths.Count; i++)
        {
            boundsCollider.SetPath(i, allPaths[i]);
        }

        // 5. 配置 VirtualCamera
        var vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam == null)
        {
            EditorUtility.DisplayDialog("错误", "场景中未找到 CinemachineVirtualCamera", "确定");
            return;
        }

        var confiner = vcam.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
            confiner = Undo.AddComponent<CinemachineConfiner2D>(vcam.gameObject);

        confiner.m_BoundingShape2D = boundsCollider;
        confiner.m_Damping = 0.5f;
        confiner.m_MaxWindowSize = 0;
        confiner.InvalidateCache();

        // 6. 清理旧 Composite
        var old = GameObject.Find("CameraComposite");
        if (old != null) Undo.DestroyObjectImmediate(old);

        Selection.activeGameObject = boundsGo;

        EditorUtility.DisplayDialog("完成",
            $"相机边界生成完成！\n\n" +
            $"房间区域: {allPaths.Count} 个\n\n" +
            $"请在 Scene 视图中查看 CameraBounds。",
            "确定");
    }

    // ========== 第2步：房间切换系统 ==========

    private static readonly string[] knownRoomNames = {
        "Corridor1", "Hall", "Corridor2", "Classroom", "Toilet", "Dorm"
    };

    private void GenerateIndividualRoomBounds()
    {
        // 查找现有的 CameraBounds
        var boundsGo = GameObject.Find("CameraBounds");
        if (boundsGo == null)
        {
            EditorUtility.DisplayDialog("错误", "请先执行「第1步」生成相机边界。", "确定");
            return;
        }

        var sourceCollider = boundsGo.GetComponent<PolygonCollider2D>();
        if (sourceCollider == null || sourceCollider.pathCount == 0)
        {
            EditorUtility.DisplayDialog("错误", "CameraBounds 上没有有效的 PolygonCollider2D 路径。", "确定");
            return;
        }

        // 匹配房间名：按 prefab 根对象名匹配路径
        var roomPaths = MatchRoomPaths(sourceCollider);

        // 创建每个房间的独立 GameObject
        int created = 0;
        foreach (var kvp in roomPaths)
        {
            string roomName = kvp.Key;
            Vector2[] path = kvp.Value;

            string goName = $"RoomBounds_{roomName}";
            var roomGo = GameObject.Find(goName);
            if (roomGo == null)
            {
                roomGo = new GameObject(goName);
                Undo.RegisterCreatedObjectUndo(roomGo, $"Create {goName}");
            }

            var roomCol = roomGo.GetComponent<PolygonCollider2D>();
            if (roomCol == null)
                roomCol = Undo.AddComponent<PolygonCollider2D>(roomGo);

            roomCol.isTrigger = true;
            roomCol.pathCount = 1;
            roomCol.SetPath(0, path);

            created++;
            Debug.Log($"[房间边界] {goName} 顶点数: {path.Length}");
        }

        EditorUtility.DisplayDialog("完成",
            $"生成了 {created} 个独立房间边界。\n\n" +
            $"每个 RoomBounds_Xxx 对象可在 Scene 视图中单独查看和编辑。",
            "确定");
    }

    private Dictionary<string, Vector2[]> MatchRoomPaths(PolygonCollider2D source)
    {
        var result = new Dictionary<string, Vector2[]>();

        // 方法1：按场景中 prefab 根对象名匹配路径中心
        var rootMap = new Dictionary<string, Transform>();
        foreach (var root in FindObjectsOfType<Transform>())
        {
            if (root.parent != null) continue; // 只看根对象
            foreach (var name in knownRoomNames)
            {
                if (root.name.Contains(name))
                {
                    rootMap[name] = root;
                    break;
                }
            }
        }

        // 对每条路径，找到中心点最近的 prefab 根对象
        for (int i = 0; i < source.pathCount; i++)
        {
            Vector2[] path = source.GetPath(i);
            if (path.Length < 3) continue;

            // 计算路径中心
            Vector2 center = Vector2.zero;
            foreach (var p in path) center += p;
            center /= path.Length;

            // 找最近的房间
            string bestRoom = null;
            float bestDist = float.MaxValue;

            foreach (var kvp in rootMap)
            {
                // 计算 prefab 实例的 AABB 中心
                var renderers = kvp.Value.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;

                Bounds bounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++)
                    bounds.Encapsulate(renderers[r].bounds);

                float dist = Vector2.Distance(center, (Vector2)bounds.center);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestRoom = kvp.Key;
                }
            }

            if (bestRoom != null && !result.ContainsKey(bestRoom))
            {
                result[bestRoom] = path;
                Debug.Log($"[匹配] 路径{i} center={center} → {bestRoom} (距离={bestDist:F1})");
            }
            else
            {
                // fallback：用路径索引命名
                string fallback = $"Room{i}";
                result[fallback] = path;
                Debug.Log($"[匹配] 路径{i} center={center} → {fallback} (未匹配到已知房间)");
            }
        }

        return result;
    }

    private void GenerateDoorTriggers()
    {
        // 查找所有门对象（名称含 "door"/"Door"）
        var allTransforms = FindObjectsOfType<Transform>();
        var doorObjects = new List<Transform>();

        foreach (var t in allTransforms)
        {
            string name = t.gameObject.name;
            if (name.Contains("door") || name.Contains("Door"))
            {
                // 排除非门对象（如 DoorTrigger_ 开头的已生成对象）
                if (name.StartsWith("DoorTrigger_")) continue;
                // 排除 Doors 父容器（只处理具体门对象）
                if (name == "Doors") continue;
                // 需要有 SpriteRenderer 或 Collider2D
                if (t.GetComponent<SpriteRenderer>() == null && t.GetComponent<Collider2D>() == null) continue;
                doorObjects.Add(t);
            }
        }

        Debug.Log($"[门Trigger] 找到 {doorObjects.Count} 个门对象");

        // 获取房间边界信息（用于判断门属于哪个房间）
        var roomBounds = new Dictionary<string, PolygonCollider2D>();
        foreach (var name in knownRoomNames)
        {
            var go = GameObject.Find($"RoomBounds_{name}");
            if (go != null)
            {
                var col = go.GetComponent<PolygonCollider2D>();
                if (col != null) roomBounds[name] = col;
            }
        }

        if (roomBounds.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先执行「2a. 生成独立房间边界」。", "确定");
            return;
        }

        int created = 0;
        var triggerParent = GameObject.Find("DoorTriggers");
        if (triggerParent == null)
        {
            triggerParent = new GameObject("DoorTriggers");
            Undo.RegisterCreatedObjectUndo(triggerParent, "Create DoorTriggers");
        }
        else
        {
            // 清除已有的 DoorTrigger 子对象
            var children = new List<GameObject>();
            foreach (Transform child in triggerParent.transform)
                children.Add(child.gameObject);
            foreach (var child in children)
                Undo.DestroyObjectImmediate(child);
            Debug.Log($"[门Trigger] 已清除 {children.Count} 个旧 Trigger");
        }

        foreach (var door in doorObjects)
        {
            Vector3 doorPos = door.position;

            // 找到门所在的房间（用 OverlapPoint 检测）
            string fromRoom = null;
            foreach (var kvp in roomBounds)
            {
                if (kvp.Value.OverlapPoint((Vector2)doorPos))
                {
                    fromRoom = kvp.Key;
                    break;
                }
            }

            // 如果门不在任何房间内，用最近的房间
            if (fromRoom == null)
            {
                float minDist = float.MaxValue;
                foreach (var kvp in roomBounds)
                {
                    Vector2 roomCenter = kvp.Value.bounds.center;
                    float dist = Vector2.Distance((Vector2)doorPos, roomCenter);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        fromRoom = kvp.Key;
                    }
                }
            }

            // 找到门朝向的目标房间（门通常在两个房间的边界上）
            // 在门位置附近扩展搜索
            string targetRoom = null;
            float searchRadius = 6f;
            foreach (var kvp in roomBounds)
            {
                if (kvp.Key == fromRoom) continue;
                // 检查门附近的点是否在另一个房间内
                Vector2[] offsets = {
                    new Vector2(searchRadius, 0),
                    new Vector2(-searchRadius, 0),
                    new Vector2(0, searchRadius),
                    new Vector2(0, -searchRadius)
                };
                foreach (var offset in offsets)
                {
                    if (kvp.Value.OverlapPoint((Vector2)doorPos + offset))
                    {
                        targetRoom = kvp.Key;
                        break;
                    }
                }
                if (targetRoom != null) break;
            }

            // 创建 Trigger（门位置创建一个 BoxCollider2D）
            string triggerName = $"DoorTrigger_{door.name}";
            var triggerGo = new GameObject(triggerName);
            Undo.RegisterCreatedObjectUndo(triggerGo, $"Create {triggerName}");
            triggerGo.transform.SetParent(triggerParent.transform);
            triggerGo.transform.position = doorPos;

            var boxCol = triggerGo.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector2(2f, 2f);

            // 添加 RoomDoorTrigger 组件（但需要在运行时绑定）
            // 编辑器模式下只创建结构，运行时由 SetupCameraRoomManager 绑定

            created++;
            Debug.Log($"[门Trigger] {triggerName} pos={doorPos} from={fromRoom} target={targetRoom}");
        }

        EditorUtility.DisplayDialog("完成",
            $"创建了 {created} 个门 Trigger。\n\n" +
            $"请在 Inspector 中检查每个 DoorTrigger 的目标房间设置。\n" +
            $"然后点击「2c. 配置 CameraRoomManager」。",
            "确定");
    }

    private void SetupCameraRoomManager()
    {
        // 1. 查找原始 Virtual Camera 作为模板
        var templateVcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (templateVcam == null)
        {
            EditorUtility.DisplayDialog("错误", "场景中未找到 VirtualCamera。", "确定");
            return;
        }

        // 2. 收集房间边界
        var roomBoundaries = new Dictionary<string, PolygonCollider2D>();
        foreach (var name in knownRoomNames)
        {
            var go = GameObject.Find($"RoomBounds_{name}");
            if (go == null) continue;
            var col = go.GetComponent<PolygonCollider2D>();
            if (col != null) roomBoundaries[name] = col;
        }

        if (roomBoundaries.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "未找到任何 RoomBounds_Xxx。请先执行「2a」。", "确定");
            return;
        }

        // 3. 为每个房间创建 Virtual Camera + Confiner2D
        var roomCamerasParent = GameObject.Find("RoomCameras");
        if (roomCamerasParent == null)
        {
            roomCamerasParent = new GameObject("RoomCameras");
            Undo.RegisterCreatedObjectUndo(roomCamerasParent, "Create RoomCameras");
        }

        var roomCameraList = new List<CameraRoomManager.RoomCamera>();

        foreach (var kvp in roomBoundaries)
        {
            string roomName = kvp.Key;
            PolygonCollider2D boundary = kvp.Value;

            string camName = $"VCam_{roomName}";
            var camGo = GameObject.Find(camName);
            if (camGo == null)
            {
                camGo = new GameObject(camName);
                Undo.RegisterCreatedObjectUndo(camGo, $"Create {camName}");
            }
            camGo.transform.SetParent(roomCamerasParent.transform);

            // 复制模板相机的组件
            var vcam = camGo.GetComponent<CinemachineVirtualCamera>();
            if (vcam == null)
                vcam = Undo.AddComponent<CinemachineVirtualCamera>(camGo);

            // 配置 Virtual Camera
            vcam.m_Priority = 0; // 默认低优先级
            vcam.m_Follow = templateVcam.m_Follow;
            vcam.m_LookAt = templateVcam.m_LookAt;
            vcam.m_Lens = templateVcam.m_Lens;

            // 添加 Confiner2D
            var confiner = camGo.GetComponent<CinemachineConfiner2D>();
            if (confiner == null)
                confiner = Undo.AddComponent<CinemachineConfiner2D>(camGo);

            confiner.m_BoundingShape2D = boundary;
            confiner.m_Damping = 0.5f;
            confiner.m_MaxWindowSize = 0;
            confiner.InvalidateCache();

            roomCameraList.Add(new CameraRoomManager.RoomCamera
            {
                roomName = roomName,
                vcam = vcam
            });

            Debug.Log($"[房间相机] {camName} → {boundary.name}");
        }

        // 4. 恢复原始 Virtual Camera 的 Confiner2D
        templateVcam.Priority = 10; // 恢复原始优先级
        var originalConfiner = templateVcam.GetComponent<CinemachineConfiner2D>();
        if (originalConfiner != null)
        {
            // 确保 Confiner2D 指向 CameraBounds
            var cameraBoundsGo = GameObject.Find("CameraBounds");
            if (cameraBoundsGo != null)
            {
                originalConfiner.m_BoundingShape2D = cameraBoundsGo.GetComponent<PolygonCollider2D>();
                originalConfiner.m_Damping = 0.5f;
                originalConfiner.m_MaxWindowSize = 0;
                originalConfiner.InvalidateCache();
            }
            originalConfiner.enabled = true;
        }

        // 删除之前创建的房间相机
        if (roomCamerasParent != null)
            Undo.DestroyObjectImmediate(roomCamerasParent);

        // 5. 创建或更新 CameraRoomManager
        var managerObj = GameObject.Find("CameraRoomManager");
        if (managerObj == null)
        {
            managerObj = new GameObject("CameraRoomManager");
            Undo.RegisterCreatedObjectUndo(managerObj, "Create CameraRoomManager");
        }

        var manager = managerObj.GetComponent<CameraRoomManager>();
        if (manager == null)
            manager = Undo.AddComponent<CameraRoomManager>(managerObj);

        manager.roomCameras = roomCameraList.ToArray();

        // 6. 绑定 DoorTrigger
        var triggerParent = GameObject.Find("DoorTriggers");
        if (triggerParent != null)
        {
            int bound = 0;
            foreach (Transform child in triggerParent.transform)
            {
                var trigger = child.GetComponent<RoomDoorTrigger>();
                if (trigger == null)
                    trigger = Undo.AddComponent<RoomDoorTrigger>(child.gameObject);

                trigger.roomManager = manager;

                // 推断目标房间：找门附近的另一个房间
                Vector2 pos = (Vector2)child.position;
                string targetRoom = null;

                // 先找门所在的房间
                string fromRoom = null;
                foreach (var kvp in roomBoundaries)
                {
                    if (kvp.Value.OverlapPoint(pos))
                    {
                        fromRoom = kvp.Key;
                        break;
                    }
                }

                // 找目标房间：附近 6 米内另一个房间
                float searchRadius = 6f;
                foreach (var kvp in roomBoundaries)
                {
                    if (kvp.Key == fromRoom) continue;
                    Vector2[] offsets = {
                        new Vector2(searchRadius, 0),
                        new Vector2(-searchRadius, 0),
                        new Vector2(0, searchRadius),
                        new Vector2(0, -searchRadius)
                    };
                    foreach (var offset in offsets)
                    {
                        if (kvp.Value.OverlapPoint(pos + offset))
                        {
                            targetRoom = kvp.Key;
                            break;
                        }
                    }
                    if (targetRoom != null) break;
                }

                // Fallback：最近的非当前房间
                if (targetRoom == null)
                {
                    float minDist = float.MaxValue;
                    foreach (var kvp in roomBoundaries)
                    {
                        if (kvp.Key == fromRoom) continue;
                        float dist = Vector2.Distance(pos, kvp.Value.bounds.center);
                        if (dist < minDist) { minDist = dist; targetRoom = kvp.Key; }
                    }
                }

                trigger.targetRoom = targetRoom;
                bound++;
                Debug.Log($"[绑定] {child.name} from={fromRoom} target={targetRoom}");
            }
        }

        EditorUtility.DisplayDialog("完成",
            $"多相机方案配置完成！\n\n" +
            $"房间相机: {roomCameraList.Count} 个\n" +
            $"每个房间有独立的 VirtualCamera + Confiner2D\n\n" +
            $"运行游戏测试。",
            "确定");
    }

}
