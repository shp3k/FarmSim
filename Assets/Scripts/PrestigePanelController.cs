using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PrestigePanelController : MonoBehaviour
{
    public static PrestigePanelController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PrestigeManager prestigeManager;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new(920f, 640f);

    private Canvas targetCanvas;
    private RectTransform rootRect;
    private RectTransform upgradesContent;
    private TMP_Text summaryText;
    private TMP_Text statusText;
    private UIPanelAnimator panelAnimator;
    private bool isOpen;

    public bool IsOpen => isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachToGameplayScene()
    {
        EnsureInScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInScene(scene);
    }

    private static void EnsureInScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "MainScene")
        {
            return;
        }

        if (FindFirstObjectByType<PrestigePanelController>() != null)
        {
            return;
        }

        GameObject host = new GameObject("PrestigePanelController");
        host.AddComponent<PrestigePanelController>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
        BuildUiIfNeeded();
        isOpen = false;
        panelAnimator?.ApplyImmediate(false);
    }

    private void OnEnable()
    {
        PrestigeManager.OnPrestigeChanged += Refresh;
    }

    private void OnDisable()
    {
        PrestigeManager.OnPrestigeChanged -= Refresh;
    }

    private void Update()
    {
        if (!UIInputUtility.IsTextInputFocused() && WasPPressedThisFrame())
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        if (!isOpen && IsBlockingPanelOpen())
        {
            return;
        }

        SetOpen(!isOpen);
    }

    public void Close()
    {
        SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        isOpen = open;
        if (rootRect == null)
        {
            BuildUiIfNeeded();
        }

        if (rootRect == null)
        {
            Debug.LogWarning("PrestigePanelController: Prestige panel could not be created. Check that MainScene has a Canvas.");
            return;
        }

        panelAnimator ??= rootRect.GetComponent<UIPanelAnimator>() ?? rootRect.gameObject.AddComponent<UIPanelAnimator>();
        if (open)
        {
            panelAnimator.Show();
        }
        else
        {
            panelAnimator.Hide();
        }

        if (open)
        {
            FarmEncyclopediaController.Instance?.Close();
            Refresh();
        }
    }

    private void ResolveReferences()
    {
        if (prestigeManager == null)
        {
            prestigeManager = PrestigeManager.Instance ?? FindFirstObjectByType<PrestigeManager>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = UIInputUtility.FindSceneCanvas();
        }
    }

    private void BuildUiIfNeeded()
    {
        ResolveReferences();
        if (rootRect != null || targetCanvas == null)
        {
            return;
        }

        GameObject root = CreateUiObject("PrestigePanel", targetCanvas.transform);
        rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image scrim = root.AddComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.52f);
        panelAnimator = root.AddComponent<UIPanelAnimator>();

        RectTransform panelRect = CreateUiObject("Panel", rootRect).GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;

        Image panelBg = panelRect.gameObject.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.11f, 0.12f, 0.97f);

        VerticalLayoutGroup layout = panelRect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 16, 18);
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        RectTransform headerRow = CreateUiObject("HeaderRow", panelRect).GetComponent<RectTransform>();
        HorizontalLayoutGroup headerLayout = headerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 10f;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandWidth = false;
        LayoutElement headerElement = headerRow.gameObject.AddComponent<LayoutElement>();
        headerElement.preferredHeight = 46f;

        TMP_Text title = CreateText("Title", headerRow, 28f, FontStyles.Bold);
        title.text = "Престиж";
        LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;

        Button closeButton = CreateButton(headerRow, "Закрыть", () => SetOpen(false));
        Image closeImage = closeButton.GetComponent<Image>();
        if (closeImage != null)
        {
            closeImage.color = new Color(0.2f, 0.23f, 0.26f, 0.95f);
        }

        closeButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 116f;

        summaryText = CreateText("Summary", panelRect, 18f, FontStyles.Normal);
        summaryText.color = new Color(0.92f, 0.96f, 1f, 0.95f);
        summaryText.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

        Button prestigeButton = CreateButton(panelRect, "Престиж", HandlePrestigeClicked);
        prestigeButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

        RectTransform scroll = CreateScrollArea(panelRect, out upgradesContent);
        LayoutElement scrollLayout = scroll.gameObject.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.minHeight = 330f;

        statusText = CreateText("Status", panelRect, 17f, FontStyles.Normal);
        statusText.color = new Color(0.9f, 0.95f, 1f, 0.9f);
        statusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
    }

    private void Refresh()
    {
        ResolveReferences();
        if (prestigeManager == null || summaryText == null || upgradesContent == null)
        {
            return;
        }

        bool canPrestige = prestigeManager.CanPrestige(out string reason);
        int pendingPoints = prestigeManager.CalculatePrestigePointsForCurrentRun();
        summaryText.text =
            $"ОП: {prestigeManager.PrestigePoints}\n" +
            $"Всего престижей: {prestigeManager.TotalPrestigeCount}\n" +
            $"ОП за текущий престиж: {pendingPoints}\n" +
            $"Доступ: {(canPrestige ? "можно выполнить" : reason)}";

        ClearChildren(upgradesContent);
        foreach (PrestigeUpgradeDefinition definition in prestigeManager.GetUpgradeDefinitions())
        {
            CreateUpgradeEntry(definition);
        }
    }

    private void CreateUpgradeEntry(PrestigeUpgradeDefinition definition)
    {
        GameObject row = CreateUiObject("Upgrade", upgradesContent);
        Image bg = row.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.15f, 0.17f, 0.92f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        row.AddComponent<LayoutElement>().preferredHeight = 112f;

        int level = prestigeManager.GetUpgradeLevel(definition.Id);
        TMP_Text info = CreateText("Info", row.transform, 16f, FontStyles.Normal);
        info.text =
            $"{definition.Title}  ур. {level}/{definition.MaxLevel}\n" +
            $"{definition.Description}\n" +
            $"Стоимость: {definition.Cost} ОП";
        info.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        Button buyButton = CreateButton(row.transform, level >= definition.MaxLevel ? "Макс." : "Купить", () =>
        {
            prestigeManager.TryBuyUpgrade(definition.Id, out string message);
            statusText.text = message;
            Refresh();
        });
        buyButton.interactable = level < definition.MaxLevel && prestigeManager.PrestigePoints >= definition.Cost;
        buyButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 116f;
    }

    private void HandlePrestigeClicked()
    {
        if (prestigeManager == null)
        {
            return;
        }

        prestigeManager.TryPerformPrestige(out string message);
        statusText.text = message;
        Refresh();
    }

    private bool IsBlockingPanelOpen()
    {
        return SideShopPanelController.Instance != null && SideShopPanelController.Instance.IsOpen;
    }

    private RectTransform CreateScrollArea(Transform parent, out RectTransform contentRect)
    {
        GameObject scrollObject = CreateUiObject("Scroll", parent);
        Image viewportImage = scrollObject.AddComponent<Image>();
        viewportImage.color = new Color(0.03f, 0.05f, 0.06f, 0.6f);
        Mask mask = scrollObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        RectTransform viewport = scrollObject.GetComponent<RectTransform>();
        GameObject contentObject = CreateUiObject("Content", viewport);
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(8f, 0f);
        contentRect.offsetMax = new Vector2(-8f, 0f);

        VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.padding = new RectOffset(0, 0, 8, 8);
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = contentRect;
        return viewport;
    }

    private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = CreateUiObject($"{label}Button", parent);
        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.19f, 0.55f, 0.36f, 0.95f);
        Button button = obj.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        TMP_Text text = CreateText("Label", obj.transform, 16f, FontStyles.Bold);
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, float size, FontStyles style)
    {
        GameObject obj = CreateUiObject(name, parent);
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.GetComponent<RectTransform>().SetParent(parent, false);
        return obj;
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private static bool WasPPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.P);
#endif
    }
}
