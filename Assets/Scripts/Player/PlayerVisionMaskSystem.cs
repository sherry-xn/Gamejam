using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "PlayerVisionMaskSettings", menuName = "Game/Player Vision Mask Settings")]
public class PlayerVisionMaskSettings : ScriptableObject
{
    [Header("Darkness")]
    [SerializeField] private Color darknessColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float darknessAlpha = 0.9f;

    [Header("Near Visibility")]
    [SerializeField, Min(0f)] private float innerRadius = 0.12f;

    [Header("Flashlight")]
    [SerializeField, Min(0f)] private float coneRange = 0.75f;
    [SerializeField, Range(1f, 179f)] private float coneAngle = 70f;

    [Header("Edge Softness")]
    [SerializeField, Min(0.001f)] private float edgeSoftness = 0.05f;
    [SerializeField, Min(0.001f)] private float coneAngleSoftness = 0.05f;

    public Color DarknessColor => darknessColor;
    public float DarknessAlpha => darknessAlpha;
    public float InnerRadius => innerRadius;
    public float ConeRange => coneRange;
    public float ConeAngle => coneAngle;
    public float EdgeSoftness => edgeSoftness;
    public float ConeAngleSoftness => coneAngleSoftness;
}

/// <summary>
/// 在屏幕上绘制一层黑色遮罩，并在玩家周围挖出可见区域：
/// 1) 玩家身边一个小圆形常亮；
/// 2) 玩家面朝方向一个锥形手电筒视野。
/// </summary>
public class PlayerVisionMaskSystem : MonoBehaviour
{
    private const string SettingsResourcePath = "PlayerVisionMaskSettings";

    [Header("Settings")]
    [SerializeField] private PlayerVisionMaskSettings settings;
    [Header("遮罩")]
    [SerializeField] private Color darknessColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float darknessAlpha = 0.9f;

    [Header("近身可视")]
    [SerializeField, Min(0f)] private float innerRadius = 0.12f;

    [Header("手电筒锥形视野")]
    [SerializeField, Min(0f)] private float coneRange = 0.75f;
    [SerializeField, Range(1f, 179f)] private float coneAngle = 70f;

    [Header("边缘过渡")]
    [SerializeField, Min(0.001f)] private float edgeSoftness = 0.05f;
    [SerializeField, Min(0.001f)] private float coneAngleSoftness = 0.05f;

    private const string ShaderName = "Hidden/PlayerVisionMask";
    private const int MaskSortingOrder = -100;

    private Camera mainCamera;
    private Transform player;
    private Canvas maskCanvas;
    private Image maskImage;
    private Material maskMaterial;
    private bool forceHidden;

    private static PlayerVisionMaskSystem instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (instance != null)
        {
            return;
        }

        var root = new GameObject(nameof(PlayerVisionMaskSystem));
        DontDestroyOnLoad(root);
        instance = root.AddComponent<PlayerVisionMaskSystem>();
    }

    private void OnEnable()
    {
        LoadSettingsIfNeeded();
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (forceHidden)
        {
            SetMaskVisualActive(false);
            return;
        }

        SetMaskVisualActive(TryBindSceneObjects());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (maskMaterial != null)
        {
            Destroy(maskMaterial);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LoadSettingsIfNeeded();
        mainCamera = null;
        player = null;
        if (forceHidden)
        {
            SetMaskVisualActive(false);
            return;
        }

        SetMaskVisualActive(TryBindSceneObjects());
    }

    private void LateUpdate()
    {
        if (forceHidden)
        {
            SetMaskVisualActive(false);
            return;
        }

        if (!TryBindSceneObjects())
        {
            SetMaskVisualActive(false);
            return;
        }

        SetMaskVisualActive(true);
        UpdateMaskMaterial();
    }

    private bool TryBindSceneObjects()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        EnsureMaskVisual();

        return mainCamera != null && player != null && maskMaterial != null;
    }

    private void EnsureMaskVisual()
    {
        if (maskCanvas == null)
        {
            var canvasObj = new GameObject("PlayerVisionMaskCanvas");
            canvasObj.transform.SetParent(transform, false);

            maskCanvas = canvasObj.AddComponent<Canvas>();
            maskCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
        }

        maskCanvas.sortingOrder = MaskSortingOrder;

        if (maskImage == null)
        {
            var imageObj = new GameObject("PlayerVisionMaskImage");
            imageObj.transform.SetParent(maskCanvas.transform, false);

            maskImage = imageObj.AddComponent<Image>();
            maskImage.raycastTarget = false;

            var rect = maskImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        if (maskMaterial == null)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                return;
            }

            maskMaterial = new Material(shader)
            {
                name = "PlayerVisionMaskMaterial (Runtime)"
            };
            maskImage.material = maskMaterial;
            maskImage.color = Color.white;
        }
    }

    private void SetMaskVisualActive(bool active)
    {
        if (maskCanvas != null)
        {
            maskCanvas.gameObject.SetActive(active);
        }
    }

    public void SetForceHidden(bool hidden)
    {
        forceHidden = hidden;

        if (hidden)
        {
            SetMaskVisualActive(false);
            return;
        }

        SetMaskVisualActive(TryBindSceneObjects());
    }

    private void UpdateMaskMaterial()
    {
        Vector3 playerViewport = mainCamera.WorldToViewportPoint(player.position);
        Vector3 lookPointViewport = mainCamera.WorldToViewportPoint(player.position + player.right);
        Vector2 facingDir = (Vector2)(lookPointViewport - playerViewport);

        if (facingDir.sqrMagnitude < 0.000001f)
        {
            facingDir = Vector2.right;
        }
        else
        {
            facingDir.Normalize();
        }

        maskMaterial.SetColor("_DarknessColor", settings != null ? settings.DarknessColor : darknessColor);
        maskMaterial.SetFloat("_DarknessAlpha", settings != null ? settings.DarknessAlpha : darknessAlpha);
        maskMaterial.SetVector("_PlayerViewportPos", new Vector4(playerViewport.x, playerViewport.y, 0f, 0f));
        maskMaterial.SetVector("_FacingDir", new Vector4(facingDir.x, facingDir.y, 0f, 0f));
        maskMaterial.SetFloat("_InnerRadius", settings != null ? settings.InnerRadius : innerRadius);
        maskMaterial.SetFloat("_ConeRange", settings != null ? settings.ConeRange : coneRange);
        maskMaterial.SetFloat("_ConeHalfAngleCos", Mathf.Cos((settings != null ? settings.ConeAngle : coneAngle) * 0.5f * Mathf.Deg2Rad));
        maskMaterial.SetFloat("_EdgeSoftness", settings != null ? settings.EdgeSoftness : edgeSoftness);
        maskMaterial.SetFloat("_ConeAngleSoftness", settings != null ? settings.ConeAngleSoftness : coneAngleSoftness);
        maskMaterial.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1f, Screen.height));
    }

    private void LoadSettingsIfNeeded()
    {
        if (settings != null)
        {
            return;
        }

        settings = Resources.Load<PlayerVisionMaskSettings>(SettingsResourcePath);
    }
}
