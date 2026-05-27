using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class FarmSimFirebaseSceneSetup
{
    private const string AuthScenePath = "Assets/Scenes/AuthScene.unity";
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string SettingsScenePath = "Assets/Scenes/SettingsScene.unity";
    private const string GameScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("FarmSim/Firebase/Create Or Update Auth Flow Scenes")]
    public static void CreateOrUpdateAuthFlowScenes()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Stop Play Mode before running FarmSim Firebase scene setup.");
            return;
        }

        CreateAuthScene();
        CreateSettingsScene();
        UpdateMenuScene();
        UpdateGameScene();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("FarmSim Firebase auth flow setup completed.");
    }

    [MenuItem("FarmSim/UI/Create Modern Menu UI In Open Scene")]
    public static void CreateModernMenuUiInOpenScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Stop Play Mode before creating menu UI.");
            return;
        }

        EnsureModernMenuUiInCurrentScene();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Modern menu UI created in the open scene.");
    }

    private static void CreateSettingsScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject camera = new("Main Camera");
        Camera cameraComponent = camera.AddComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = new Color(0.09f, 0.13f, 0.11f, 1f);
        camera.tag = "MainCamera";

        GameObject firebaseManager = new("FirebaseManager");
        firebaseManager.AddComponent<FirebaseInitializer>();
        firebaseManager.AddComponent<FirebaseAuthService>();
        firebaseManager.AddComponent<FirebaseSaveService>();

        GameObject eventSystem = new("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif

        GameObject controller = new("SettingsSceneController");
        SettingsSceneController settingsController = controller.AddComponent<SettingsSceneController>();
        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BG/MainMenuBG.png");
        if (background != null)
        {
            SerializedObject serializedController = new(settingsController);
            serializedController.FindProperty("backgroundSprite").objectReferenceValue = background;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        settingsController.BuildUi();

        EditorSceneManager.SaveScene(scene, SettingsScenePath);
    }

    private static void CreateAuthScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject camera = new("Main Camera");
        Camera cameraComponent = camera.AddComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = new Color(0.09f, 0.12f, 0.10f, 1f);
        camera.tag = "MainCamera";

        GameObject firebaseManager = new("FirebaseManager");
        firebaseManager.AddComponent<FirebaseInitializer>();
        firebaseManager.AddComponent<FirebaseAuthService>();
        firebaseManager.AddComponent<FirebaseSaveService>();

        GameObject authController = new("AuthUIController");
        AuthUIController controller = authController.AddComponent<AuthUIController>();
        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BG/MainMenuBG.png");
        if (background != null)
        {
            SerializedObject serializedController = new(controller);
            serializedController.FindProperty("backgroundSprite").objectReferenceValue = background;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        controller.EnsureUi();

        EditorSceneManager.SaveScene(scene, AuthScenePath);
    }

    private static void UpdateMenuScene()
    {
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        if (Object.FindFirstObjectByType<FirebaseInitializer>() == null)
        {
            GameObject firebaseManager = new("FirebaseManager");
            firebaseManager.AddComponent<FirebaseInitializer>();
            firebaseManager.AddComponent<FirebaseAuthService>();
            firebaseManager.AddComponent<FirebaseSaveService>();
        }

        if (Object.FindFirstObjectByType<MenuUIController>() == null && Object.FindFirstObjectByType<MainMenuController>() == null)
        {
            GameObject menuController = new("MenuUIController");
            menuController.AddComponent<MenuUIController>();
        }

        EnsureModernMenuUiInCurrentScene();
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureModernMenuUiInCurrentScene()
    {
        if (GameObject.Find("ModernMenuCanvas") != null)
        {
            EnsureMenuControllerReferences();
            return;
        }

        GameObject canvasObject = new("ModernMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject fade = new("ModernMenuFade", typeof(RectTransform), typeof(Image));
        fade.transform.SetParent(canvasObject.transform, false);
        RectTransform fadeRect = fade.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fade.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.03f, 0.22f);

        GameObject panel = new("ModernMenuPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(360f, 360f);
        panelRect.anchoredPosition = new Vector2(260f, 0f);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = CreateRoundedEditorSprite(64, 18);
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.055f, 0.082f, 0.068f, 0.78f);
        panel.GetComponent<CanvasGroup>().alpha = 1f;

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 30, 30);
        layout.spacing = 16f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateEditorLabel(panel.transform, "FarmSim", 38, 52f);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;

        Button play = CreateEditorButton(panel.transform, "ModernPlayButton", "Играть", new Color(0.52f, 0.78f, 0.18f, 0.98f));
        Button settings = CreateEditorButton(panel.transform, "ModernSettingsButton", "Настройки", new Color(0.16f, 0.48f, 0.82f, 0.97f));
        Button exit = CreateEditorButton(panel.transform, "ModernExitButton", "Выйти", new Color(0.78f, 0.25f, 0.12f, 0.97f));

        EnsureMenuControllerReferences(play, settings, exit, canvas, fade.GetComponent<Image>(), panelRect);
    }

    private static void EnsureMenuControllerReferences(
        Button play = null,
        Button settings = null,
        Button exit = null,
        Canvas canvas = null,
        Image fade = null,
        RectTransform panel = null)
    {
        MenuUIController controller = Object.FindFirstObjectByType<MenuUIController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            GameObject existingManager = GameObject.Find("MainMenuManager");
            if (existingManager != null)
            {
                controller = existingManager.GetComponent<MenuUIController>();
            }
        }

        if (controller == null)
        {
            GameObject menuController = new("MenuUIController");
            controller = menuController.AddComponent<MenuUIController>();
        }

        play ??= GameObject.Find("ModernPlayButton")?.GetComponent<Button>();
        settings ??= GameObject.Find("ModernSettingsButton")?.GetComponent<Button>();
        exit ??= GameObject.Find("ModernExitButton")?.GetComponent<Button>();
        canvas ??= GameObject.Find("ModernMenuCanvas")?.GetComponent<Canvas>();
        fade ??= GameObject.Find("ModernMenuFade")?.GetComponent<Image>();
        panel ??= GameObject.Find("ModernMenuPanel")?.GetComponent<RectTransform>();

        SerializedObject serialized = new(controller);
        serialized.FindProperty("playButton").objectReferenceValue = play;
        serialized.FindProperty("settingsButton").objectReferenceValue = settings;
        serialized.FindProperty("exitButton").objectReferenceValue = exit;
        serialized.FindProperty("modernMenuCanvas").objectReferenceValue = canvas;
        serialized.FindProperty("modernMenuFade").objectReferenceValue = fade;
        serialized.FindProperty("modernMenuPanel").objectReferenceValue = panel;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TextMeshProUGUI CreateEditorLabel(Transform parent, string text, int fontSize, float height)
    {
        GameObject label = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        label.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18;
        tmp.fontSizeMax = fontSize;
        label.GetComponent<LayoutElement>().preferredHeight = height;
        return tmp;
    }

    private static Button CreateEditorButton(Transform parent, string objectName, string text, Color color)
    {
        GameObject root = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.sprite = CreateRoundedEditorSprite(64, 14);
        image.type = Image.Type.Sliced;
        image.color = color;
        root.GetComponent<LayoutElement>().preferredHeight = 58f;

        Button button = root.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.14f);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TextMeshProUGUI label = CreateEditorLabel(root.transform, text, 22, 58f);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return button;
    }

    private static Sprite CreateRoundedEditorSprite(int size, int radius)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = $"EditorRounded_{radius}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new(1f, 1f, 1f, 0f);
        Color white = Color.white;
        Color[] pixels = new Color[size * size];
        float max = size - 1;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = x < radius ? radius : x > max - radius ? max - radius : x;
                float cy = y < radius ? radius : y > max - radius ? max - radius : y;
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                pixels[y * size + x] = distance <= radius ? white : clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private static void UpdateGameScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        if (Object.FindFirstObjectByType<GameSceneAuthGuard>() == null)
        {
            GameObject guard = new("GameSceneAuthGuard");
            guard.AddComponent<GameSceneAuthGuard>();
        }

        EditorSceneManager.SaveScene(scene);
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene != null && !string.IsNullOrWhiteSpace(scene.path))
            .ToList();

        RemoveScene(scenes, AuthScenePath);
        RemoveScene(scenes, MenuScenePath);
        RemoveScene(scenes, SettingsScenePath);
        RemoveScene(scenes, GameScenePath);

        scenes.Insert(0, new EditorBuildSettingsScene(AuthScenePath, true));
        scenes.Insert(1, new EditorBuildSettingsScene(MenuScenePath, true));
        scenes.Insert(2, new EditorBuildSettingsScene(SettingsScenePath, true));
        scenes.Insert(3, new EditorBuildSettingsScene(GameScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void RemoveScene(List<EditorBuildSettingsScene> scenes, string path)
    {
        scenes.RemoveAll(scene => string.Equals(scene.path, path, System.StringComparison.OrdinalIgnoreCase));
    }
}
