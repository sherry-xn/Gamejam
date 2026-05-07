using UnityEditor;
using UnityEngine;
using Cinemachine;
using System.Collections.Generic;
using System.IO;

public class CameraBoundsSetup : EditorWindow
{
    private PolygonCollider2D boundsCollider;
    private CompositeCollider2D compositeCollider;
    private string wallsParentName = "Walls";
    private string prefabFolder = "Assets/Prefabs";
    private string[] wallKeywords = { "boundary", "Wall", "wall" };

    [MenuItem("Tools/相机边界设置")]
    public static void ShowWindow()
    {
        GetWindow<CameraBoundsSetup>("相机边界设置");
    }

    private void OnGUI()
    {
        GUILayout.Label("相机边界自动配置", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // ========== 第0步：给Prefab墙体添加碰撞体 ==========
        GUILayout.Label("第0步：给 Prefab 墙体添加碰撞体", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "扫描 Prefab 文件夹，给所有 boundary/Wall 对象自动添加 BoxCollider2D，\n" +
            "并设置 compositeOperation = Merge，使其可用于 CompositeCollider2D。",
            MessageType.Info);

        prefabFolder = EditorGUILayout.TextField("Prefab 文件夹", prefabFolder);

        if (GUILayout.Button("扫描并添加碰撞体到 Prefab"))
        {
            AddCollidersToPrefabs();
        }

        EditorGUILayout.Space(10);

        // ========== 第1步：CompositeCollider2D（推荐） ==========
        GUILayout.Label("第1步：配置 CompositeCollider2D", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "将场景中所有墙体 BoxCollider2D 合并为一个复杂形状，\n" +
            "Confiner2D 直接引用它，墙体变化时碰撞区域自动更新。",
            MessageType.Info);

        wallsParentName = EditorGUILayout.TextField("Walls 父对象名", wallsParentName);

        if (GUILayout.Button("一键配置 CompositeCollider2D"))
        {
            SetupCompositeConfiner();
        }

        EditorGUILayout.Space(20);

        // ========== 方案2：手动 PolygonCollider2D ==========
        GUILayout.Label("备选：手动 PolygonCollider2D", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "手动编辑多边形顶点，适合简单形状或精确控制。",
            MessageType.Info);

        boundsCollider = (PolygonCollider2D)EditorGUILayout.ObjectField(
            "边界碰撞体", boundsCollider, typeof(PolygonCollider2D), true);

        if (GUILayout.Button("创建 PolygonCollider2D 边界"))
        {
            CreatePolygonBounds();
        }

        if (GUILayout.Button("配置 VirtualCamera (Polygon)"))
        {
            SetupVirtualCameraPolygon();
        }

        EditorGUILayout.Space(20);

        // ========== 状态显示 ==========
        compositeCollider = (CompositeCollider2D)EditorGUILayout.ObjectField(
            "当前 Composite", compositeCollider, typeof(CompositeCollider2D), true);

        EditorGUILayout.Space();
        if (GUILayout.Button("刷新：选中场景中所有墙体"))
        {
            var walls = GameObject.Find(wallsParentName);
            if (walls != null)
            {
                Selection.activeGameObject = walls;
                Debug.Log($"已选中 {walls.name}，子对象数量：{walls.transform.childCount}");
            }
            else
            {
                EditorUtility.DisplayDialog("提示", $"未找到名为「{wallsParentName}」的对象", "确定");
            }
        }
    }

    private void AddCollidersToPrefabs()
    {
        // 查找所有 prefab
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        if (prefabGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", $"在 {prefabFolder} 中未找到任何 Prefab", "确定");
            return;
        }

        int totalAdded = 0;
        int totalSkipped = 0;
        var log = new List<string>();

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // 查找所有子对象（递归）
            Transform[] allChildren = prefab.GetComponentsInChildren<Transform>(true);
            bool prefabModified = false;

            foreach (Transform child in allChildren)
            {
                // 跳过根对象
                if (child == prefab.transform) continue;

                string objName = child.gameObject.name;

                // 检查名称是否匹配关键词
                bool isWall = false;
                foreach (string keyword in wallKeywords)
                {
                    if (objName.Contains(keyword))
                    {
                        isWall = true;
                        break;
                    }
                }
                if (!isWall) continue;

                // 检查是否有 SpriteRenderer
                var sr = child.GetComponent<SpriteRenderer>();
                if (sr == null) continue;

                // 检查是否已有 Collider2D
                var existingCol = child.GetComponent<Collider2D>();
                if (existingCol != null)
                {
                    // 已有碰撞体，只确保 compositeOperation 正确
                    if (existingCol.compositeOperation != Collider2D.CompositeOperation.Merge)
                    {
                        existingCol.compositeOperation = Collider2D.CompositeOperation.Merge;
                        log.Add($"  [设置Merge] {path} → {objName}");
                        prefabModified = true;
                        totalAdded++;
                    }
                    else
                    {
                        totalSkipped++;
                    }
                    continue;
                }

                // 使用 SpriteRenderer 的 localBounds 计算碰撞体大小
                Bounds bounds = sr.localBounds;
                Vector2 spriteSize = bounds.size;

                // 添加 BoxCollider2D
                var col = child.gameObject.AddComponent<BoxCollider2D>();
                col.size = spriteSize;
                col.offset = bounds.center;
                col.compositeOperation = Collider2D.CompositeOperation.Merge;

                log.Add($"  [添加BoxCollider2D] {path} → {objName} (size: {spriteSize:F2})");
                prefabModified = true;
                totalAdded++;
            }

            if (prefabModified)
            {
                EditorUtility.SetDirty(prefab);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = $"扫描完成！\n\n" +
            $"扫描 Prefab 数量: {prefabGuids.Length}\n" +
            $"新增碰撞体: {totalAdded} 个\n" +
            $"已有碰撞体: {totalSkipped} 个\n\n" +
            $"详细日志请看 Console";

        Debug.Log("=== Prefab 碰撞体添加日志 ===\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("完成", summary, "确定");
    }

    private void SetupCompositeConfiner()
    {
        // 1. 查找 Walls 父对象
        var wallsGo = GameObject.Find(wallsParentName);
        if (wallsGo == null)
        {
            EditorUtility.DisplayDialog("错误", $"场景中未找到名为「{wallsParentName}」的对象", "确定");
            return;
        }

        // 2. 添加 Rigidbody2D（Static）
        var rb = wallsGo.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = Undo.AddComponent<Rigidbody2D>(wallsGo);
        }
        rb.bodyType = RigidbodyType2D.Static;

        // 3. 添加 CompositeCollider2D
        var composite = wallsGo.GetComponent<CompositeCollider2D>();
        if (composite == null)
        {
            composite = Undo.AddComponent<CompositeCollider2D>(wallsGo);
        }
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        compositeCollider = composite;

        // 4. 遍历所有子对象的 Collider2D，勾选 Used by Composite
        var colliders = wallsGo.GetComponentsInChildren<Collider2D>();
        int count = 0;
        foreach (var col in colliders)
        {
            if (col.gameObject == wallsGo) continue; // 跳过父对象自身的碰撞体
            if (col.compositeOperation != Collider2D.CompositeOperation.Merge)
            {
                Undo.RecordObject(col, "Enable CompositeMerge");
                col.compositeOperation = Collider2D.CompositeOperation.Merge;
                count++;
            }
        }

        // 5. 配置 VirtualCamera 使用 CompositeCollider2D
        var vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam == null)
        {
            EditorUtility.DisplayDialog("错误", "场景中未找到 CinemachineVirtualCamera", "确定");
            return;
        }

        var confiner = vcam.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
        {
            confiner = Undo.AddComponent<CinemachineConfiner2D>(vcam.gameObject);
        }

        confiner.m_BoundingShape2D = composite;
        confiner.m_Damping = 0.5f;
        confiner.m_MaxWindowSize = 0;

        // 6. 清理旧的 CameraBounds 对象（如果存在）
        var oldBounds = GameObject.Find("CameraBounds");
        if (oldBounds != null)
        {
            Undo.DestroyObjectImmediate(oldBounds);
        }

        EditorUtility.DisplayDialog("完成",
            $"CompositeCollider2D 配置完成！\n\n" +
            $"Walls 对象: {wallsGo.name}\n" +
            $"启用 UsedByComposite 的碰撞体: {count} 个\n" +
            $"VirtualCamera: {vcam.name}\n\n" +
            $"墙体变化时碰撞区域会自动更新。",
            "确定");
    }

    private void CreatePolygonBounds()
    {
        var existing = GameObject.Find("CameraBounds");
        if (existing != null)
        {
            boundsCollider = existing.GetComponent<PolygonCollider2D>();
            EditorUtility.DisplayDialog("提示", "已存在 CameraBounds 对象，已自动选中", "确定");
            Selection.activeGameObject = existing;
            return;
        }

        var go = new GameObject("CameraBounds");
        boundsCollider = go.AddComponent<PolygonCollider2D>();
        boundsCollider.isTrigger = true;

        boundsCollider.points = new Vector2[]
        {
            new Vector2(-10, -10),
            new Vector2(10, -10),
            new Vector2(10, 10),
            new Vector2(-10, 10)
        };

        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("完成", "已创建 CameraBounds 对象\n请在场景中编辑碰撞体顶点以匹配你的地图边界", "确定");
    }

    private void SetupVirtualCameraPolygon()
    {
        if (boundsCollider == null)
        {
            EditorUtility.DisplayDialog("错误", "请先指定边界碰撞体", "确定");
            return;
        }

        var vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam == null)
        {
            EditorUtility.DisplayDialog("错误", "场景中未找到 CinemachineVirtualCamera", "确定");
            return;
        }

        var confiner = vcam.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
        {
            confiner = Undo.AddComponent<CinemachineConfiner2D>(vcam.gameObject);
        }

        confiner.m_BoundingShape2D = boundsCollider;
        confiner.m_Damping = 0.5f;
        confiner.m_MaxWindowSize = 0;

        EditorUtility.DisplayDialog("完成",
            $"已配置完成：\n" +
            $"VirtualCamera: {vcam.name}\n" +
            $"边界碰撞体: {boundsCollider.gameObject.name}\n\n" +
            $"请运行游戏测试效果", "确定");
    }
}
