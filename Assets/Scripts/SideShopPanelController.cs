using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[ExecuteAlways]
public class SideShopPanelController : MonoBehaviour
{
    public static SideShopPanelController Instance { get; private set; }

    public static event Action OnPanelOpened;
    public static event Action OnPanelOpenedByTab;

    private enum SideTab
    {
        Crops = 0,
        Animals = 1
    }

    [Header("Panel Motion")]
    [SerializeField] [Min(120f)] private float panelWidth = 700f;
    [SerializeField] [Min(0.05f)] private float animationDuration = 0.22f;
    [SerializeField] private float closedOffsetX = -730f;
    [SerializeField] private AnimationCurve slideCurve;

    [Header("References")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private ProgressionManager progressionManager;

    private Canvas targetCanvas;
    private RectTransform panelRect;
    private CanvasGroup panelCanvasGroup;
    private RectTransform cropsContent;
    private RectTransform animalsContent;
    private Button cropsTabButton;
    private Button animalsTabButton;
    private TMP_Text statusText;
    private TMP_Text headerText;
    private Coroutine animateRoutine;
    private SideTab currentTab = SideTab.Crops;
    private bool isOpen;

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

        if (FindFirstObjectByType<SideShopPanelController>() != null)
        {
            return;
        }

        FarmUIManager uiManager = FindFirstObjectByType<FarmUIManager>();
        if (uiManager != null)
        {
            uiManager.gameObject.AddComponent<SideShopPanelController>();
            return;
        }

        GameObject fallback = new GameObject("SideShopPanelController");
        fallback.AddComponent<SideShopPanelController>();
    }

    public bool IsOpen => isOpen;

    public void ClosePanel()
    {
        SetPanelOpen(false);
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

        if (slideCurve == null || slideCurve.length == 0)
        {
            slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        ResolveManagers();
        EnsurePanelBuilt();
        RefreshAllLists();
        SetPanelOpenImmediate(!Application.isPlaying);
    }

    private void OnEnable()
    {
        ProgressionManager.OnProgressionChanged += RefreshAllListsIfOpen;
        PrestigeManager.OnPrestigeChanged += RefreshAllListsIfOpen;
        AnimalManager.OnAnimalPurchased += RefreshAllListsIfOpen;
        AnimalManager.OnAnimalSold += RefreshAllListsIfOpen;
    }

    private void OnDisable()
    {
        ProgressionManager.OnProgressionChanged -= RefreshAllListsIfOpen;
        PrestigeManager.OnPrestigeChanged -= RefreshAllListsIfOpen;
        AnimalManager.OnAnimalPurchased -= RefreshAllListsIfOpen;
        AnimalManager.OnAnimalSold -= RefreshAllListsIfOpen;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!UIInputUtility.IsTextInputFocused() && WasTabPressedThisFrame())
        {
            TogglePanelFromTab();
        }

        if (!isOpen)
        {
            return;
        }

        if (WasQPressedThisFrame())
        {
            SetTab(SideTab.Crops);
        }
        else if (WasEPressedThisFrame())
        {
            SetTab(SideTab.Animals);
        }

    }

    private void ResolveManagers()
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

