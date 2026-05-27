using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialHintsController : MonoBehaviour
{
    private const string ManualAnimalHintText = "Животные производят продукцию со временем. Собирайте её, когда она готова. Прибыль зависит от уровня животного.";
    private const string ShopHintKey = "shop";
    private const string HarvestHintKey = "harvest";
    private const string AnimalHintKey = "animal";
    private const string EncyclopediaHintKey = "encyclopedia";
    private const string PrestigeHintKey = "prestige";

    [Header("Hint Text")]
    [SerializeField] private string shopHintText = "Магазин открыт. Здесь можно покупать культуры и животных.";
    [SerializeField] private string encyclopediaHintText = "Нажмите J, чтобы открыть справочник с культурами, животными, уровнями и прогрессом.";
    [SerializeField] private string harvestHintText = "Урожай готов. Нажмите на растение, чтобы собрать прибыль.";
    [SerializeField] private string animalHintText = ManualAnimalHintText;
    [SerializeField] private string prestigeHintText = "Нажмите P, чтобы открыть окно престижа и купить постоянные улучшения.";

    [Header("Display")]
    [SerializeField] [Min(0.5f)] private float hintDurationSeconds = 5f;
    [SerializeField] [Min(0f)] private float firstHintDelayAfterTabSeconds = 0.75f;
    [SerializeField] [Min(0f)] private float regularHintDelaySeconds = 0.25f;

    private Canvas canvas;
    private GameHintOverlay overlay;
    private bool prestigeHintCheckedThisSession;

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

        if (FindFirstObjectByType<TutorialHintsController>() != null)
        {
            return;
        }

        GameObject host = new GameObject("TutorialHintsController");
        host.AddComponent<TutorialHintsController>();
    }

    private void Awake()
    {
        ResolveOverlay();
    }

    private void OnEnable()
    {
        SideShopPanelController.OnPanelOpened += HandleShopOpened;
        CropGrowth.OnAnyCropReadyToHarvest += HandleCropReadyToHarvest;
        AnimalManager.OnAnimalPurchased += HandleAnimalHintTrigger;
        AnimalProductProducer.OnAnimalProductCollected += HandleAnimalHintTrigger;
    }

    private void OnDisable()
    {
        SideShopPanelController.OnPanelOpened -= HandleShopOpened;
        CropGrowth.OnAnyCropReadyToHarvest -= HandleCropReadyToHarvest;
        AnimalManager.OnAnimalPurchased -= HandleAnimalHintTrigger;
        AnimalProductProducer.OnAnimalProductCollected -= HandleAnimalHintTrigger;
    }

    private void Update()
    {
        TryShowPrestigeHintWhenAvailable();
    }

    [ContextMenu("Clear Tutorial Hints")]
    public void ClearTutorialHintsFromInspector()
    {
        TutorialHintsSave.ClearTutorialHints();
        overlay?.StopAllHints();
        prestigeHintCheckedThisSession = false;

        TabHintController tabHint = FindFirstObjectByType<TabHintController>();
        if (tabHint != null)
        {
            tabHint.ResetHintForTesting();
        }
    }

    public void ShowPrestigeHintIfNeeded()
    {
        if (TutorialHintsSave.PrestigeHintSeen)
        {
            return;
        }

        TutorialHintsSave.MarkPrestigeHintSeen();
        EnqueueHint(PrestigeHintKey, prestigeHintText, regularHintDelaySeconds);
    }

    private void HandleShopOpened()
    {
        bool enqueuedAnyHint = false;

        if (!TutorialHintsSave.ShopHintSeen)
        {
            TutorialHintsSave.MarkShopHintSeen();
            EnqueueHint(ShopHintKey, shopHintText, firstHintDelayAfterTabSeconds);
            enqueuedAnyHint = true;
        }

        if (!TutorialHintsSave.EncyclopediaHintSeen)
        {
            TutorialHintsSave.MarkEncyclopediaHintSeen();
            EnqueueHint(
                EncyclopediaHintKey,
                encyclopediaHintText,
                enqueuedAnyHint ? regularHintDelaySeconds : firstHintDelayAfterTabSeconds);
        }
    }

    private void HandleCropReadyToHarvest(CropGrowth crop)
    {
        if (TutorialHintsSave.HarvestHintSeen)
        {
            return;
        }

        TutorialHintsSave.MarkHarvestHintSeen();
        EnqueueHint(HarvestHintKey, harvestHintText, regularHintDelaySeconds);
    }

    private void HandleAnimalHintTrigger(string animalId)
    {
        if (TutorialHintsSave.AnimalHintSeen)
        {
            return;
        }

        TutorialHintsSave.MarkAnimalHintSeen();
        EnqueueHint(AnimalHintKey, GetAnimalHintText(), regularHintDelaySeconds);
    }

    private string GetAnimalHintText()
    {
        if (string.IsNullOrWhiteSpace(animalHintText)
            || animalHintText.Contains("автомат")
            || animalHintText.Contains("Р°РІС‚РѕРјР°С‚"))
        {
            return ManualAnimalHintText;
        }

        return animalHintText;
    }

    private void TryShowPrestigeHintWhenAvailable()
    {
        if (prestigeHintCheckedThisSession || TutorialHintsSave.PrestigeHintSeen)
        {
            return;
        }

        PrestigeManager prestigeManager = PrestigeManager.Instance;
        if (prestigeManager == null || !prestigeManager.CanPrestige(out _))
        {
            return;
        }

        prestigeHintCheckedThisSession = true;
        ShowPrestigeHintIfNeeded();
    }

    private void EnqueueHint(string key, string text, float delaySeconds)
    {
        ResolveOverlay();
        overlay?.EnqueueHint(key, text, hintDurationSeconds, delaySeconds);
    }

    private void ResolveOverlay()
    {
        if (overlay != null)
        {
            return;
        }

        if (canvas == null)
        {
            canvas = UIInputUtility.FindSceneCanvas();
        }

        overlay = GameHintOverlay.EnsureInCanvas(canvas);
    }
}
