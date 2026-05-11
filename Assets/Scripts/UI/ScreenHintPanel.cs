using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight screen message that fades out without blocking gameplay input.
/// </summary>
public class ScreenHintPanel : MonoBehaviour
{
    private static ScreenHintPanel instance;

    [SerializeField] private float panelWidth = 620f;
    [SerializeField] private float panelHeight = 90f;
    [SerializeField] private float visibleDuration = 1.2f;
    [SerializeField] private float fadeDuration = 0.8f;
    [SerializeField] private int fontSize = 28;
    [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color textColor = Color.white;

    private GameObject panelRoot;
    private CanvasGroup canvasGroup;
    private Text textComponent;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        CreateUI();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void Show(string message)
    {
        EnsureInstance();
        instance.ShowInternal(message);
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;

        var hintObject = new GameObject("ScreenHintPanel");
        hintObject.AddComponent<ScreenHintPanel>();
    }

    private void CreateUI()
    {
        var canvasObj = new GameObject("ScreenHintCanvas");
        canvasObj.transform.SetParent(transform, false);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        panelRoot = new GameObject("HintPanel");
        panelRoot.transform.SetParent(canvasObj.transform, false);

        canvasGroup = panelRoot.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var bg = panelRoot.AddComponent<Image>();
        bg.color = bgColor;
        bg.raycastTarget = false;

        var panelRt = panelRoot.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = new Vector2(0f, -260f);
        panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);

        var textObj = new GameObject("HintText");
        textObj.transform.SetParent(panelRoot.transform, false);

        textComponent = textObj.AddComponent<Text>();
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = fontSize;
        textComponent.color = textColor;
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        textComponent.raycastTarget = false;

        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(24f, 8f);
        textRt.offsetMax = new Vector2(-24f, -8f);
    }

    private void ShowInternal(string message)
    {
        textComponent.text = message;
        panelRoot.SetActive(true);
        canvasGroup.alpha = 1f;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeOutAfterDelay());
    }

    private IEnumerator FadeOutAfterDelay()
    {
        yield return new WaitForSecondsRealtime(visibleDuration);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }

        HideImmediate();
        fadeCoroutine = null;
    }

    private void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }
}
