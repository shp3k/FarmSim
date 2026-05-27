using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsSceneController : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private string authSceneName = "AuthScene";
    [SerializeField] private Sprite backgroundSprite;

    [Header("Scene UI")]
    [SerializeField] private Canvas settingsCanvas;
    [SerializeField] private RectTransform settingsPanel;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TextMeshProUGUI musicValueText;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TextMeshProUGUI sfxValueText;
    [SerializeField] private Button backButton;
    [SerializeField] private Button logoutButton;

    private const string MusicVolumeKey = "farmsim_music_volume";
    private const string SFXVolumeKey = "farmsim_sfx_volume";

    private void Awake()
    {
        EnsureServices();
        EnsureEventSystem();
        EnsureUi();
        HookButtons();
        LoadAudioVolumes();
    }

    private void OnDestroy()
    {
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(SetSFXVolume);
        if (backButton != null) backButton.onClick.RemoveListener(BackToMenu);
        if (logoutButton != null) logoutButton.onClick.RemoveListener(Logout);
    }

    private void Update()
    {
        if (WasEscapePressed())
        {
            SceneTransitionManager.LoadScene(menuSceneName);
        }
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

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif
    }

    private void EnsureUi()
    {
        if (settingsPanel != null && musicSlider != null && musicValueText != null && sfxSlider != null && sfxValueText != null && backButton != null && logoutButton != null)
        {
            NormalizeSliderVisuals(musicSlider);
            NormalizeSliderVisuals(sfxSlider);
            PlaceSFXRowAfterMusicRow();
            return;
        }

        FindExistingUiReferences();
        if (settingsPanel != null && backButton != null && logoutButton != null)
        {
            EnsureAudioRows();
            return;
        }

        BuildUi();
    }

    private void FindExistingUiReferences()
    {
        settingsCanvas ??= UIInputUtility.FindSceneCanvas();
        GameObject panelObject = GameObject.Find("SettingsPanel");
        if (panelObject != null)
        {
            settingsPanel = panelObject.GetComponent<RectTransform>();
        }

        musicSlider ??= GameObject.Find("MusicSlider")?.GetComponent<Slider>();
        musicSlider ??= GameObject.Find("VolumeSlider")?.GetComponent<Slider>();
        musicValueText ??= GameObject.Find("MusicValueText")?.GetComponent<TextMeshProUGUI>();
        musicValueText ??= GameObject.Find("VolumeValueText")?.GetComponent<TextMeshProUGUI>();
        sfxSlider ??= GameObject.Find("SFXSlider")?.GetComponent<Slider>();
        sfxValueText ??= GameObject.Find("SFXValueText")?.GetComponent<TextMeshProUGUI>();
        backButton ??= GameObject.Find("BackButton")?.GetComponent<Button>();
        logoutButton ??= GameObject.Find("LogoutButton")?.GetComponent<Button>();
    }

    private void EnsureAudioRows()
    {
        if (settingsPanel == null)
        {
            return;
        }

        if (musicSlider == null || musicValueText == null)
        {
            CreateAudioRow(settingsPanel, "MusicRow", "Музыка", out musicSlider, out musicValueText);
        }
        else
        {
            musicSlider.gameObject.name = "MusicSlider";
            musicValueText.gameObject.name = "MusicValueText";
        }

        if (sfxSlider == null || sfxValueText == null)
        {
            CreateAudioRow(settingsPanel, "SFXRow", "SFX", out sfxSlider, out sfxValueText);
        }

        NormalizeSliderVisuals(musicSlider);
        NormalizeSliderVisuals(sfxSlider);
        PlaceSFXRowAfterMusicRow();
    }

    private void PlaceSFXRowAfterMusicRow()
    {
        if (musicSlider == null || sfxSlider == null)
        {
            return;
        }

        Transform musicRow = musicSlider.transform.parent;
        Transform sfxRow = sfxSlider.transform.parent;
        if (musicRow == null || sfxRow == null || musicRow.parent != sfxRow.parent)
        {
            return;
        }

        sfxRow.SetSiblingIndex(musicRow.GetSiblingIndex() + 1);
    }

    public void BuildUi()
    {
        GameObject canvasObject = new("SettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        settingsCanvas = canvasObject.GetComponent<Canvas>();
        settingsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject background = new("SettingsBackground", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(settingsCanvas.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.color = backgroundSprite != null ? Color.white : new Color(0.08f, 0.13f, 0.1f, 1f);

        GameObject fade = new("SettingsFade", typeof(RectTransform), typeof(Image));
        fade.transform.SetParent(settingsCanvas.transform, false);
        RectTransform fadeRect = fade.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fade.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.03f, 0.42f);

        GameObject panel = new("SettingsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(settingsCanvas.transform, false);
        settingsPanel = panel.GetComponent<RectTransform>();
        settingsPanel.anchorMin = new Vector2(0.5f, 0.5f);
        settingsPanel.anchorMax = new Vector2(0.5f, 0.5f);
        settingsPanel.pivot = new Vector2(0.5f, 0.5f);
        settingsPanel.sizeDelta = new Vector2(620f, 620f);
        settingsPanel.anchoredPosition = Vector2.zero;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = RoundedSpriteCache.Get(64, 22, Color.white);
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.045f, 0.075f, 0.06f, 0.86f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 42, 42);
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateLabel(panel.transform, "Настройки", 36, 52f);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;

        CreateAudioRow(panel.transform, "MusicRow", "Музыка", out musicSlider, out musicValueText);
        CreateAudioRow(panel.transform, "SFXRow", "SFX", out sfxSlider, out sfxValueText);
        CreateSpacer(panel.transform, 6f);
        backButton = CreateButton(panel.transform, "Назад", new Color(0.95f, 0.97f, 0.91f, 0.98f), new Color(0.08f, 0.12f, 0.1f, 1f));
        backButton.gameObject.name = "BackButton";
        logoutButton = CreateButton(panel.transform, "Выйти из аккаунта", new Color(0.95f, 0.97f, 0.91f, 0.98f), new Color(0.45f, 0.12f, 0.07f, 1f));
        logoutButton.gameObject.name = "LogoutButton";
    }

    private void HookButtons()
    {
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(SetSFXVolume);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(BackToMenu);
            backButton.onClick.AddListener(BackToMenu);
        }

        if (logoutButton != null)
        {
            logoutButton.onClick.RemoveListener(Logout);
            logoutButton.onClick.AddListener(Logout);
        }
    }

    private void CreateAudioRow(Transform parent, string rowName, string labelText, out Slider slider, out TextMeshProUGUI valueText)
    {
        GameObject row = new(rowName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 120f;
        VerticalLayoutGroup layout = row.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        valueText = CreateLabel(row.transform, $"{labelText}: 100%", 24, 34f);
        valueText.gameObject.name = rowName == "MusicRow" ? "MusicValueText" : "SFXValueText";
        valueText.alignment = TextAlignmentOptions.Center;

        GameObject sliderObject = new(rowName == "MusicRow" ? "MusicSlider" : "SFXSlider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        sliderObject.transform.SetParent(row.transform, false);
        sliderObject.GetComponent<LayoutElement>().preferredHeight = 56f;
        slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        NormalizeSliderVisuals(slider);
    }

    private static void NormalizeSliderVisuals(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = null;
        slider.handleRect = null;
        slider.targetGraphic = null;

        Transform sliderTransform = slider.transform;
        for (int i = sliderTransform.childCount - 1; i >= 0; i--)
        {
            GameObject child = sliderTransform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }

        CreateSliderVisuals(sliderTransform, slider);
    }

    private static void CreateSliderVisuals(Transform sliderTransform, Slider slider)
    {
        RectTransform sliderRect = sliderTransform.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(0f, 56f);

        GameObject background = new("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(sliderTransform, false);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(1f, 0.5f);
        bgRect.offsetMin = new Vector2(0f, -7f);
        bgRect.offsetMax = new Vector2(0f, 7f);
        bgRect.anchoredPosition = Vector2.zero;
        Image bgImage = background.GetComponent<Image>();
        bgImage.sprite = RoundedSpriteCache.Get(64, 14, Color.white);
        bgImage.type = Image.Type.Sliced;
        bgImage.color = new Color(1f, 1f, 1f, 0.25f);

        GameObject fillArea = new("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderTransform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(10f, 0f);
        fillAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject fill = new("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.offsetMin = new Vector2(0f, -7f);
        fillRect.offsetMax = new Vector2(0f, 7f);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.sprite = RoundedSpriteCache.Get(64, 14, Color.white);
        fillImage.type = Image.Type.Sliced;
        fillImage.color = new Color(0.52f, 0.78f, 0.18f, 0.98f);
        slider.fillRect = fill.GetComponent<RectTransform>();

        GameObject handleArea = new("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderTransform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(16f, 0f);
        handleAreaRect.offsetMax = new Vector2(-16f, 0f);

        GameObject handle = new("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(32f, 32f);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.sprite = RoundedSpriteCache.Get(64, 22, Color.white);
        handleImage.type = Image.Type.Sliced;
        handleImage.color = Color.white;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
    }

    private void LoadAudioVolumes()
    {
        float musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        float sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);

        AudioManager.Instance?.SetMusicVolume(musicVolume);
        AudioManager.Instance?.SetSFXVolume(sfxVolume);

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(musicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(sfxVolume);
        }

        UpdateMusicVolumeLabel(musicVolume);
        UpdateSFXVolumeLabel(sfxVolume);
    }

    private void SetMusicVolume(float value)
    {
        float clamped = Mathf.Clamp01(value);
        AudioManager.Instance?.SetMusicVolume(clamped);
        PlayerPrefs.SetFloat(MusicVolumeKey, clamped);
        PlayerPrefs.Save();
        UpdateMusicVolumeLabel(clamped);
    }

    private void SetSFXVolume(float value)
    {
        float clamped = Mathf.Clamp01(value);
        AudioManager.Instance?.SetSFXVolume(clamped);
        PlayerPrefs.SetFloat(SFXVolumeKey, clamped);
        PlayerPrefs.Save();
        UpdateSFXVolumeLabel(clamped);
    }

    private void UpdateMusicVolumeLabel(float value)
    {
        if (musicValueText != null)
        {
            musicValueText.text = $"Музыка: {Mathf.RoundToInt(value * 100f)}%";
        }
    }

    private void UpdateSFXVolumeLabel(float value)
    {
        if (sfxValueText != null)
        {
            sfxValueText.text = $"SFX: {Mathf.RoundToInt(value * 100f)}%";
        }
    }

    private void Logout()
    {
        FirebaseAuthService.Instance?.Logout();
        SceneTransitionManager.LoadScene(authSceneName);
    }

    private void BackToMenu()
    {
        SceneTransitionManager.LoadScene(menuSceneName);
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
        tmp.fontSizeMin = 16;
        tmp.fontSizeMax = size;
        label.GetComponent<LayoutElement>().preferredHeight = height;
        return tmp;
    }

    private static void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = new("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        spacer.GetComponent<LayoutElement>().preferredHeight = height;
    }

    private static Button CreateButton(Transform parent, string text, Color color, Color textColor)
    {
        GameObject root = new(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.sprite = RoundedSpriteCache.Get(64, 14, Color.white);
        image.type = Image.Type.Sliced;
        image.color = color;
        root.GetComponent<LayoutElement>().preferredHeight = 64f;

        Button button = root.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TextMeshProUGUI label = CreateLabel(root.transform, text, 23, 64f);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = textColor;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return button;
    }

    private static bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
