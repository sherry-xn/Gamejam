using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class PrefabDoorColliderSetup : EditorWindow
{
    private string prefabFolder = "Assets/Prefabs";

    [MenuItem("Tools/Prefab 门碰撞体设置")]
    public static void ShowWindow()
    {
        GetWindow<PrefabDoorColliderSetup>("门碰撞体设置");
    }

    private void OnGUI()
    {
        GUILayout.Label("给 Prefab 的 Door 对象添加碰撞体", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        prefabFolder = EditorGUILayout.TextField("Prefab 文件夹", prefabFolder);

        EditorGUILayout.HelpBox(
            "扫描 Prefab 文件夹，给所有名称含 door/Door 的子对象\n" +
            "添加 BoxCollider2D (isTrigger)，用于房间切换检测。",
            MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("扫描并添加碰撞体"))
        {
            AddCollidersToDoors();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("移除门碰撞体"))
        {
            RemoveCollidersFromDoors();
        }
    }

    private void AddCollidersToDoors()
    {
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

            Transform[] allChildren = prefab.GetComponentsInChildren<Transform>(true);
            bool prefabModified = false;

            foreach (Transform child in allChildren)
            {
                if (child == prefab.transform) continue;

                string objName = child.gameObject.name;
                if (!objName.Contains("door") && !objName.Contains("Door")) continue;

                var existingCol = child.GetComponent<Collider2D>();
                if (existingCol != null)
                {
                    log.Add($"  [已有] {path} → {objName}");
                    totalSkipped++;
                    continue;
                }

                var sr = child.GetComponent<SpriteRenderer>();
                Vector2 size;
                if (sr != null && sr.sprite != null && sr.sprite.rect.width > 0)
                {
                    size = new Vector2(
                        sr.sprite.rect.width / sr.sprite.pixelsPerUnit,
                        sr.sprite.rect.height / sr.sprite.pixelsPerUnit);
                }
                else
                {
                    size = new Vector2(1f, 2f);
                }

                var col = child.gameObject.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = size;
                col.offset = Vector2.zero;

                log.Add($"  [添加] {path} → {objName} (size: {size:F2})");
                prefabModified = true;
                totalAdded++;
            }

            if (prefabModified) EditorUtility.SetDirty(prefab);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== 门碰撞体添加日志 ===\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("完成",
            $"扫描 Prefab: {prefabGuids.Length}\n新增: {totalAdded}\n已有: {totalSkipped}\n\n详细日志见 Console",
            "确定");
    }

    private void RemoveCollidersFromDoors()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        if (prefabGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", $"在 {prefabFolder} 中未找到任何 Prefab", "确定");
            return;
        }

        int totalRemoved = 0;
        var log = new List<string>();

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
                if (!objName.Contains("door") && !objName.Contains("Door")) continue;

                var col = child.GetComponent<BoxCollider2D>();
                if (col == null) continue;

                Object.DestroyImmediate(col);
                log.Add($"  [移除] {path} → {objName}");
                prefabModified = true;
                totalRemoved++;
            }

            if (prefabModified) EditorUtility.SetDirty(prefab);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== 门碰撞体移除日志 ===\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("完成",
            $"移除: {totalRemoved} 个碰撞体\n\n详细日志见 Console",
            "确定");
    }
}
