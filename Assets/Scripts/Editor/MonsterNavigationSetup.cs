using UnityEngine;
using UnityEditor;
using Pathfinding;

public class MonsterNavigationSetup : EditorWindow
{
    [MenuItem("Tools/Setup Monster Navigation")]
    public static void SetupNavigation()
    {
        // 查找或创建 AstarPath 对象
        AstarPath astarPath = FindObjectOfType<AstarPath>();
        if (astarPath == null)
        {
            GameObject astarObj = new GameObject("AstarPath");
            astarPath = astarObj.AddComponent<AstarPath>();
            Debug.Log("[MonsterNav] 创建 AstarPath 对象");
        }

        // 清除现有图形
        astarPath.data.graphs = new NavGraph[0];

        // 创建 Grid Graph
        GridGraph gridGraph = astarPath.data.AddGraph(typeof(GridGraph)) as GridGraph;
        
        // 配置 Grid Graph - 覆盖整个地图
        gridGraph.SetDimensions(450, 460, 0.25f);
        gridGraph.center = new Vector3(52.0f, 22.0f, 0f);
        gridGraph.rotation = new Vector3(0, 0, 0);
        
        // 2D 模式设置
        gridGraph.is2D = true;
        
        // 碰撞检测设置
        gridGraph.collision.use2D = true;
        gridGraph.collision.diameter = 0.5f;
        gridGraph.collision.height = 1f;
        
        // 碰撞检测层 - 检测 Default(0) 和 Obstacle(6) 层
        gridGraph.collision.mask = (1 << 0) | (1 << 6);
        Debug.Log("[MonsterNav] 碰撞检测层: Default + Obstacle");
        
        // 禁用高度检测
        gridGraph.collision.heightMask = 0;
        gridGraph.collision.fromHeight = 100;
        
        // 扫描图形
        AstarPath.active.Scan();
        
        Debug.Log("[MonsterNav] 导航配置完成！已创建 Grid Graph 并扫描");
        
        // 选中 AstarPath 对象
        Selection.activeGameObject = astarPath.gameObject;
    }

    [MenuItem("Tools/Setup Monster Navigation", true)]
    public static bool SetupNavigationValidation()
    {
        return true;
    }
}
