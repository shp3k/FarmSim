using System;
using UnityEngine;
using UnityEngine.Serialization;

public class AnimalProductProducer : MonoBehaviour
{
    public static event Action<string> OnAnimalProductCollected;

    private const float MaxProductionSeconds = 24f * 60f * 60f;

    [Header("Product")]
    [SerializeField] private string animalId = "chicken";
    [SerializeField] private string productItemId = "egg";
    [SerializeField] [Min(1)] private int baseSellPrice = 25;
    [SerializeField] [FormerlySerializedAs("productionMinutes")] [Min(0.1f)] private float productionSeconds = 15f;
    [SerializeField] private int amountPerCycle = 1;
    [SerializeField] [Min(0)] private int maxProductionCycles;
    [SerializeField] [Min(0)] private int producedCycles;

    [Header("Ready Marker")]
    [SerializeField] private ReadyMarkerController readyMarkerController;

    private GameManager gameManager;
    private float elapsedProductionSeconds;
    private double lastProductionRealtimeSeconds;
    private bool hasProductionRealtimeSample;
    private bool isConfigured;
    private bool isProductReady;

    public string AnimalId => animalId;
    public bool IsProductReady => isProductReady;

    public void Configure(string configuredAnimalId, string itemId, int configuredBaseSellPrice, float secondsPerCycle, int amount, int maxCycles)
    {
        if (!string.IsNullOrWhiteSpace(configuredAnimalId))
        {
            animalId = configuredAnimalId;
        }

        if (!string.IsNullOrWhiteSpace(itemId))
        {
            productItemId = itemId;
        }

        baseSellPrice = Mathf.Max(1, configuredBaseSellPrice);
        productionSeconds = Mathf.Clamp(secondsPerCycle, 0.1f, MaxProductionSeconds);
        amountPerCycle = Mathf.Max(1, amount);
        maxProductionCycles = Mathf.Max(0, maxCycles);
        isConfigured = true;

        ResolveReadyMarkerController();
        EnsureClickSupport();

        SampleProductionRealtime();
        AdvanceProductionFromRealtime(false);
    }

    private void Awake()
    {
        ResolveGameManager();
        ResolveReadyMarkerController();
    }

    private void OnEnable()
    {
        if (isConfigured)
        {
            ApplyReadyMarker();
        }
    }

    private void Update()
    {
        if (isProductReady)
        {
            ApplyReadyMarker();
            return;
        }

        AdvanceProductionFromRealtime();
    }

    public bool CollectProduct(out int earned, out string message)
    {
        earned = 0;
        AdvanceProductionFromRealtime();

        if (!isProductReady)
        {
            message = $"{name}: продукция ещё не готова.";
            Debug.Log(message);
            return false;
        }

        ResolveGameManager();
        if (gameManager == null)
        {
            message = $"{name}: GameManager not found.";
            Debug.LogError(message);
            return false;
        }

        int unitPrice = ProgressionManager.Instance != null
            ? ProgressionManager.Instance.GetAnimalProductSellPrice(animalId, baseSellPrice)
            : baseSellPrice;

        earned = Mathf.Max(1, unitPrice) * amountPerCycle;
        gameManager.AddMoney(earned);
        producedCycles++;
        ProgressionManager.Instance?.RegisterAnimalCollection(animalId, amountPerCycle);
        OnAnimalProductCollected?.Invoke(animalId);

        elapsedProductionSeconds = 0f;
        SetProductReady(false);
        SampleProductionRealtime();
        SaveManager.Instance?.SaveGame();

        message = $"{name}: собрано {amountPerCycle} x {productItemId} за {earned} монет.";
        Debug.Log(message);
        return true;
    }

    public AnimalProductSaveData GetSaveData(int spawnIndex)
    {
        AdvanceProductionFromRealtime(false);

        return new AnimalProductSaveData
        {
            animalId = animalId,
            spawnIndex = Mathf.Max(0, spawnIndex),
            productItemId = productItemId,
            productionSeconds = productionSeconds,
            elapsedProductionSeconds = elapsedProductionSeconds,
            lastProductionUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            producedCycles = producedCycles,
            isProductReady = isProductReady
        };
    }

