using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen hurt overlay that flashes when player takes damage.
/// Follows ScreenHintPanel singleton pattern.
/// </summary>
public class HurtUI : MonoBehaviour
{
    private static HurtUI instance;

    [Header("Timing")]
    [SerializeField] private float displayDuration = 0.15f;
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Sprite")]
    [SerializeField] private Sprite hurtSprite;

    private CanvasGroup canvasGroup;
    private Image hurtImage;
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

    /// <summary>
    /// Show the hurt overlay effect. Call from PlayerController.TakeDamage().
    /// </summary>
    public static void Show()
    {
        EnsureInstance();
        instance.ShowInternal();
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;

        var hurtObj = new GameObject("[HurtUI]");
        hurtObj.AddComponent<HurtUI>();
    }

    private void CreateUI()
    {
        // Load sprite if not assigned
        if (hurtSprite == null)
        {
            hurtSprite = Resources.Load<Sprite>("ui_game_hurt");
            if (hurtSprite == null)
            {
                // Try loading from path
                var tex = Resources.Load<Texture2D>("ui_game_hurt");
                if (tex != null)
                    hurtSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            }
        }

        // Create Canvas
        var canvasObj = new GameObject("HurtUICanvas");
        canvasObj.transform.SetParent(transform, false);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Below ScreenHintPanel but above game UI

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Create hurt image
        var imageObj = new GameObject("HurtImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        hurtImage = imageObj.AddComponent<Image>();
        hurtImage.sprite = hurtSprite;
        hurtImage.color = Color.white;
        hurtImage.raycastTarget = false;

        // Stretch to fill screen
        var rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        // Add CanvasGroup for fade control
        canvasGroup = imageObj.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void ShowInternal()
    {
        // Stop any existing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        canvasGroup.alpha = 1f;
        hurtImage.gameObject.SetActive(true);

        fadeCoroutine = StartCoroutine(FadeOutAfterDelay());
    }

    private IEnumerator FadeOutAfterDelay()
    {
        yield return new WaitForSecondsRealtime(displayDuration);

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
            canvasGroup.alpha = 0f;

        if (hurtImage != null)
            hurtImage.gameObject.SetActive(false);
    }
}
