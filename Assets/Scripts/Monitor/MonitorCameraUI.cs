using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 监控终端 UI 叠加层。
/// 负责信号丢失遮罩和 Z/X 切换输入。
/// 挂在场景中任意 GameObject 上即可。
/// </summary>
public class MonitorCameraUI : MonoBehaviour
{
    private static Sprite fallbackWhiteSprite;

    [SerializeField, Tooltip("信号丢失时的全屏遮罩（可选）")]
    private Image signalLostOverlay;

    [SerializeField, Tooltip("监控背景")]
    private Image ui_monitoring_bg;

    [SerializeField, Tooltip("录制红点指示器")]
    private Image ui_monitoring_rec;

    [SerializeField, Tooltip("当前监控房间信息")]
    private Text cameraInfoText;

    private Image cameraFeedImage;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        SetupCanvasGroup();
        SetupVisuals();
        SetupCameraFeedImage();
        SetupCameraInfoText();
        Hide();
        if (signalLostOverlay != null) signalLostOverlay.enabled = false;
    }

    private void Update()
    {
        var monitor = MonitorController.Instance;
        if (monitor == null || !monitor.IsMonitorOpen) return;

        if (Input.GetKeyDown(KeyCode.Z))
            monitor.PrevCamera();
        if (Input.GetKeyDown(KeyCode.X))
            monitor.NextCamera();
    }

    /// <summary>
    /// 显示监控 UI 元素
    /// </summary>
    public void Show()
    {
        SetVisible(true);
        if (cameraFeedImage != null) cameraFeedImage.enabled = cameraFeedImage.sprite != null;
        if (ui_monitoring_bg != null) ui_monitoring_bg.enabled = true;
        if (ui_monitoring_rec != null) ui_monitoring_rec.enabled = true;
        if (cameraInfoText != null) cameraInfoText.enabled = true;
        RefreshCameraInfo();
    }

    /// <summary>
    /// 隐藏监控 UI 元素
    /// </summary>
    public void Hide()
    {
        if (cameraFeedImage != null) cameraFeedImage.enabled = false;
        if (ui_monitoring_bg != null) ui_monitoring_bg.enabled = false;
        if (ui_monitoring_rec != null) ui_monitoring_rec.enabled = false;
        if (cameraInfoText != null) cameraInfoText.enabled = false;
        if (signalLostOverlay != null) signalLostOverlay.enabled = false;
        SetVisible(false);
    }

    public void SetCameraFeed(Sprite feedSprite)
    {
        SetupCameraFeedImage();

        if (cameraFeedImage == null)
            return;

        cameraFeedImage.sprite = feedSprite;
        cameraFeedImage.enabled = feedSprite != null && gameObject.activeInHierarchy;
    }

    private void LateUpdate()
    {
        var monitor = MonitorController.Instance;
        if (monitor == null || !monitor.IsMonitorOpen) return;

        RefreshCameraInfo();
    }

    private void SetupVisuals()
    {
        if (ui_monitoring_bg != null)
        {
            ui_monitoring_bg.sprite = EnsureSprite(ui_monitoring_bg.sprite);
            // This image is full-screen in the prefab, so an opaque color hides the camera feed.
            ui_monitoring_bg.color = new Color(0f, 0f, 0f, 0.08f);
            ui_monitoring_bg.raycastTarget = false;
        }

        if (ui_monitoring_rec != null)
        {
            ui_monitoring_rec.sprite = EnsureSprite(ui_monitoring_rec.sprite);
            ui_monitoring_rec.color = new Color(1f, 0.08f, 0.08f, 1f);
            ui_monitoring_rec.raycastTarget = false;

            var rt = ui_monitoring_rec.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(32f, -32f);
            rt.sizeDelta = new Vector2(56f, 24f);
        }

        if (signalLostOverlay != null)
        {
            signalLostOverlay.sprite = EnsureSprite(signalLostOverlay.sprite);
            signalLostOverlay.color = new Color(1f, 1f, 1f, 1f);
            signalLostOverlay.raycastTarget = false;
        }
    }

    private void SetupCanvasGroup()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            SetupCanvasGroup();

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void SetupCameraFeedImage()
    {
        if (cameraFeedImage == null)
        {
            var feedGo = new GameObject("cameraFeedImage");
            feedGo.transform.SetParent(transform, false);
            cameraFeedImage = feedGo.AddComponent<Image>();
        }

        if (cameraFeedImage == null)
            return;

        var rt = cameraFeedImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        cameraFeedImage.type = Image.Type.Simple;
        cameraFeedImage.preserveAspect = true;
        cameraFeedImage.color = Color.white;
        cameraFeedImage.raycastTarget = false;
        cameraFeedImage.transform.SetAsFirstSibling();
    }

    private void SetupCameraInfoText()
    {
        if (cameraInfoText == null)
        {
            var infoGo = new GameObject("cameraInfoText");
            infoGo.transform.SetParent(transform, false);
            cameraInfoText = infoGo.AddComponent<Text>();
        }

        if (cameraInfoText == null)
            return;

        var rt = cameraInfoText.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -24f);
        rt.sizeDelta = new Vector2(300f, 48f);

        cameraInfoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cameraInfoText.fontSize = 18;
        cameraInfoText.color = Color.white;
        cameraInfoText.alignment = TextAnchor.UpperLeft;
        cameraInfoText.raycastTarget = false;
        cameraInfoText.horizontalOverflow = HorizontalWrapMode.Overflow;
        cameraInfoText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void RefreshCameraInfo()
    {
        if (cameraInfoText == null)
            return;

        var monitor = MonitorController.Instance;
        if (monitor == null || !monitor.IsMonitorOpen)
        {
            cameraInfoText.text = string.Empty;
            return;
        }

        string roomName = monitor.CurrentCameraRoomName;
        int currentIndex = monitor.CurrentCameraIndex + 1;
        int totalCount = monitor.CurrentCameraCount;
        cameraInfoText.text = $"房间: {roomName}  {currentIndex}/{totalCount}";
    }

    private static Sprite EnsureSprite(Sprite sprite)
    {
        if (sprite != null)
            return sprite;

        if (fallbackWhiteSprite == null)
        {
            fallbackWhiteSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        return fallbackWhiteSprite;
    }
}