    public void LoadFromSave(AnimalProductSaveData data)
    {
        if (data == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(data.animalId))
        {
            animalId = data.animalId;
        }

        if (!string.IsNullOrWhiteSpace(data.productItemId))
        {
            productItemId = data.productItemId;
        }

        if (data.productionSeconds > 0f)
        {
            productionSeconds = Mathf.Clamp(data.productionSeconds, 0.1f, MaxProductionSeconds);
        }

        producedCycles = Mathf.Max(0, data.producedCycles);
        isProductReady = data.isProductReady;

        float totalSeconds = GetEffectiveProductionSeconds();
        elapsedProductionSeconds = Mathf.Clamp(data.elapsedProductionSeconds, 0f, totalSeconds);

        if (!isProductReady && data.lastProductionUnixSeconds > 0)
        {
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long offlineDelta = nowUnix - data.lastProductionUnixSeconds;
            if (offlineDelta > 0)
            {
                elapsedProductionSeconds = Mathf.Min(totalSeconds, elapsedProductionSeconds + offlineDelta);
            }
        }

        if (elapsedProductionSeconds >= totalSeconds && !HasReachedCycleLimit())
        {
            isProductReady = true;
            elapsedProductionSeconds = totalSeconds;
        }
        else if (HasReachedCycleLimit())
        {
            isProductReady = false;
        }

        ApplyReadyMarker();
        SampleProductionRealtime();
    }

    private void AdvanceProductionFromRealtime(bool triggerSaveOnReady = true)
    {
        if (!isConfigured || isProductReady || HasReachedCycleLimit())
        {
            return;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        if (!hasProductionRealtimeSample)
        {
            lastProductionRealtimeSeconds = now;
            hasProductionRealtimeSample = true;
            return;
        }

        float deltaSeconds = (float)Math.Max(0d, now - lastProductionRealtimeSeconds);
        lastProductionRealtimeSeconds = now;
        AdvanceProduction(deltaSeconds, triggerSaveOnReady);
    }

    private void AdvanceProduction(float deltaSeconds, bool triggerSaveOnReady)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        elapsedProductionSeconds = Mathf.Min(GetEffectiveProductionSeconds(), elapsedProductionSeconds + deltaSeconds);

        if (elapsedProductionSeconds >= GetEffectiveProductionSeconds())
        {
            SetProductReady(true);
            if (triggerSaveOnReady)
            {
                SaveManager.Instance?.SaveGame();
            }
        }
    }

    private void SampleProductionRealtime()
    {
        lastProductionRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
        hasProductionRealtimeSample = true;
    }

    private void SetProductReady(bool ready)
    {
        bool wasReady = isProductReady;
        isProductReady = ready && !HasReachedCycleLimit();

        if (isProductReady)
        {
            elapsedProductionSeconds = GetEffectiveProductionSeconds();
        }

        ApplyReadyMarker();

        if (isProductReady && !wasReady)
        {
            Debug.Log($"{name}: продукция готова к сбору.");
        }
    }

    private void ApplyReadyMarker()
    {
        if (readyMarkerController == null)
        {
            ResolveReadyMarkerController();
        }

        readyMarkerController?.SetReady(isProductReady, ReadyMarkerKind.Animal);
    }

    private void ResolveGameManager()
    {
        if (gameManager != null)
        {
            return;
        }

        gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }
    }

    private bool HasReachedCycleLimit()
    {
        return maxProductionCycles > 0 && producedCycles >= maxProductionCycles;
    }

    private float GetEffectiveProductionSeconds()
    {
        return ProgressionManager.Instance != null
            ? Mathf.Clamp(ProgressionManager.Instance.GetAnimalProductionSeconds(animalId, productionSeconds), 0.1f, MaxProductionSeconds)
            : Mathf.Clamp(productionSeconds, 0.1f, MaxProductionSeconds);
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

    private void EnsureClickSupport()
    {
        AnimalProductClickHandler clickHandler = GetComponent<AnimalProductClickHandler>();
        if (clickHandler == null)
        {
            clickHandler = gameObject.AddComponent<AnimalProductClickHandler>();
        }

        BoxCollider2D clickCollider = GetComponent<BoxCollider2D>();
        if (clickCollider == null)
        {
            clickCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        clickCollider.isTrigger = true;
        clickHandler.Configure(this);
    }
}
