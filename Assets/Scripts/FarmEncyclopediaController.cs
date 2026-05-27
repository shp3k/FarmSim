using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FarmEncyclopediaController : MonoBehaviour
{
    public static FarmEncyclopediaController Instance { get; private set; }

    private enum Section
    {
        Crops = 0,
        Animals = 1,
        Levels = 2,
        Prestige = 3
    }

    [Header("References")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private ProgressionManager progressionManager;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new(980f, 640f);

    private Canvas targetCanvas;
    private RectTransform rootRect;
    private RectTransform contentRect;
    private TMP_Text headerText;
    private UIPanelAnimator panelAnimator;
    private readonly Dictionary<Section, Button> tabButtons = new();
    private Section currentSection = Section.Crops;
    private bool isOpen;
    public bool IsOpen => isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachToGameplayScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "MainScene")
        {
            return;
        }

        if (FindFirstObjectByType<FarmEncyclopediaController>() != null)
        {
            return;
        }

        GameObject host = new GameObject("FarmEncyclopediaController");
        host.AddComponent<FarmEncyclopediaController>();
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

    private void Update()
    {
        if (!UIInputUtility.IsTextInputFocused() && WasJPressedThisFrame())
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        SideShopPanelController shopPanel = SideShopPanelController.Instance ?? FindFirstObjectByType<SideShopPanelController>();
        if (!isOpen && shopPanel != null && shopPanel.IsOpen)
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
            Debug.LogWarning("FarmEncyclopediaController: encyclopedia panel could not be created. Check that MainScene has a Canvas.");
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
            PrestigePanelController.Instance?.Close();
            RefreshContent();
        }
    }

    private void ResolveReferences()
    {
        if (shopManager == null)
        {
            shopManager = FindFirstObjectByType<ShopManager>();
        }

        if (progressionManager == null)
        {
            progressionManager = ProgressionManager.Instance ?? FindFirstObjectByType<ProgressionManager>();
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

        GameObject root = CreateUiObject("FarmEncyclopedia", targetCanvas.transform);
        rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image scrim = root.AddComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.52f);
        panelAnimator = root.AddComponent<UIPanelAnimator>();

        GameObject panel = CreateUiObject("Panel", rootRect);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.11f, 0.12f, 0.97f);

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(18, 18, 16, 18);
        panelLayout.spacing = 12f;
        panelLayout.childControlHeight = true;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childForceExpandWidth = true;

        RectTransform headerRow = CreateUiObject("HeaderRow", panelRect).GetComponent<RectTransform>();
        HorizontalLayoutGroup headerLayout = headerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 10f;
        headerLayout.childAlignment = TextAnchor.MiddleCenter;
        headerLayout.childControlHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandWidth = false;
        LayoutElement headerRowLayout = headerRow.gameObject.AddComponent<LayoutElement>();
        headerRowLayout.preferredHeight = 46f;

        headerText = CreateText("Header", headerRow, 28f, FontStyles.Bold);
        headerText.text = "Справочник FarmSim";
        LayoutElement headerLayoutElement = headerText.gameObject.AddComponent<LayoutElement>();
        headerLayoutElement.flexibleWidth = 1f;

        Button closeButton = CreateButton(headerRow, "Закрыть", () => SetOpen(false));
        LayoutElement closeLayout = closeButton.gameObject.AddComponent<LayoutElement>();
        closeLayout.preferredWidth = 116f;

        RectTransform tabsRow = CreateUiObject("TabsRow", panelRect).GetComponent<RectTransform>();
        HorizontalLayoutGroup tabsLayout = tabsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 8f;
        tabsLayout.childControlHeight = true;
        tabsLayout.childControlWidth = true;
        tabsLayout.childForceExpandWidth = true;
        LayoutElement tabsRowLayout = tabsRow.gameObject.AddComponent<LayoutElement>();
        tabsRowLayout.preferredHeight = 42f;

        CreateTab(tabsRow, Section.Crops, "Культуры");
        CreateTab(tabsRow, Section.Animals, "Животные");
        CreateTab(tabsRow, Section.Levels, "Уровни");
        CreateTab(tabsRow, Section.Prestige, "Престиж");

        RectTransform scrollRectTransform = CreateScrollArea(panelRect, out contentRect);
        LayoutElement scrollLayout = scrollRectTransform.gameObject.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.minHeight = 420f;
    }

    private void CreateTab(Transform parent, Section section, string title)
    {
        Button button = CreateButton(parent, title, () => SetSection(section));
        tabButtons[section] = button;
    }

    private void SetSection(Section section)
    {
        currentSection = section;
        RefreshContent();
    }

    private void RefreshContent()
    {
        ResolveReferences();
        if (contentRect == null)
        {
            return;
        }

        ClearChildren(contentRect);
        UpdateTabVisuals();

        switch (currentSection)
        {
            case Section.Crops:
                RefreshCrops();
                break;
            case Section.Animals:
                RefreshAnimals();
                break;
            case Section.Levels:
                RefreshLevels();
                break;
            case Section.Prestige:
                RefreshPrestige();
                break;
        }
    }

    private void RefreshCrops()
    {
        if (shopManager == null)
        {
            CreateEntry("Культуры", "ShopManager не найден.");
            return;
        }

        IEnumerable<ShopManager.CropShopEntry> crops = shopManager.GetCropCatalog()
            .Where(entry => entry != null)
            .OrderBy(entry => entry.buyPrice);

        foreach (ShopManager.CropShopEntry crop in crops)
        {
            int level = progressionManager != null ? progressionManager.GetCropLevel(crop.cropId) : 1;
            int buyPrice = shopManager.GetCropBuyPrice(crop);
            int profit = progressionManager != null ? progressionManager.GetCropSellPrice(crop.cropId, crop.baseSellPrice) : crop.baseSellPrice;
            float growthSeconds = progressionManager != null ? progressionManager.GetCropGrowthSeconds(crop.cropId, crop.growthSeconds) : crop.growthSeconds;
            string unlockText = shopManager.GetCropUnlockRequirementText(crop.cropId);
            string description = FarmEncyclopediaTextCatalog.GetCropDescription(crop.cropId);

            CreateEntry(
                crop.displayName,
                $"Цена: {buyPrice}\n" +
                $"Время роста: {growthSeconds:0.#} с\n" +
                $"Прибыль: {profit}\n" +
                $"Открытие: {unlockText}\n" +
                $"Текущий уровень: {level}/{GetMaxLevelText()}\n" +
                description);
        }
    }

    private void RefreshAnimals()
    {
        if (shopManager == null)
        {
            CreateEntry("Животные", "ShopManager не найден.");
            return;
        }

        IEnumerable<ShopManager.AnimalShopEntry> animals = shopManager.GetAnimalCatalog()
            .Where(entry => entry != null)
            .OrderBy(entry => entry.buyPrice);
        AnimalManager animalManager = AnimalManager.Instance ?? FindFirstObjectByType<AnimalManager>();
        int maxAnimalSlots = animalManager != null ? animalManager.MaxAnimalSlots : 4;

        foreach (ShopManager.AnimalShopEntry animal in animals)
        {
            int level = progressionManager != null ? progressionManager.GetAnimalLevel(animal.animalId) : 1;
            int buyPrice = shopManager.GetAnimalBuyPrice(animal);
            int profit = progressionManager != null ? progressionManager.GetAnimalProductSellPrice(animal.animalId, animal.baseSellPrice) : animal.baseSellPrice;
            float productionSeconds = progressionManager != null ? progressionManager.GetAnimalProductionSeconds(animal.animalId, animal.productionSeconds) : animal.productionSeconds;
            string productName = FarmEncyclopediaTextCatalog.GetProductDisplayName(animal.productItemId);
            string unlockText = shopManager.GetAnimalUnlockRequirementText(animal.animalId);
            string description = FarmEncyclopediaTextCatalog.GetAnimalDescription(animal.animalId);

            CreateEntry(
                animal.displayName,
                $"Цена: {buyPrice}\n" +
                $"Продукция: {productName}\n" +
                $"Время продукции: {productionSeconds:0.#} с\n" +
                $"Прибыль продукции: {profit}\n" +
                $"Лимит на карте: {maxAnimalSlots} всего\n" +
                $"Открытие: {unlockText}\n" +
                $"Текущий уровень: {level}/{GetMaxLevelText()}\n" +
                description);
        }
    }

    private void RefreshLevels()
    {
        int maxLevel = progressionManager != null ? progressionManager.GetMaxLevel() : 5;
        CreateEntry(
            "Как работают уровни",
            "Культуры повышают уровень через продажу этой культуры.\n" +
            "Животные повышают уровень через сбор продукции этого животного.\n" +
            $"Максимальный уровень: {maxLevel}.\n" +
            "Уровни увеличивают прибыль и уменьшают время роста или производства.");

        for (int level = 1; level <= maxLevel; level++)
        {
            float profitMultiplier = progressionManager != null ? progressionManager.GetLevelProfitMultiplier(level) : 1f;
            float timeMultiplier = progressionManager != null ? progressionManager.GetLevelTimeMultiplier(level) : 1f;
            int requiredActions = progressionManager != null ? progressionManager.GetRequiredLifetimeActionsForLevel(level) : 0;
            string requirement = level <= 1
                ? "доступен сразу"
                : $"{requiredActions} продаж/ед. продукции";

            CreateEntry(
                $"Level {level}",
                $"Условие: {requirement}\n" +
                $"Прибыль: x{profitMultiplier:0.00}\n" +
                $"Время: x{timeMultiplier:0.00}");
        }
    }

    private void RefreshPrestige()
    {
        PrestigeManager prestigeManager = PrestigeManager.Instance ?? FindFirstObjectByType<PrestigeManager>();
        string currentState = prestigeManager != null
            ? $"Текущие ОП: {prestigeManager.PrestigePoints}\nВсего престижей: {prestigeManager.TotalPrestigeCount}\nОП за текущий забег: {prestigeManager.CalculatePrestigePointsForCurrentRun()}\n"
            : string.Empty;

        CreateEntry(
            "Престиж",
            currentState +
            "Окно престижа открывается клавишей P.\n" +
            "Престиж доступен, если за забег заработано 500 монет или открыта Морковь.\n" +
            "При престиже текущий забег сбрасывается, а очки престижа и купленные улучшения сохраняются.");

        CreateEntry(
            "Улучшения престижа",
            "Быстрый рост культур: уменьшает итоговое время роста после уровня культуры.\n" +
            "Выгодный урожай: увеличивает итоговую прибыль урожая после уровня культуры.\n" +
            "Быстрая продукция животных: уменьшает итоговое время продукции после уровня животного.\n" +
            "Ценная продукция животных: увеличивает итоговую прибыль продукции после уровня животного.\n" +
            "Стартовый капитал: добавляет монеты в начале нового забега.\n" +
            "Дешёвые культуры и Дешёвые животные: уменьшают цену покупки.\n" +
            "Опытный фермер и Опытный скотовод: ускоряют выполнение условий открытия.\n" +
            "Умелый сбор: увеличивает только получаемые ОП за забег с округлением вниз.");
    }

    private string GetMaxLevelText()
    {
        return progressionManager != null ? progressionManager.GetMaxLevel().ToString() : "5";
    }

    private void CreateEntry(string title, string body)
    {
        GameObject entry = CreateUiObject("Entry", contentRect);
        Image bg = entry.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.15f, 0.17f, 0.92f);

        VerticalLayoutGroup layout = entry.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = entry.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text titleText = CreateText("Title", entry.transform, 20f, FontStyles.Bold);
        titleText.text = title;

        TMP_Text bodyText = CreateText("Body", entry.transform, 16f, FontStyles.Normal);
        bodyText.text = body;
        bodyText.color = new Color(0.92f, 0.96f, 1f, 0.95f);
    }

    private RectTransform CreateScrollArea(Transform parent, out RectTransform scrollContent)
    {
        GameObject scrollObject = CreateUiObject("Scroll", parent);
        Image viewportImage = scrollObject.AddComponent<Image>();
        viewportImage.color = new Color(0.03f, 0.05f, 0.06f, 0.6f);
        Mask mask = scrollObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        RectTransform viewport = scrollObject.GetComponent<RectTransform>();
        GameObject content = CreateUiObject("Content", viewport);
        scrollContent = content.GetComponent<RectTransform>();
        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(1f, 1f);
        scrollContent.pivot = new Vector2(0.5f, 1f);
        scrollContent.offsetMin = new Vector2(10f, 0f);
        scrollContent.offsetMax = new Vector2(-10f, 0f);

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(0, 0, 10, 10);
        contentLayout.spacing = 10f;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;

        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = scrollContent;
        return viewport;
    }

    private Button CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUiObject($"{text}Button", parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.23f, 0.26f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        TMP_Text label = CreateText("Label", buttonObject.transform, 16f, FontStyles.Bold);
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return obj;
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

    private void UpdateTabVisuals()
    {
        foreach (KeyValuePair<Section, Button> pair in tabButtons)
        {
            Image image = pair.Value != null ? pair.Value.GetComponent<Image>() : null;
            if (image != null)
            {
                image.color = pair.Key == currentSection
                    ? new Color(0.19f, 0.55f, 0.36f, 0.95f)
                    : new Color(0.2f, 0.23f, 0.26f, 0.95f);
            }
        }
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

    private static bool WasJPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.J);
#endif
    }
}
