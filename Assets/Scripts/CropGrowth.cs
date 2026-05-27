using System;
using UnityEngine;
using UnityEngine.Serialization;

public class CropGrowth : MonoBehaviour
{
    public static event Action<CropGrowth> OnAnyCropReadyToHarvest;

    [Header("Crop")]
    [SerializeField] private string cropId = "wheat";
    [SerializeField] private string harvestItemId = "wheat";

    [Header("Growth Time")]
    [SerializeField] [FormerlySerializedAs("totalGrowthMinutes")] private float configuredBaseGrowthSeconds = 180f;
    [SerializeField] [HideInInspector] private bool migratedGrowthSecondsFromMinutes;
    // Runtime multiplier only. It is reserved for future modifiers (e.g. prestige bonuses),
    // and must not duplicate the base growth configuration from the Crop Catalog.
    [SerializeField] [HideInInspector] [FormerlySerializedAs("growthDurationMultiplier")] [Min(0.1f)] private float growthSecondsMultiplier = 1f;

    [Header("Visuals")]
    [SerializeField] private CropVisualController visualController;
    [SerializeField] private ReadyMarkerController readyMarkerController;
    [SerializeField] [HideInInspector] [FormerlySerializedAs("stageModels")] private GameObject[] legacyStageModels = new GameObject[3];

    private const int TotalStages = 3;
    private const int FullyGrownStageIndex = 2;
    private const float MaxGrowthSeconds = 24f * 60f * 60f;

    private int currentStage;
    private float elapsedGrowthSeconds;
    private long lastGrowthClockSampleUnixSeconds;
    private bool hasGrowthClockSample;
    private bool isReadyToHarvest;

    public int CurrentStage => currentStage;
    public bool IsFullyGrown => currentStage >= FullyGrownStageIndex;
    public bool IsReadyToHarvest => isReadyToHarvest;
    public string HarvestItemId => harvestItemId;
    public string CropId => cropId;
    public float TotalGrowthSeconds => configuredBaseGrowthSeconds;

    private void Awake()
    {
        TryMigrateLegacyGrowthTimeField();
        ResolveVisualController();
        ResolveReadyMarkerController();
        ResolveCatalogConfigurationIfNeeded();
    }

    private void OnEnable()
    {
        ResolveVisualController();
        ResolveReadyMarkerController();
        ResolveCatalogConfigurationIfNeeded();

        if (elapsedGrowthSeconds <= 0f && currentStage <= 0)
        {
            StartGrowth();
            return;
        }

        ApplyStageFromElapsedGrowth(triggerSaveOnStageChange: false);
        SampleGrowthClock();
    }

    private void OnDisable()
    {
        AdvanceGrowthByRealTime();
    }

    public void ConfigureFromCatalog(string configuredCropId, string configuredHarvestItemId, float baseGrowthSeconds)
    {
        if (!string.IsNullOrWhiteSpace(configuredCropId))
        {
            cropId = configuredCropId;
        }

        if (!string.IsNullOrWhiteSpace(configuredHarvestItemId))
        {
            harvestItemId = configuredHarvestItemId;
        }

        if (baseGrowthSeconds > 0f)
        {
            configuredBaseGrowthSeconds = Mathf.Clamp(baseGrowthSeconds, 0.1f, MaxGrowthSeconds);
        }

        RefreshRuntimeGrowthMultiplierFromProgression();
    }

