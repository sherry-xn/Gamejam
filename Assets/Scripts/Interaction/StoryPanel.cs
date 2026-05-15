using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 线索文本面板单例，用于显示线索物品的文字内容。
/// 挂在场景中任意物体上即可，会自动创建 UI。
/// </summary>
public class StoryPanel : MonoBehaviour
{
    private static StoryPanel instance;

    [Header("Panel Layout")]
    [SerializeField] private Vector2 anchorMin = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 anchorMax = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 pivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 anchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 panelSize = new Vector2(900f, 260f);
    [SerializeField] private Vector4 textPadding = new Vector4(30f, 20f, 30f, 20f);

    [Header("Text Style")]
    [SerializeField] private int fontSize = 24;
    [SerializeField] private TextAnchor textAlignment = TextAnchor.MiddleLeft;

    [Header("Colors")]
    [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color textColor = Color.white;

    private GameObject panelRoot;
    private Text textComponent;
    private PlayerController currentPlayer;
    private bool isShowing;
    private bool readyToClose;

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
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        panelRoot = new GameObject("StoryPanel");
        panelRoot.transform.SetParent(canvasObj.transform, false);

        var bg = panelRoot.AddComponent<Image>();
        bg.color = bgColor;

        var rt = panelRoot.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = panelSize;

        var textObj = new GameObject("StoryText");
        textObj.transform.SetParent(panelRoot.transform, false);

        textComponent = textObj.AddComponent<Text>();
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = fontSize;
        textComponent.color = textColor;
        textComponent.alignment = textAlignment;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;

        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(textPadding.x, textPadding.w);
        textRt.offsetMax = new Vector2(-textPadding.z, -textPadding.y);
    }

    private void Update()
    {
        if (!isShowing) return;
        if (!readyToClose) { readyToClose = true; return; }

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
        {
            Hide();
        }
    }

    public static void Show(string text, PlayerController player)
    {
        Show(text, player, SFX.UIPopupOpen);
    }

    public static void Show(string text, PlayerController player, SFX openSound)
    {
        if (instance == null) return;
        instance.textComponent.text = text;
        instance.ApplyLayout();
        instance.panelRoot.SetActive(true);
        instance.currentPlayer = player;
        instance.isShowing = true;
        instance.readyToClose = false;
        if (player != null) player.Input.DisablePlayerMoveInput();
        AudioManager.Instance.Play(openSound);
    }

    private void Hide()
    {
        AudioManager.Instance.Play(SFX.UIPopupClose);
        panelRoot.SetActive(false);
        isShowing = false;
        if (currentPlayer != null) currentPlayer.Input.EnablePlayerMoveInput();
        currentPlayer = null;
    }

    private void ApplyLayout()
    {
        if (panelRoot != null)
        {
            var rt = panelRoot.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = panelSize;

            var bg = panelRoot.GetComponent<Image>();
            bg.color = bgColor;
        }

        if (textComponent != null)
        {
            textComponent.fontSize = fontSize;
            textComponent.color = textColor;
            textComponent.alignment = textAlignment;

            var textRt = textComponent.GetComponent<RectTransform>();
            textRt.offsetMin = new Vector2(textPadding.x, textPadding.w);
            textRt.offsetMax = new Vector2(-textPadding.z, -textPadding.y);
        }
    }

    private void OnValidate()
    {
        ApplyLayout();
    }
}
