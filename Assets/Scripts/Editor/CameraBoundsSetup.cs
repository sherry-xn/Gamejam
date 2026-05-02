using UnityEditor;
using UnityEngine;
using Cinemachine;

public class CameraBoundsSetup : EditorWindow
{
    private PolygonCollider2D boundsCollider;
    private float shrinkAmount = 1f;

    [MenuItem("Tools/相机边界设置")]
    public static void ShowWindow()
    {
        GetWindow<CameraBoundsSetup>("相机边界设置");
    }

    private void OnGUI()
    {
        GUILayout.Label("相机边界自动配置", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        shrinkAmount = EditorGUILayout.FloatField("边界内缩量", shrinkAmount);
        EditorGUILayout.HelpBox("内缩量：相机中心到墙壁的最小距离，通常设为相机视口半宽/半高", MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("1. 创建边界碰撞体"))
        {
            CreateBoundsCollider();
        }

        EditorGUILayout.Space();

        boundsCollider = (PolygonCollider2D)EditorGUILayout.ObjectField(
            "边界碰撞体", boundsCollider, typeof(PolygonCollider2D), true);

        if (GUILayout.Button("2. 配置 VirtualCamera"))
        {
            SetupVirtualCamera();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "使用步骤：\n" +
            "1. 点击「创建边界碰撞体」生成 CameraBounds 对象\n" +
            "2. 在场景中编辑 PolygonCollider2D 的顶点，沿墙壁内侧画出区域\n" +
            "3. 将碰撞体拖到上方字段\n" +
            "4. 点击「配置 VirtualCamera」完成绑定",
            MessageType.Info);
    }

    private void CreateBoundsCollider()
    {
        // 检查是否已存在
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

        // 设置默认矩形区域（可根据场景大小调整）
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

    private void SetupVirtualCamera()
    {
        if (boundsCollider == null)
        {
            EditorUtility.DisplayDialog("错误", "请先指定边界碰撞体", "确定");
            return;
        }

        // 查找 VirtualCamera
        var vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam == null)
        {
            EditorUtility.DisplayDialog("错误", "场景中未找到 CinemachineVirtualCamera", "确定");
            return;
        }

        // 添加 Confiner2D 扩展
        var confiner = vcam.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
        {
            confiner = Undo.AddComponent<CinemachineConfiner2D>(vcam.gameObject);
        }

        // 设置引用
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
