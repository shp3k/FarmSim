using System.Collections;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class MenuUIController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] protected string authSceneName = "AuthScene";
    [SerializeField] protected string gameSceneName = "MainScene";
    [SerializeField] protected string settingsSceneName = "SettingsScene";

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float slideDuration = 0.2f;
    [SerializeField] private Vector2 panelStartOffset = new(-460f, 0f);

    [Header("Buttons")]
    [SerializeField] protected Button playButton;
    [SerializeField] protected Button settingsButton;
    [SerializeField] protected Button exitButton;

    [Header("Scene UI")]
    [SerializeField] private Canvas modernMenuCanvas;
    [SerializeField] private RectTransform modernMenuPanel;
    [SerializeField] private Image modernMenuFade;

    private Canvas menuCanvas;
    private Image fadeOverlay;
    private RectTransform panelRect;
    private CanvasGroup panelCanvasGroup;
    private Vector2 panelTargetPosition;

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= EnsureEditorMenuUi;
            EditorApplication.delayCall += EnsureEditorMenuUi;
        }
#endif
    }

    protected virtual void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureServices();
        HideLegacyMenu();
        EnsureModernMenu();
        HookButtons();
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            StartCoroutine(PlayIntroAnimation());
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (WasEscapePressed())
        {
            ExitGame();
        }
    }

    protected virtual void OnDestroy()
    {
        if (playButton != null) playButton.onClick.RemoveListener(Play);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
        if (exitButton != null) exitButton.onClick.RemoveListener(ExitGame);
    }

    public void Play()
    {
        FirebaseAuthService auth = FirebaseAuthService.Instance;
        if (auth != null && !auth.IsSignedIn())
        {
            SceneTransitionManager.LoadScene(authSceneName);
            return;
        }

        SceneTransitionManager.LoadScene(gameSceneName);
    }

    public void OpenSettings()
    {
        SceneTransitionManager.LoadScene(settingsSceneName);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        Debug.Log("Exit button pressed. Application.Quit is ignored in the Unity Editor.");
#else
        Application.Quit();
#endif
    }

    protected void HookButtons()
    {
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (exitButton != null) exitButton.onClick.AddListener(ExitGame);
    }

    private static void EnsureServices()
    {
        if (FirebaseInitializer.Instance != null || FindFirstObjectByType<FirebaseInitializer>() != null)
        {
            return;
        }

        GameObject manager = new("FirebaseManager");
        manager.AddComponent<FirebaseInitializer>();
        manager.AddComponent<FirebaseAuthService>();
        manager.AddComponent<FirebaseSaveService>();
    }

    private static void HideLegacyMenu()
    {
        SetInactiveIfExists("ForButton");
        SetInactiveIfExists("PlayButton");
        SetInactiveIfExists("QuitButton");
        SetInactiveIfExists("SettingsButton");
        SetInactiveIfExists("LogoutButton");
        SetInactiveIfExists("SettingsPanel");
    }

    private static void SetInactiveIfExists(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            target.SetActive(false);
        }
    }

    public void EnsureModernMenu()
    {
        FindExistingUiReferences();
        if (modernMenuCanvas != null && modernMenuPanel != null && playButton != null && settingsButton != null && exitButton != null)
        {
            menuCanvas = modernMenuCanvas;
            panelRect = modernMenuPanel;
            panelCanvasGroup = modernMenuPanel.GetComponent<CanvasGroup>() ?? modernMenuPanel.gameObject.AddComponent<CanvasGroup>();
            fadeOverlay = modernMenuFade;
            return;
        }

        BuildModernMenu();
    }

    private void FindExistingUiReferences()
    {
        modernMenuCanvas ??= GameObject.Find("ModernMenuCanvas")?.GetComponent<Canvas>();
        modernMenuPanel ??= GameObject.Find("ModernMenuPanel")?.GetComponent<RectTransform>();
        modernMenuFade ??= GameObject.Find("ModernMenuFade")?.GetComponent<Image>();
        playButton ??= GameObject.Find("ModernPlayButton")?.GetComponent<Button>();
        settingsButton ??= GameObject.Find("ModernSettingsButton")?.GetComponent<Button>();
        exitButton ??= GameObject.Find("ModernExitButton")?.GetComponent<Button>();
    }

    public void BuildModernMenu()
    {
        menuCanvas = modernMenuCanvas != null ? modernMenuCanvas : UIInputUtility.FindSceneCanvas();
        if (menuCanvas == null)
        {
            GameObject canvasObject = new("ModernMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            menuCanvas = canvasObject.GetComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
        else
        {
            menuCanvas.gameObject.name = "ModernMenuCanvas";
        }

        modernMenuCanvas = menuCanvas;

        GameObject overlayObject = new("ModernMenuFade", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(menuCanvas.transform, false);
        fadeOverlay = overlayObject.GetComponent<Image>();
        modernMenuFade = fadeOverlay;
        fadeOverlay.color = new Color(0.02f, 0.04f, 0.03f, 1f);
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        GameObject panelObject = new("ModernMenuPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(VerticalLayoutGroup));
        panelObject.transform.SetParent(menuCanvas.transform, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        modernMenuPanel = panelRect;
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(360f, 360f);
        panelTargetPosition = ResolvePanelTargetPosition();
        panelRect.anchoredPosition = panelTargetPosition + panelStartOffset;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = RoundedSpriteCache.Get(64, 18, Color.white);
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.055f, 0.082f, 0.068f, 0.78f);

        panelCanvasGroup = panelObject.GetComponent<CanvasGroup>();
        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 30, 30);
        layout.spacing = 16f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateLabel(panelObject.transform, "FarmSim", 38, 52f);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;

        playButton = CreateButton(panelObject.transform, "\u0418\u0433\u0440\u0430\u0442\u044c", new Color(0.52f, 0.78f, 0.18f, 0.98f));
        playButton.gameObject.name = "ModernPlayButton";
        settingsButton = CreateButton(panelObject.transform, "\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438", new Color(0.16f, 0.48f, 0.82f, 0.97f));
        settingsButton.gameObject.name = "ModernSettingsButton";
        exitButton = CreateButton(panelObject.transform, "\u0412\u044b\u0439\u0442\u0438", new Color(0.78f, 0.25f, 0.12f, 0.97f));
        exitButton.gameObject.name = "ModernExitButton";
    }

#if UNITY_EDITOR
    private void EnsureEditorMenuUi()
    {
        if (this == null || Application.isPlaying)
        {
            return;
        }

        if (!gameObject.scene.IsValid() || gameObject.scene.name != "MainMenu")
        {
            return;
        }

        HideLegacyMenu();
        EnsureModernMenu();
        SetEditorMenuVisible();
        EditorUtility.SetDirty(this);
        if (modernMenuCanvas != null)
        {
            EditorUtility.SetDirty(modernMenuCanvas.gameObject);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private void SetEditorMenuVisible()
    {
        if (panelRect != null)
        {
            panelTargetPosition = ResolvePanelTargetPosition();
            panelRect.anchoredPosition = panelTargetPosition;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        if (fadeOverlay != null)
        {
            fadeOverlay.color = new Color(0.02f, 0.04f, 0.03f, 0.22f);
        }
    }
#endif

    private IEnumerator PlayIntroAnimation()
    {
        EnsureModernMenu();
        if (panelRect == null)
        {
            yield break;
        }

        float elapsed = 0f;
        panelTargetPosition = ResolvePanelTargetPosition();
        Vector2 start = panelTargetPosition + panelStartOffset;
        Color overlayColor = new(0.02f, 0.04f, 0.03f, 1f);
        float safeSlideDuration = Mathf.Min(Mathf.Max(0.01f, slideDuration), 0.25f);
        float safeFadeDuration = Mathf.Min(Mathf.Max(0.01f, fadeDuration), 0.25f);
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        while (elapsed < safeSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float slideT = Mathf.Clamp01(elapsed / safeSlideDuration);
            float fadeT = Mathf.Clamp01(elapsed / safeFadeDuration);
            float easedSlide = EaseOutCubic(slideT);
            float easedFade = EaseOutCubic(fadeT);

            if (panelRect != null)
            {
                panelRect.anchoredPosition = Vector2.LerpUnclamped(start, panelTargetPosition, easedSlide);
            }

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = easedFade;
            }

            if (fadeOverlay != null)
            {
                overlayColor.a = Mathf.Lerp(1f, 0.22f, easedFade);
                fadeOverlay.color = overlayColor;
            }

            yield return null;
        }

        if (panelRect != null) panelRect.anchoredPosition = panelTargetPosition;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        if (fadeOverlay != null) fadeOverlay.color = new Color(0.02f, 0.04f, 0.03f, 0.22f);
    }

    private static float EaseOutCubic(float value)
    {
        return 1f - Mathf.Pow(1f - Mathf.Clamp01(value), 3f);
    }

    private Vector2 ResolvePanelTargetPosition()
    {
        float panelWidth = panelRect != null ? panelRect.sizeDelta.x : 360f;
        float leftMargin = Mathf.Clamp(Screen.width * 0.075f, 70f, 120f);
        return new Vector2(leftMargin + panelWidth * 0.5f, 0f);
    }

    protected static bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string text, int size, float height)
    {
        GameObject label = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        label.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18;
        tmp.fontSizeMax = size;
        label.GetComponent<LayoutElement>().preferredHeight = height;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string text, Color color)
    {
        GameObject root = new(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.transform.SetParent(parent, false);

        Image image = root.GetComponent<Image>();
        image.sprite = RoundedSpriteCache.Get(64, 14, Color.white);
        image.type = Image.Type.Sliced;
        image.color = color;

        Button button = root.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        root.GetComponent<LayoutElement>().preferredHeight = 58f;
        TextMeshProUGUI label = CreateLabel(root.transform, text, 22, 58f);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return button;
    }
}

public static class RoundedSpriteCache
{
    private static Sprite sprite14;
    private static Sprite sprite18;
    private static Sprite sprite22;

    public static Sprite Get(int size, int radius, Color color)
    {
        if (radius <= 14)
        {
            sprite14 ??= Create(size, 14, color);
            return sprite14;
        }

        if (radius <= 18)
        {
            sprite18 ??= Create(size, 18, color);
            return sprite18;
        }

        sprite22 ??= Create(size, 22, color);
        return sprite22;
    }

    private static Sprite Create(int size, int radius, Color color)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = $"Rounded_{radius}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new(1f, 1f, 1f, 0f);
        Color[] pixels = new Color[size * size];
        float max = size - 1;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = x < radius ? radius : x > max - radius ? max - radius : x;
                float cy = y < radius ? radius : y > max - radius ? max - radius : y;
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                pixels[y * size + x] = distance <= radius ? color : clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}

