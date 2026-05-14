using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    private static PauseMenu instance;

    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private GameObject settingsPanelPrefab;

    private GameObject menuRoot;
    private GameObject settingsPanel;
    private bool isPaused;

    public static bool IsPaused => instance != null && instance.isPaused;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveUIReferences();
        if (menuRoot == null)
        {
            CreateLegacyUI();
        }
        EnsureSettingsPanel();
        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            Time.timeScale = 1f;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isPaused)
        {
            Resume();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    private void ResolveUIReferences()
    {
        var canvas = FindChildRecursive(transform, "PauseCanvas");
        if (canvas != null)
        {
            menuRoot = FindChildRecursive(canvas, "PauseMenuRoot")?.gameObject;
        }

        if (menuRoot == null)
            menuRoot = FindChildRecursive(transform, "PauseMenuRoot")?.gameObject;

        if (menuRoot != null)
        {
            ConfigurePrefabButton("ResumeButton", OnResume);
            ConfigurePrefabButton("SettingsButton", OnSettings);
            ConfigurePrefabButton("QuitButton", OnQuit);
        }
    }

    private void ConfigurePrefabButton(string buttonName, UnityEngine.Events.UnityAction onClick)
    {
        var buttonTransform = FindChildRecursive(menuRoot.transform, buttonName);
        if (buttonTransform == null)
            return;

        var button = buttonTransform.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveListener(onClick);
        button.onClick.AddListener(onClick);

        RemoveGeneratedButtonAdditions(buttonTransform);
    }

    private void RemoveGeneratedButtonAdditions(Transform buttonTransform)
    {
        var background = buttonTransform.Find("VisibleBackground");
        var labelTransform = buttonTransform.Find("Label");

        if (background != null)
            Destroy(background.gameObject);

        if (labelTransform != null && labelTransform.GetComponent<Text>() != null)
            Destroy(labelTransform.gameObject);
    }

    private void CreateLegacyUI()
    {
        var canvasObj = new GameObject("PauseCanvas");
        canvasObj.transform.SetParent(transform);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        menuRoot = new GameObject("PauseMenuRoot");
        menuRoot.transform.SetParent(canvasObj.transform, false);

        var bg = menuRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        var bgRect = menuRoot.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(menuRoot.transform, false);
        var titleText = titleObj.AddComponent<Text>();
        titleText.text = "游戏已暂停";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 48;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(400, 60);
        titleRect.anchoredPosition = new Vector2(0, 120);

        float buttonY = 30f;
        float buttonSpacing = 70f;

        CreateButton(menuRoot.transform, "ResumeButton", "继续游戏", new Vector2(0, buttonY), OnResume);
        CreateButton(menuRoot.transform, "SettingsButton", "设置", new Vector2(0, buttonY - buttonSpacing), OnSettings);
        CreateButton(menuRoot.transform, "QuitButton", "退出到主菜单", new Vector2(0, buttonY - buttonSpacing * 2), OnQuit);

    }

    private void EnsureSettingsPanel()
    {
        if (settingsPanel != null)
            return;

        var prefab = settingsPanelPrefab != null ? settingsPanelPrefab : LoadSettingsPanelPrefab();
        if (prefab == null)
            return;

        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            return;

        settingsPanel = Instantiate(prefab, canvas.transform, false);
        settingsPanel.name = "SettingsPanel";
        settingsPanel.SetActive(false);
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            var found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private GameObject LoadSettingsPanelPrefab()
    {
        var fromResources = Resources.Load<GameObject>("UI/SettingsPanel");
        if (fromResources != null)
            return fromResources;
#if UNITY_EDITOR
        const string prefabPath = "Assets/Prefabs/UI/SettingsPanel.prefab";
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
#else
        return null;
#endif
    }

    private void CreateButton(Transform parent, string name, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var button = btnObj.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.8f, 0.1f, 0.1f, 1f);
        colors.pressedColor = new Color(0.6f, 0.05f, 0.05f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(260, 50);
        rect.anchoredPosition = position;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var txt = textObj.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 28;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
    }

    public static void Pause()
    {
        if (instance == null) return;
        if (instance.settingsPanel != null && instance.settingsPanel.activeSelf) return;

        instance.isPaused = true;
        if (instance.menuRoot != null)
            instance.menuRoot.SetActive(true);
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        AudioManager.Instance.Play(SFX.UIPopupOpen);
    }

    public static void Resume()
    {
        if (instance == null) return;

        instance.isPaused = false;
        if (instance.menuRoot != null)
            instance.menuRoot.SetActive(false);
        if (instance.settingsPanel != null)
            instance.settingsPanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
        SetVisionMaskEnabled(true);
        AudioManager.Instance.Play(SFX.UIPopupClose);
    }

    private static void SetVisionMaskEnabled(bool enabled)
    {
        var mask = FindObjectOfType<PlayerVisionMaskSystem>();
        if (mask == null) return;

        var canvas = mask.GetComponentInChildren<Canvas>(true);
        if (canvas != null)
            canvas.gameObject.SetActive(enabled);
    }

    private void OnResume()
    {
        Resume();
    }

    private void OnSettings()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);
        EnsureSettingsPanel();
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void ReturnFromSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (menuRoot != null)
            menuRoot.SetActive(true);
    }

    private void OnQuit()
    {
        isPaused = false;
        if (menuRoot != null)
            menuRoot.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SetVisionMaskEnabled(false);
        Destroy(gameObject);
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
