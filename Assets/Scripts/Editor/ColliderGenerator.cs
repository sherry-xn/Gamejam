using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class ColliderGenerator : EditorWindow
{
    private static readonly string[] wallKeywords = { "wall", "Wall", "boundary", "Boundary" };
    private static readonly string[] doorKeywords = { "door", "Door" };

    [MenuItem("Tools/一键生成碰撞体")]
    public static void ShowWindow()
    {
        GetWindow<ColliderGenerator>("碰撞体生成器");
    }

    private void OnGUI()
    {
        GUILayout.Label("一键生成场景中所有对象的碰撞体", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "墙壁 (名称含 wall/boundary)：添加 BoxCollider2D (物理阻挡)\n" +
            "门 (名称含 door)：添加 BoxCollider2D (Trigger 检测) + BoxCollider2D (物理阻挡)\n\n" +
            "已有碰撞体的对象会被跳过，不会重复添加。",
            MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("一键生成所有碰撞体", GUILayout.Height(40)))
        {
            GenerateAllColliders();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("移除场景中所有门的碰撞体"))
        {
            RemoveDoorColliders();
        }

        if (GUILayout.Button("移除场景中所有墙壁的碰撞体"))
        {
            RemoveWallColliders();
        }
    }

    private static void GenerateAllColliders()
    {
        var allObjects = FindObjectsOfType<Transform>(true);
        int wallAdded = 0, doorAdded = 0, skipped = 0;
        var log = new List<string>();

        Undo.RecordObjects(allObjects, "Generate Colliders");

        foreach (var t in allObjects)
        {
            string objName = t.gameObject.name;
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            Vector2 size = GetSpriteSize(sr);
            if (size.x < 0.01f || size.y < 0.01f)
            {
                skipped++;
                continue;
            }

            bool isDoor = ContainsAny(objName, doorKeywords);
            bool isWall = ContainsAny(objName, wallKeywords);

            if (!isDoor && !isWall)
            {
                skipped++;
                continue;
            }

            var existingCols = t.GetComponents<BoxCollider2D>();

            if (isWall)
            {
                bool hasNonTrigger = false;
                foreach (var c in existingCols)
                    if (!c.isTrigger) { hasNonTrigger = true; break; }

                if (hasNonTrigger)
                {
                    log.Add($"  [墙壁-跳过] {GetPath(t)} (已有物理碰撞体)");
                    continue;
                }

                var col = Undo.AddComponent<BoxCollider2D>(t.gameObject);
                col.size = size;
                col.offset = Vector2.zero;
                log.Add($"  [墙壁-添加] {GetPath(t)} (size: {size:F2})");
                wallAdded++;
            }
            else if (isDoor)
            {
                bool hasTrigger = false;
                bool hasBlock = false;
                foreach (var c in existingCols)
                {
                    if (c.isTrigger) hasTrigger = true;
                    else hasBlock = true;
                }

                if (hasTrigger && hasBlock)
                {
                    log.Add($"  [门-跳过] {GetPath(t)} (已有完整碰撞体)");
                    continue;
                }

                if (!hasTrigger)
                {
                    var triggerCol = Undo.AddComponent<BoxCollider2D>(t.gameObject);
                    triggerCol.isTrigger = true;
                    triggerCol.size = size;
                    triggerCol.offset = Vector2.zero;
                }

                if (!hasBlock)
                {
                    var blockCol = Undo.AddComponent<BoxCollider2D>(t.gameObject);
                    blockCol.isTrigger = false;
                    blockCol.size = size;
                    blockCol.offset = Vector2.zero;
                }

                log.Add($"  [门-添加] {GetPath(t)} (trigger + block, size: {size:F2})");
                doorAdded++;
            }
        }

        Debug.Log("=== 碰撞体生成日志 ===\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("完成",
            $"墙壁碰撞体: {wallAdded}\n门碰撞体: {doorAdded}\n跳过: {skipped}\n\n详细日志见 Console",
            "确定");
    }

    private static void RemoveDoorColliders()
    {
        var allObjects = FindObjectsOfType<Transform>(true);
        int removed = 0;
        var log = new List<string>();

        foreach (var t in allObjects)
        {
            if (!ContainsAny(t.gameObject.name, doorKeywords)) continue;

            var cols = t.GetComponents<BoxCollider2D>();
            if (cols.Length == 0) continue;

            foreach (var col in cols)
            {
                Undo.DestroyObjectImmediate(col);
            }
            log.Add($"  [移除] {GetPath(t)}");
            removed++;
        }

        Debug.Log("=== 门碰撞体移除日志 ===\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("完成", $"移除 {removed} 个门碰撞体", "确定");
    }

    private static void RemoveWallColliders()
    {
        var allObjects = FindObjectsOfType<Transform>(true);
        int removed = 0;
        var log = new List<string>();

        foreach (var t in allObjects)
        {
            if (!ContainsAny(t.gameObject.name, wallKeywords)) continue;

            var cols = t.GetComponents<BoxCollider2D>();
            if (cols.Length == 0) continue;

            foreach (var col in cols)
            {
                Undo.DestroyObjectImmediate(col);
            }
            log.Add($"  [移除] {GetPath(t)}");
            removed++;
        }

        Debug.Log("=== 墙壁碰撞体移除日志 ===\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("完成", $"移除 {removed} 个墙壁碰撞体", "确定");
    }

    private static Vector2 GetSpriteSize(SpriteRenderer sr)
    {
        Sprite sprite = sr.sprite;
        if (sprite != null && sprite.rect.width > 0 && sprite.rect.height > 0)
        {
            return new Vector2(
                sprite.rect.width / sprite.pixelsPerUnit,
                sprite.rect.height / sprite.pixelsPerUnit);
        }
        return sr.localBounds.size;
    }

    private static bool ContainsAny(string name, string[] keywords)
    {
        foreach (var kw in keywords)
            if (name.Contains(kw)) return true;
        return false;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        var parent = t.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