    private void BuildPanelUI()
    {
        if (targetCanvas == null)
        {
            return;
        }

        GameObject root = new GameObject("SideMarketPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelRect = root.GetComponent<RectTransform>();
        panelRect.SetParent(targetCanvas.transform, false);
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.sizeDelta = new Vector2(panelWidth, 0f);
        panelRect.anchoredPosition = new Vector2(closedOffsetX, 0f);

        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.11f, 0.12f, 0.96f);

        panelCanvasGroup = root.GetComponent<CanvasGroup>();

        RectTransform container = CreateUIObject("Container", panelRect).GetComponent<RectTransform>();
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = new Vector2(14f, 14f);
        container.offsetMax = new Vector2(-14f, -14f);

        VerticalLayoutGroup layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = container.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        headerText = CreateText("Header", container, 28f, FontStyles.Bold);
        headerText.text = "\u041a\u0430\u0442\u0430\u043b\u043e\u0433 \u0444\u0435\u0440\u043c\u044b";
        LayoutElement headerLayout = headerText.gameObject.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 52f;

        RectTransform tabsRow = CreateUIObject("TabsRow", container).GetComponent<RectTransform>();
        HorizontalLayoutGroup tabsLayout = tabsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 8f;
        tabsLayout.childControlHeight = false;
        tabsLayout.childControlWidth = true;
        tabsLayout.childForceExpandHeight = false;
        tabsLayout.childForceExpandWidth = true;
        LayoutElement tabsLayoutElement = tabsRow.gameObject.AddComponent<LayoutElement>();
        tabsLayoutElement.preferredHeight = 38f;

        cropsTabButton = CreateTabButton(tabsRow, "\u041a\u0443\u043b\u044c\u0442\u0443\u0440\u044b", () => SetTab(SideTab.Crops));
        animalsTabButton = CreateTabButton(tabsRow, "\u0416\u0438\u0432\u043e\u0442\u043d\u044b\u0435", () => SetTab(SideTab.Animals));

        RectTransform cropScroll = CreateScrollArea("CropsScroll", container, out cropsContent);
        RectTransform animalScroll = CreateScrollArea("AnimalsScroll", container, out animalsContent);
        LayoutElement cropLayoutElement = cropScroll.gameObject.AddComponent<LayoutElement>();
        cropLayoutElement.flexibleHeight = 1f;
        cropLayoutElement.minHeight = 280f;
        LayoutElement animalLayoutElement = animalScroll.gameObject.AddComponent<LayoutElement>();
        animalLayoutElement.flexibleHeight = 1f;
        animalLayoutElement.minHeight = 280f;

        statusText = CreateText("Status", container, 20f, FontStyles.Normal);
        statusText.text = "TAB: \u043e\u0442\u043a\u0440\u044b\u0442\u044c/\u0437\u0430\u043a\u0440\u044b\u0442\u044c";
        statusText.color = new Color(0.9f, 0.95f, 1f, 0.9f);
        LayoutElement statusLayout = statusText.gameObject.AddComponent<LayoutElement>();
        statusLayout.preferredHeight = 44f;

        SetTab(SideTab.Crops);
    }

    private void EnsurePanelBuilt()
    {
        ResolveManagers();
        if (panelRect != null)
        {
            return;
        }

        if (targetCanvas != null)
        {
            Transform existing = targetCanvas.transform.Find("SideMarketPanel");
            if (existing != null)
            {
                panelRect = existing as RectTransform;
            }
        }

        if (panelRect == null)
        {
            BuildPanelUI();
            return;
        }

        panelCanvasGroup = panelRect.GetComponent<CanvasGroup>();
        cropsTabButton = FindComponentInChildrenByName<Button>(panelRect, "КультурыTab");
        animalsTabButton = FindComponentInChildrenByName<Button>(panelRect, "ЖивотныеTab");
        statusText = FindComponentInChildrenByName<TMP_Text>(panelRect, "Status");
        headerText = FindComponentInChildrenByName<TMP_Text>(panelRect, "Header");

        RectTransform cropsScroll = FindRectByName(panelRect, "CropsScroll");
        RectTransform animalsScroll = FindRectByName(panelRect, "AnimalsScroll");
        cropsContent = cropsScroll != null ? FindRectByName(cropsScroll, "Content") : null;
        animalsContent = animalsScroll != null ? FindRectByName(animalsScroll, "Content") : null;

        if (panelCanvasGroup == null || cropsContent == null || animalsContent == null || cropsTabButton == null || animalsTabButton == null)
        {
            if (Application.isPlaying)
            {
                Destroy(panelRect.gameObject);
            }
            else
            {
                DestroyImmediate(panelRect.gameObject);
            }

            panelRect = null;
            BuildPanelUI();
        }
    }

    public void EditorRebuildPreview()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ResolveManagers();
        if (targetCanvas == null)
        {
            return;
        }