    public void SetRuntimeGrowthMultiplier(float multiplier)
    {
        growthSecondsMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ResetRuntimeGrowthMultiplier()
    {
        growthSecondsMultiplier = 1f;
    }

    public void StartGrowth()
    {
        ResolveCatalogConfigurationIfNeeded();
        currentStage = 0;
        elapsedGrowthSeconds = 0f;
        SetReadyToHarvest(false);
        ApplyStageVisual();
        Debug.Log($"Crop '{cropId}' stage: {currentStage + 1}/{TotalStages}");
        SampleGrowthClock();
    }

    private void Update()
    {
        AdvanceGrowthByRealTime();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            AdvanceGrowthByRealTime();
            return;
        }

        SampleGrowthClock();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            AdvanceGrowthByRealTime();
        }
    }

    public CropSaveData GetSaveData()
    {
        AdvanceGrowthByRealTime();

        return new CropSaveData
        {
            cropId = cropId,
            harvestItemId = harvestItemId,
            totalGrowthSeconds = configuredBaseGrowthSeconds,
            totalGrowthMinutes = configuredBaseGrowthSeconds / 60f,
            currentStage = currentStage,
            elapsedGrowthSeconds = elapsedGrowthSeconds,
            lastGrowthUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    public void LoadFromSave(CropSaveData data)
    {
        if (data == null)
        {
            return;
        }

        ResolveVisualController();

        if (!string.IsNullOrWhiteSpace(data.cropId))
        {
            cropId = data.cropId;
        }

        if (!string.IsNullOrWhiteSpace(data.harvestItemId))
        {
            harvestItemId = data.harvestItemId;
        }

        ResolveCatalogConfigurationIfNeeded();
        float savedGrowthSeconds = data.totalGrowthSeconds > 0f
            ? data.totalGrowthSeconds
            : data.totalGrowthMinutes > 0f ? data.totalGrowthMinutes * 60f : 0f;

        if (configuredBaseGrowthSeconds <= 0.1f && savedGrowthSeconds > 0f)
        {
            configuredBaseGrowthSeconds = Mathf.Clamp(savedGrowthSeconds, 0.1f, MaxGrowthSeconds);
        }

        float totalSeconds = GetEffectiveTotalGrowthSeconds();
        float stageDelay = totalSeconds / (TotalStages - 1);

        if (data.elapsedGrowthSeconds > 0f)
        {
            elapsedGrowthSeconds = Mathf.Clamp(data.elapsedGrowthSeconds, 0f, totalSeconds);
        }
        else
        {
            int loadedStage = Mathf.Clamp(data.currentStage, 0, FullyGrownStageIndex);
            elapsedGrowthSeconds = Mathf.Clamp(loadedStage * stageDelay, 0f, totalSeconds);
        }

        if (data.lastGrowthUnixSeconds > 0)
        {
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long offlineDelta = nowUnix - data.lastGrowthUnixSeconds;
            if (offlineDelta > 0)
            {
                elapsedGrowthSeconds = Mathf.Min(totalSeconds, elapsedGrowthSeconds + offlineDelta);
            }
        }

        ApplyStageFromElapsedGrowth(triggerSaveOnStageChange: false);
        SampleGrowthClock();
    }

    private void AdvanceGrowthByRealTime()
    {
        if (IsFullyGrown)
        {
            return;
        }

        long nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!hasGrowthClockSample)
        {
            lastGrowthClockSampleUnixSeconds = nowUnixSeconds;
            hasGrowthClockSample = true;
            return;
        }

        long deltaSeconds = nowUnixSeconds - lastGrowthClockSampleUnixSeconds;
        if (deltaSeconds <= 0)
        {
            return;
        }

        lastGrowthClockSampleUnixSeconds = nowUnixSeconds;
        elapsedGrowthSeconds = Mathf.Min(GetEffectiveTotalGrowthSeconds(), elapsedGrowthSeconds + deltaSeconds);
        ApplyStageFromElapsedGrowth(triggerSaveOnStageChange: true);
    }

    private void ApplyStageFromElapsedGrowth(bool triggerSaveOnStageChange)
    {
        float totalSeconds = GetEffectiveTotalGrowthSeconds();
        int resolvedStage;

        if (totalSeconds <= 0f)
        {
            resolvedStage = FullyGrownStageIndex;
        }
        else
        {
            float stageStep = totalSeconds / (TotalStages - 1);
            resolvedStage = Mathf.Clamp(Mathf.FloorToInt(elapsedGrowthSeconds / stageStep), 0, FullyGrownStageIndex);
        }

        if (resolvedStage == currentStage)
        {
            SetReadyToHarvest(resolvedStage >= FullyGrownStageIndex);
            return;
        }

        currentStage = resolvedStage;
        ApplyStageVisual();
        SetReadyToHarvest(currentStage >= FullyGrownStageIndex);
        Debug.Log($"Crop '{cropId}' stage: {currentStage + 1}/{TotalStages}");

        if (triggerSaveOnStageChange)
        {
            SaveManager.Instance?.SaveGame();
        }
    }

    private void ApplyStageVisual()
    {
        if (visualController == null)
        {
            return;
        }

        visualController.SetStage(currentStage);
    }

    private void SetReadyToHarvest(bool ready)
    {
        bool wasReady = isReadyToHarvest;
        isReadyToHarvest = ready;

        if (visualController != null)
        {
            visualController.SetReadyToHarvest(ready);
        }

        if (readyMarkerController != null)
        {
            readyMarkerController.SetReady(ready, ReadyMarkerKind.Crop);
        }

        if (ready && !wasReady)
        {
            OnAnyCropReadyToHarvest?.Invoke(this);
        }
    }

    private void SampleGrowthClock()
    {
        lastGrowthClockSampleUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        hasGrowthClockSample = true;
    }

    private float GetEffectiveTotalGrowthSeconds()
    {
        RefreshRuntimeGrowthMultiplierFromProgression();
        float baseSeconds = Mathf.Clamp(configuredBaseGrowthSeconds, 0.1f, MaxGrowthSeconds);
        if (ProgressionManager.Instance != null)
        {
            return Mathf.Clamp(ProgressionManager.Instance.GetCropGrowthSeconds(cropId, baseSeconds), 0.1f, MaxGrowthSeconds);
        }

        float multiplier = Mathf.Max(0.1f, growthSecondsMultiplier);
        return Mathf.Clamp(baseSeconds * multiplier, 0.1f, MaxGrowthSeconds);
    }

    private void ResolveCatalogConfigurationIfNeeded()
    {
        ShopManager shopManager = FindFirstObjectByType<ShopManager>();
        if (shopManager == null)
        {
            return;
        }

        if (shopManager.TryGetCropById(cropId, out ShopManager.CropShopEntry cropById))
        {
            ConfigureFromCatalog(cropById.cropId, cropById.harvestItemId, cropById.growthSeconds);
            return;
        }

        if (shopManager.TryGetCropBySeedId(cropId, out ShopManager.CropShopEntry cropBySeedId))
        {
            ConfigureFromCatalog(cropBySeedId.cropId, cropBySeedId.harvestItemId, cropBySeedId.growthSeconds);
        }
    }

    private void TryMigrateLegacyGrowthTimeField()
    {
        if (migratedGrowthSecondsFromMinutes)
        {
            return;
        }

        if (configuredBaseGrowthSeconds > 0f && configuredBaseGrowthSeconds <= 15f)
        {
            configuredBaseGrowthSeconds *= 60f;
        }

        growthSecondsMultiplier = Mathf.Max(0.1f, growthSecondsMultiplier);
        migratedGrowthSecondsFromMinutes = true;
    }

    private void RefreshRuntimeGrowthMultiplierFromProgression()
    {
        if (ProgressionManager.Instance == null)
        {
            growthSecondsMultiplier = 1f;
            return;
        }

        growthSecondsMultiplier = ProgressionManager.Instance.GetCropGrowthTimeMultiplier(cropId);
    }

    private void ResolveVisualController()
    {
        if (visualController == null)
        {
            visualController = GetComponent<CropVisualController>();
        }

        if (visualController == null)
        {
            visualController = gameObject.AddComponent<CropVisualController>();
        }

        visualController.TryInitializeFromLegacy(legacyStageModels);
    }

    private void ResolveReadyMarkerController()
    {
        if (readyMarkerController == null)
        {
            readyMarkerController = GetComponent<ReadyMarkerController>();
        }

        if (readyMarkerController == null)
        {
            readyMarkerController = gameObject.AddComponent<ReadyMarkerController>();
        }
    }
}
