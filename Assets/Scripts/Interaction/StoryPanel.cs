using UnityEngine;
using TMPro;

/// <summary>
/// 底部对话框单例，用于显示线索物品的文字内容。
/// 挂在场景中任意物体上即可，会自动创建 UI。
/// </summary>
public class StoryPanel : MonoBehaviour
{
    private static StoryPanel instance;

    [SerializeField] private float panelHeight = 160f;
    [SerializeField] private float fontSize = 24f;
    [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color textColor = Color.white;

    private GameObject panelRoot;
    private TextMeshProUGUI textComponent;
    private PlayerController currentPlayer;
    private bool isShowing;

    public static bool IsShowing => instance != null && instance.isShowing;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        CreateUI();
        panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void CreateUI()
    {
        var canvasObj = new GameObject("StoryCanvas");
        canvasObj.transform.SetParent(transform);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        panelRoot = new GameObject("StoryPanel");
        panelRoot.transform.SetParent(canvas.transform, false);

        var bg = panelRoot.AddComponent<UnityEngine.UI.Image>();
        bg.color = bgColor;

        var rt = panelRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0f, panelHeight);

        var textObj = new GameObject("StoryText");
        textObj.transform.SetParent(panelRoot.transform, false);

        textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.fontSize = fontSize;
        textComponent.color = textColor;
        textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        textComponent.enableWordWrapping = true;
        textComponent.margin = new Vector4(30f, 10f, 30f, 10f);

        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
    }

    private void Update()
    {
        if (!isShowing) return;

        if (UnityEngine.Input.GetKeyDown(KeyCode.E) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
        {
            Hide();
        }
    }

    public static void Show(string text, PlayerController player)
    {
        if (instance == null) return;
        instance.textComponent.text = text;
        instance.panelRoot.SetActive(true);
        instance.currentPlayer = player;
        instance.isShowing = true;
        if (player != null) player.Input.DisablePlayerMoveInput();
    }

    private void Hide()
    {
        panelRoot.SetActive(false);
        isShowing = false;
        if (currentPlayer != null) currentPlayer.Input.EnablePlayerMoveInput();
        currentPlayer = null;
    }
}
