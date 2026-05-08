#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuSetup
{
    [MenuItem("Tools/Setup Main Menu Scene")]
    public static void CreateMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var camera = Camera.main;
        camera.backgroundColor = new Color(0.04f, 0.04f, 0.04f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0f, 0f, 0.3f);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        var titleText = titleGO.AddComponent<Text>();
        titleText.text = "\u6821\u56ed\u60ca\u9b42";
        titleText.fontSize = 72;
        titleText.color = new Color(0.545f, 0f, 0f, 1f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(600, 100);
        titleRect.anchoredPosition = new Vector2(0, -200);

        var startBtnGO = CreateButton(canvasGO.transform, "StartButton", "\u5f00\u59cb\u6e38\u620f", new Vector2(0, 0));
        var exitBtnGO = CreateButton(canvasGO.transform, "ExitButton", "\u9000\u51fa\u6e38\u620f", new Vector2(0, -80));

        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        var menuUIGO = new GameObject("MainMenuUI");
        var menuUI = menuUIGO.AddComponent<MainMenuUI>();

        var so = new SerializedObject(menuUI);
        so.FindProperty("startButton").objectReferenceValue = startBtnGO.GetComponent<Button>();
        so.FindProperty("exitButton").objectReferenceValue = exitBtnGO.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.scene");

        var buildScenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.scene", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.scene", true)
        };
        EditorBuildSettings.scenes = buildScenes;

        Debug.Log("MainMenu scene created and build settings updated!");
    }

    private static GameObject CreateButton(Transform parent, string name, string text, Vector2 position)
    {
        var btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);

        var image = btnGO.AddComponent<Image>();
        image.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        var button = btnGO.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.8f, 0.1f, 0.1f, 1f);
        button.colors = colors;

        var rect = btnGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(240, 60);
        rect.anchoredPosition = position;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        var txt = textGO.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = 32;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = FontStyle.Bold;

        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        return btnGO;
    }
}
#endif