        Transform existing = targetCanvas.transform.Find("SideMarketPanel");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        panelRect = null;
        BuildPanelUI();
        RefreshAllLists();
        SetPanelOpenImmediate(true);
    }

    private void TogglePanelFromTab()
    {
        FarmEncyclopediaController encyclopedia = FarmEncyclopediaController.Instance ?? FindFirstObjectByType<FarmEncyclopediaController>();
        if (!isOpen && encyclopedia != null && encyclopedia.IsOpen)
        {
            return;
        }

        PrestigePanelController prestigePanel = PrestigePanelController.Instance ?? FindFirstObjectByType<PrestigePanelController>();
        if (!isOpen && prestigePanel != null && prestigePanel.IsOpen)
        {
            return;
        }

        bool willOpen = !isOpen;
        SetPanelOpen(willOpen);

        if (willOpen)
        {
            RefreshAllLists();
            OnPanelOpened?.Invoke();
            OnPanelOpenedByTab?.Invoke();
        }
    }

    private void SetPanelOpen(bool shouldOpen)
    {
        if (panelRect == null)
        {
            return;
        }

        if (isOpen == shouldOpen && animateRoutine == null)
        {
            return;
        }

        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }

        animateRoutine = StartCoroutine(AnimatePanel(shouldOpen));
    }

    private IEnumerator AnimatePanel(bool targetOpen)
    {
        float from = panelRect.anchoredPosition.x;
        float to = targetOpen ? 0f : closedOffsetX;
        float elapsed = 0f;

        panelCanvasGroup.blocksRaycasts = targetOpen;
        panelCanvasGroup.interactable = targetOpen;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float curved = slideCurve.Evaluate(t);
            float x = Mathf.LerpUnclamped(from, to, curved);
            panelRect.anchoredPosition = new Vector2(x, 0f);
            yield return null;
        }

        panelRect.anchoredPosition = new Vector2(to, 0f);
        isOpen = targetOpen;
        animateRoutine = null;
    }

    private void SetPanelOpenImmediate(bool targetOpen)
    {
        isOpen = targetOpen;
        if (panelRect != null)
        {
            panelRect.anchoredPosition = new Vector2(targetOpen ? 0f : closedOffsetX, 0f);
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.blocksRaycasts = targetOpen;
            panelCanvasGroup.interactable = targetOpen;
        }
    }

    private void SetTab(SideTab tab)
    {
        currentTab = tab;
        bool showCrops = currentTab == SideTab.Crops;

        if (cropsContent != null && cropsContent.parent != null)
        {
            cropsContent.parent.gameObject.SetActive(showCrops);
        }

        if (animalsContent != null && animalsContent.parent != null)
        {
            animalsContent.parent.gameObject.SetActive(!showCrops);
        }

        UpdateTabVisual(cropsTabButton, showCrops);
        UpdateTabVisual(animalsTabButton, !showCrops);
        headerText.text = showCrops
            ? "\u041a\u0430\u0442\u0430\u043b\u043e\u0433 \u0444\u0435\u0440\u043c\u044b: \u041a\u0443\u043b\u044c\u0442\u0443\u0440\u044b"
            : "\u041a\u0430\u0442\u0430\u043b\u043e\u0433 \u0444\u0435\u0440\u043c\u044b: \u0416\u0438\u0432\u043e\u0442\u043d\u044b\u0435";
        RefreshAllLists();
    }

    private void UpdateTabVisual(Button button, bool isActive)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = isActive ? new Color(0.19f, 0.55f, 0.36f, 0.95f) : new Color(0.2f, 0.23f, 0.26f, 0.9f);
        }
    }

    private void RefreshAllLists()
    {
        ResolveManagers();
        RefreshCropList();
        RefreshAnimalList();
    }

    private void RefreshAllListsIfOpen()
    {
        if (isOpen)
        {
            RefreshAllLists();
        }
    }

    private void RefreshAllListsIfOpen(string _)
    {
        RefreshAllListsIfOpen();
    }

    private void RefreshCropList()
    {
        if (cropsContent == null || shopManager == null)
        {
            return;
        }

        ClearChildren(cropsContent);
        IEnumerable<ShopManager.CropShopEntry> crops = shopManager.GetCropCatalog()
            .Where(entry => entry != null)
            .OrderBy(entry => entry.buyPrice);

        foreach (ShopManager.CropShopEntry crop in crops)
        {
            int level = progressionManager != null ? progressionManager.GetCropLevel(crop.cropId) : 1;
            int progress = progressionManager != null ? progressionManager.GetCropProgressToNextLevel(crop.cropId) : 0;
            int required = progressionManager != null ? progressionManager.GetRequiredActionsForNextCropLevel(crop.cropId) : 10;
            int buyPrice = shopManager.GetCropBuyPrice(crop);
            int profit = progressionManager != null
                ? progressionManager.GetCropSellPrice(crop.cropId, crop.baseSellPrice)
                : crop.baseSellPrice;
            float growthSeconds = progressionManager != null
                ? progressionManager.GetCropGrowthSeconds(crop.cropId, crop.growthSeconds)
                : crop.growthSeconds;
            string levelProgress = required > 0 ? $"{progress}/{required} продаж" : "Максимальный уровень";
            bool isUnlocked = shopManager.IsCropUnlocked(crop.cropId, out _);
            string availabilityLine = shopManager.GetCropUnlockStatusText(crop.cropId);
            string prestigeLine = crop.requiredPrestigeLevel > 0
                ? $"Требование престижа: {crop.requiredPrestigeLevel}\n"
                : string.Empty;

            string info = $"{crop.displayName}\n" +
                          $"\u0423\u0440.: {level}/5  \u0426\u0435\u043d\u0430: {buyPrice}\n" +
                          $"\u041f\u0440\u043e\u0433\u0440\u0435\u0441\u0441: {levelProgress}\n" +
                          $"\u041f\u0440\u0438\u0431\u044b\u043b\u044c: {profit}\n" +
                          $"\u0412\u0440\u0435\u043c\u044f \u0440\u043e\u0441\u0442\u0430: {growthSeconds:0.#} \u0441\n" +
                          prestigeLine +
                          availabilityLine;

            CreateListEntry(
                cropsContent,
                info,
                isUnlocked ? "\u041a\u0443\u043f\u0438\u0442\u044c" : "\u0417\u0430\u043a\u0440\u044b\u0442\u043e",
                isUnlocked,
                () =>
                {
                    shopManager.TryBuyCropBySeedId(crop.seedItemId, out string message);
                    statusText.text = message;
                    RefreshAllLists();
                });
        }
    }

    private void RefreshAnimalList()
    {
        if (animalsContent == null || shopManager == null)
        {
            return;
        }

        ClearChildren(animalsContent);
        IEnumerable<ShopManager.AnimalShopEntry> animals = shopManager.GetAnimalCatalog()
            .Where(entry => entry != null)
            .OrderBy(entry => entry.buyPrice);

        foreach (ShopManager.AnimalShopEntry animal in animals)
        {
            AnimalManager animalManager = AnimalManager.Instance ?? FindFirstObjectByType<AnimalManager>();
            int level = progressionManager != null ? progressionManager.GetAnimalLevel(animal.animalId) : 1;
            int progress = progressionManager != null ? progressionManager.GetAnimalProgressToNextLevel(animal.animalId) : 0;
            int required = progressionManager != null ? progressionManager.GetRequiredActionsForNextAnimalLevel(animal.animalId) : 10;
            int buyPrice = shopManager.GetAnimalBuyPrice(animal);
            int ownedCount = animalManager != null ? animalManager.GetOwnedCount(animal.animalId) : 0;
            int currentAnimalCount = animalManager != null ? animalManager.GetCurrentAnimalCount() : 0;
            int maxAnimalSlots = animalManager != null ? animalManager.MaxAnimalSlots : 4;
            int sellPrice = animalManager != null ? animalManager.GetAnimalSellPrice(animal.animalId) : Mathf.FloorToInt(buyPrice * 0.5f);
            int productSellPrice = progressionManager != null
                ? progressionManager.GetAnimalProductSellPrice(animal.animalId, animal.baseSellPrice)
                : animal.baseSellPrice;
            float productionSeconds = progressionManager != null
                ? progressionManager.GetAnimalProductionSeconds(animal.animalId, animal.productionSeconds)
                : animal.productionSeconds;
            string levelProgress = required > 0 ? $"{progress}/{required} продукции" : "Максимальный уровень";
            string cyclesText = animal.maxProductionCycles > 0 ? animal.maxProductionCycles.ToString() : "\u221e";
            bool isUnlocked = shopManager.IsAnimalUnlocked(animal.animalId, out _);
            string availabilityLine = shopManager.GetAnimalUnlockStatusText(animal.animalId);
            string prestigeLine = animal.requiredPrestigeLevel > 0
                ? $"Требование престижа: {animal.requiredPrestigeLevel}\n"
                : string.Empty;

            string info = $"{animal.displayName}\n" +
                          $"\u0423\u0440.: {level}/5  \u0426\u0435\u043d\u0430: {buyPrice}\n" +
                          $"Куплено: {ownedCount}  Мест: {currentAnimalCount}/{maxAnimalSlots}\n" +
                          $"Цена продажи: {sellPrice}\n" +
                          $"\u041f\u0440\u043e\u0433\u0440\u0435\u0441\u0441: {levelProgress}\n" +
                          $"\u041f\u0440\u0438\u0431\u044b\u043b\u044c \u0437\u0430 \u043f\u0440\u043e\u0434\u0443\u043a\u0446\u0438\u044e: {productSellPrice}\n" +
                          $"\u0426\u0438\u043a\u043b\u043e\u0432: {cyclesText}\n" +
                          $"1 \u0435\u0434.: {productionSeconds:0.#} \u0441\n" +
                          prestigeLine +
                          availabilityLine;

            CreateListEntry(
                animalsContent,
                info,
                isUnlocked ? "\u041a\u0443\u043f\u0438\u0442\u044c" : "\u0417\u0430\u043a\u0440\u044b\u0442\u043e",
                isUnlocked,
                () =>
                {
                    shopManager.TryBuyAnimalById(animal.animalId, out string message);
                    statusText.text = message;
                    RefreshAllLists();
                },
                "Продать",
                ownedCount > 0,
                () =>
                {
                    shopManager.TrySellAnimalById(animal.animalId, out string message);
                    statusText.text = message;
                    RefreshAllLists();
                });
        }
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return obj;
    }

    private static TMP_Text CreateText(string name, Transform parent, float size, FontStyles style)
    {
        GameObject obj = CreateUIObject(name, parent);
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private Button CreateTabButton(Transform parent, string title, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObj = CreateUIObject($"{title}Tab", parent);
        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.2f, 0.23f, 0.26f, 0.9f);
        LayoutElement buttonLayout = buttonObj.AddComponent<LayoutElement>();
        buttonLayout.preferredHeight = 38f;
        buttonLayout.minHeight = 38f;
        Button button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        TMP_Text label = CreateText("Label", buttonObj.transform, 15f, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        label.text = title;
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private RectTransform CreateScrollArea(string name, Transform parent, out RectTransform contentRect)
    {
        GameObject scrollObject = CreateUIObject(name, parent);
        Image viewportImage = scrollObject.AddComponent<Image>();
        viewportImage.color = new Color(0.03f, 0.05f, 0.06f, 0.5f);
        Mask mask = scrollObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        RectTransform viewportRect = scrollObject.GetComponent<RectTransform>();

        GameObject contentObject = CreateUIObject("Content", viewportRect);
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(8f, 0f);
        contentRect.offsetMax = new Vector2(-8f, 0f);

        VerticalLayoutGroup listLayout = contentObject.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f;
        listLayout.childControlHeight = true;
        listLayout.childControlWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childForceExpandWidth = true;
        listLayout.padding = new RectOffset(0, 0, 6, 6);

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        return viewportRect;
    }

    private void CreateListEntry(
        Transform parent,
        string infoText,
        string buttonText,
        bool isInteractable,
        UnityEngine.Events.UnityAction onClick,
        string secondaryButtonText = null,
        bool secondaryInteractable = false,
        UnityEngine.Events.UnityAction secondaryOnClick = null)
    {
        GameObject row = CreateUIObject("Entry", parent);
        Image rowBg = row.AddComponent<Image>();
        rowBg.color = new Color(0.12f, 0.15f, 0.17f, 0.9f);
        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.padding = new RectOffset(12, 12, 10, 10);
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        LayoutElement rowElement = row.AddComponent<LayoutElement>();
        rowElement.preferredHeight = string.IsNullOrWhiteSpace(secondaryButtonText) ? 182f : 218f;

        TMP_Text info = CreateText("Info", row.transform, 15f, FontStyles.Normal);
        info.text = infoText;
        info.alignment = TextAlignmentOptions.TopLeft;
        info.overflowMode = TextOverflowModes.Overflow;
        LayoutElement infoLayout = info.gameObject.AddComponent<LayoutElement>();
        infoLayout.flexibleWidth = 1f;

        GameObject buttonColumn = CreateUIObject("ButtonColumn", row.transform);
        VerticalLayoutGroup buttonColumnLayout = buttonColumn.AddComponent<VerticalLayoutGroup>();
        buttonColumnLayout.spacing = 8f;
        buttonColumnLayout.childControlHeight = true;
        buttonColumnLayout.childControlWidth = true;
        buttonColumnLayout.childForceExpandHeight = false;
        buttonColumnLayout.childForceExpandWidth = true;
        LayoutElement buttonColumnElement = buttonColumn.AddComponent<LayoutElement>();
        buttonColumnElement.preferredWidth = 118f;
        buttonColumnElement.minWidth = 118f;

        CreateEntryButton(buttonColumn.transform, "BuyButton", buttonText, isInteractable, onClick, new Color(0.23f, 0.44f, 0.22f, 1f));

        if (!string.IsNullOrWhiteSpace(secondaryButtonText))
        {
            CreateEntryButton(buttonColumn.transform, "SellButton", secondaryButtonText, secondaryInteractable, secondaryOnClick, new Color(0.48f, 0.24f, 0.18f, 1f));
        }
    }

    private Button CreateEntryButton(
        Transform parent,
        string objectName,
        string buttonText,
        bool isInteractable,
        UnityEngine.Events.UnityAction onClick,
        Color activeColor)
    {
        GameObject buyButtonObject = CreateUIObject(objectName, parent);
        Image buyButtonImage = buyButtonObject.AddComponent<Image>();
        buyButtonImage.color = isInteractable
            ? activeColor
            : new Color(0.33f, 0.33f, 0.33f, 1f);
        Button buyButton = buyButtonObject.AddComponent<Button>();
        buyButton.interactable = isInteractable;
        if (onClick != null)
        {
            buyButton.onClick.AddListener(onClick);
        }

        LayoutElement buyLayout = buyButtonObject.AddComponent<LayoutElement>();
        buyLayout.preferredHeight = 54f;
        buyLayout.minHeight = 54f;

        TMP_Text buyLabel = CreateText("Label", buyButtonObject.transform, 20f, FontStyles.Bold);
        buyLabel.alignment = TextAlignmentOptions.Center;
        buyLabel.text = buttonText;
        RectTransform buyLabelRect = buyLabel.GetComponent<RectTransform>();
        buyLabelRect.anchorMin = Vector2.zero;
        buyLabelRect.anchorMax = Vector2.one;
        buyLabelRect.offsetMin = Vector2.zero;
        buyLabelRect.offsetMax = Vector2.zero;

        return buyButton;
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private static T FindComponentInChildrenByName<T>(Transform root, string objectName) where T : Component
    {
        RectTransform found = FindRectByName(root, objectName);
        return found != null ? found.GetComponent<T>() : null;
    }

    private static RectTransform FindRectByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root as RectTransform;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform result = FindRectByName(root.GetChild(i), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static bool WasTabPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Tab);
#endif
    }

    private static bool WasQPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Q);
#endif
    }

    private static bool WasEPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
