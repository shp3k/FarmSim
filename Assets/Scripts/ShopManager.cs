using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class ShopManager : MonoBehaviour
{
    [Serializable]
    public class CropShopEntry
    {
        public string cropId = "wheat";
        public string displayName = "Пшеница";
        [Header("Unlock")]
        [FormerlySerializedAs("isUnlocked")] [HideInInspector] public bool legacyIsUnlocked = true;
        [FormerlySerializedAs("unlockAtDay")] [HideInInspector] public int legacyUnlockAtDay = 1;
        public string unlockByCropId;
        [Min(0)] public int requiredPreviousCropSales;
        [Min(0)] public int requiredPrestigeLevel;
        public string seedItemId = "seed_wheat";
        public string harvestItemId = "wheat";
        public int buyPrice = 30;
        public int baseSellPrice = 50;
        [FormerlySerializedAs("growthMinutes")] public float growthSeconds = 180f;
        public GameObject plantPrefab;
    }

    [Serializable]
    public class AnimalShopEntry
    {
        public string animalId = "chicken";
        public string displayName = "Курица";
        [Header("Unlock")]
        public string unlockByAnimalId;
        [Min(0)] public int requiredPreviousAnimalCollections;
        [Min(0)] public int requiredPrestigeLevel;
        public string productItemId = "egg";
        public int buyPrice = 300;
        public int baseSellPrice = 25;
        [FormerlySerializedAs("productionMinutes")] public float productionSeconds = 15f;
        [Min(1)] public int maxOwned = 3;
        [Min(1)] public int amountPerCycle = 1;
        [FormerlySerializedAs("maxCyclesPerAnimal")] public int maxProductionCycles;
        public GameObject prefab;
        public Transform[] spawnPoints;
        public Transform fallbackParent;
    }

    private const string WheatSeedId = "seed_wheat";
    private const string CarrotSeedId = "seed_carrot";
    private const string ChickenAnimalId = "chicken";
    private const string CowAnimalId = "cow";

    [Header("Catalog (Single Source Of Truth)")]
    [SerializeField] private List<CropShopEntry> cropCatalog = new();
    [SerializeField] private List<AnimalShopEntry> animalCatalog = new();
    [SerializeField] [HideInInspector] private bool catalogTimeUnitsMigrated;
    [SerializeField] private bool warnMissingPrefabsInEditor;

    [Header("Managers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private FarmManager farmManager;
    [SerializeField] private AnimalManager animalManager;

    public bool HasPlantPrefabsConfigured => cropCatalog.Any(entry => entry != null && entry.plantPrefab != null);

    private void Awake()
    {
        ResolveManagers();
        EnsureCatalogs();
        ValidateCatalogConsistency();
        EnsureTemplatePlantsAreHidden();
        ValidateCatalogPrefabs();
    }

    private void OnValidate()
    {
        EnsureCatalogs();
        ValidateCatalogConsistency();
    }

    public IReadOnlyList<CropShopEntry> GetCropCatalog()
    {
        EnsureCatalogs();
        return cropCatalog;
    }

    public IReadOnlyList<AnimalShopEntry> GetAnimalCatalog()
    {
        EnsureCatalogs();
        return animalCatalog;
    }

    public bool TryGetCropById(string cropId, out CropShopEntry crop)
    {
        EnsureCatalogs();
        crop = cropCatalog.FirstOrDefault(entry => entry != null && string.Equals(entry.cropId, cropId, StringComparison.OrdinalIgnoreCase));
        return crop != null;
    }

    public bool TryGetCropBySeedId(string seedItemId, out CropShopEntry crop)
    {
        EnsureCatalogs();
        crop = cropCatalog.FirstOrDefault(entry => entry != null && string.Equals(entry.seedItemId, seedItemId, StringComparison.OrdinalIgnoreCase));
        return crop != null;
    }

    public bool IsCropUnlocked(string cropId, out string reason)
    {
        if (!TryGetCropById(cropId, out CropShopEntry crop))
        {
            reason = $"Неизвестная культура: {cropId}.";
            return false;
        }

        return IsCropUnlocked(crop, out reason);
    }

    public bool IsAnimalUnlocked(string animalId, out string reason)
    {
        if (!TryGetAnimalById(animalId, out AnimalShopEntry animal))
        {
            reason = $"Неизвестное животное: {animalId}.";
            return false;
        }

        return IsAnimalUnlocked(animal, out reason);
    }

    public string GetCropUnlockStatusText(string cropId)
    {
        return IsCropUnlocked(cropId, out string reason)
            ? "Статус: Открыта"
            : $"Статус: Закрыта ({reason})";
    }

    public string GetAnimalUnlockStatusText(string animalId)
    {
        return IsAnimalUnlocked(animalId, out string reason)
            ? "Статус: Открыт"
            : $"Статус: Закрыт ({reason})";
    }

    public string GetCropUnlockRequirementText(string cropId)
    {
        if (!TryGetCropById(cropId, out CropShopEntry crop))
        {
            return $"Неизвестная культура: {cropId}.";
        }

        if (string.Equals(crop.cropId, "wheat", StringComparison.OrdinalIgnoreCase))
        {
            return "Открыта сразу.";
        }

        List<string> requirements = new();
        if (!string.IsNullOrWhiteSpace(crop.unlockByCropId) && crop.requiredPreviousCropSales > 0)
        {
            requirements.Add($"продать {Mathf.Max(1, crop.requiredPreviousCropSales)} шт. \"{GetCropDisplayName(crop.unlockByCropId)}\"");
        }

        if (crop.requiredPrestigeLevel > 0)
        {
            requirements.Add($"престиж {crop.requiredPrestigeLevel}");
        }

        return requirements.Count > 0
            ? $"Требуется: {string.Join(", ", requirements)}."
            : "Условия открытия не настроены.";
    }

    public string GetAnimalUnlockRequirementText(string animalId)
    {
        if (!TryGetAnimalById(animalId, out AnimalShopEntry animal))
        {
            return $"Неизвестное животное: {animalId}.";
        }

        if (string.Equals(animal.animalId, "chicken", StringComparison.OrdinalIgnoreCase))
        {
            return "Открыта сразу.";
        }

        List<string> requirements = new();
        if (!string.IsNullOrWhiteSpace(animal.unlockByAnimalId) && animal.requiredPreviousAnimalCollections > 0)
        {
            requirements.Add($"собрать {Mathf.Max(1, animal.requiredPreviousAnimalCollections)} ед. продукции \"{GetAnimalDisplayName(animal.unlockByAnimalId)}\"");
        }

        if (animal.requiredPrestigeLevel > 0)
        {
            requirements.Add($"престиж {animal.requiredPrestigeLevel}");
        }

        return requirements.Count > 0
            ? $"Требуется: {string.Join(", ", requirements)}."
            : "Условия открытия не настроены.";
    }

    public int GetCropBuyPrice(CropShopEntry crop)
    {
        if (crop == null)
        {
            return 0;
        }

        return PrestigeManager.Instance != null
            ? PrestigeManager.Instance.GetCropBuyPrice(crop.buyPrice)
            : Mathf.Max(0, crop.buyPrice);
    }

    public int GetAnimalBuyPrice(AnimalShopEntry animal)
    {
        if (animal == null)
        {
            return 0;
        }

        return PrestigeManager.Instance != null
            ? PrestigeManager.Instance.GetAnimalBuyPrice(animal.buyPrice)
            : Mathf.Max(0, animal.buyPrice);
    }

    public bool TryGetAnimalById(string animalId, out AnimalShopEntry animal)
    {
        EnsureCatalogs();
        animal = animalCatalog.FirstOrDefault(entry => entry != null && string.Equals(entry.animalId, animalId, StringComparison.OrdinalIgnoreCase));
        return animal != null;
    }

    public bool BuyWheatSeedAndPlant(out string message)
    {
        return TryBuyCropBySeedId(WheatSeedId, out message);
    }

    public bool BuyCarrotSeedAndPlant(out string message)
    {
        return TryBuyCropBySeedId(CarrotSeedId, out message);
    }

    public bool BuyChicken(out string message)
    {
        return TryBuyAnimalById(ChickenAnimalId, out message);
    }

    public bool BuyCow(out string message)
    {
        return TryBuyAnimalById(CowAnimalId, out message);
    }

    public bool TryBuyCropBySeedId(string seedItemId, out string message)
    {
        ResolveManagers();

        if (!TryGetCropBySeedId(seedItemId, out CropShopEntry crop))
        {
            message = $"Неизвестные семена: {seedItemId}.";
            return false;
        }

        if (!IsCropUnlocked(crop, out string unlockReason))
        {
            message = unlockReason;
            return false;
        }

        return TryBuyAndPlant(crop, out message);
    }

    public bool TryBuyAnimalById(string animalId, out string message)
    {
        ResolveManagers();

        if (animalManager == null)
        {
            message = "AnimalManager not found.";
            return false;
        }

        if (!TryGetAnimalById(animalId, out _))
        {
            message = $"Неизвестное животное: {animalId}.";
            return false;
        }

        if (!IsAnimalUnlocked(animalId, out string unlockReason))
        {
            message = unlockReason;
            return false;
        }

        return animalManager.TryBuyAnimal(animalId, out message);
    }

    public bool TrySellAnimalById(string animalId, out string message)
    {
        ResolveManagers();

        if (animalManager == null)
        {
            message = "AnimalManager not found.";
            return false;
        }

        if (!TryGetAnimalById(animalId, out _))
        {
            message = $"Неизвестное животное: {animalId}.";
            return false;
        }

        return animalManager.TrySellAnimal(animalId, out message);
    }

    private bool TryBuyAndPlant(CropShopEntry crop, out string message)
    {
        ResolveManagers();

        if (gameManager == null || farmManager == null)
        {
            message = "Farm/economy managers are missing.";
            return false;
        }

        if (crop == null)
        {
            message = "Crop entry is null.";
            return false;
        }

        GameObject plantTemplate = GetPlantTemplate(crop);
        if (plantTemplate == null)
        {
            message = $"Plant prefab is missing for '{crop.seedItemId}'.";
            return false;
        }

        if (!farmManager.HasFreePlots())
        {
            message = "Нет свободных грядок";
            return false;
        }

        int buyPrice = GetCropBuyPrice(crop);
        if (gameManager.money < buyPrice)
        {
            message = $"Не хватает монет. Нужно {buyPrice}, есть {gameManager.money}.";
            return false;
        }

        bool planted = farmManager.TryPlantInPriorityPlot(
            crop.seedItemId,
            plantTemplate,
            crop.cropId,
            crop.harvestItemId,
            crop.growthSeconds,
            out string plantMessage);

        if (!planted)
        {
            message = plantMessage;
            return false;
        }

        gameManager.SpendMoney(buyPrice);
        message = $"Куплено за {buyPrice}. {plantMessage}";
        return true;
    }

    private void ResolveManagers()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
        }

        if (farmManager == null)
        {
            farmManager = FarmManager.Instance;
            if (farmManager == null)
            {
                farmManager = FindFirstObjectByType<FarmManager>();
            }
        }

        if (animalManager == null)
        {
            animalManager = AnimalManager.Instance;
            if (animalManager == null)
            {
                animalManager = FindFirstObjectByType<AnimalManager>();
            }
        }
    }

    private void EnsureCatalogs()
    {
        if (cropCatalog == null)
        {
            cropCatalog = new List<CropShopEntry>();
        }

        if (animalCatalog == null)
        {
            animalCatalog = new List<AnimalShopEntry>();
        }

        if (cropCatalog.Count == 0)
        {
            cropCatalog.Add(new CropShopEntry
            {
                cropId = "wheat",
                displayName = "Пшеница",
                unlockByCropId = string.Empty,
                requiredPreviousCropSales = 0,
                requiredPrestigeLevel = 0,
                seedItemId = WheatSeedId,
                harvestItemId = "wheat",
                buyPrice = 30,
                baseSellPrice = 50,
                growthSeconds = 180f,
                plantPrefab = null
            });

            cropCatalog.Add(new CropShopEntry
            {
                cropId = "carrot",
                displayName = "Морковь",
                unlockByCropId = "wheat",
                requiredPreviousCropSales = 12,
                requiredPrestigeLevel = 0,
                seedItemId = CarrotSeedId,
                harvestItemId = "carrot",
                buyPrice = 75,
                baseSellPrice = 100,
                growthSeconds = 300f,
                plantPrefab = null
            });
        }

        if (animalCatalog.Count == 0)
        {
            animalCatalog.Add(new AnimalShopEntry
            {
                animalId = ChickenAnimalId,
                displayName = "Курица",
                unlockByAnimalId = string.Empty,
                requiredPreviousAnimalCollections = 0,
                requiredPrestigeLevel = 0,
                productItemId = "egg",
                buyPrice = 300,
                baseSellPrice = 25,
                productionSeconds = 15f,
                maxOwned = 3,
                amountPerCycle = 1,
                maxProductionCycles = 0,
                prefab = null,
                spawnPoints = Array.Empty<Transform>(),
                fallbackParent = null
            });

            animalCatalog.Add(new AnimalShopEntry
            {
                animalId = CowAnimalId,
                displayName = "Корова",
                unlockByAnimalId = "sheep",
                requiredPreviousAnimalCollections = 16,
                requiredPrestigeLevel = 1,
                productItemId = "milk",
                buyPrice = 500,
                baseSellPrice = 50,
                productionSeconds = 25f,
                maxOwned = 2,
                amountPerCycle = 1,
                maxProductionCycles = 0,
                prefab = null,
                spawnPoints = Array.Empty<Transform>(),
                fallbackParent = null
            });
        }

        if (!catalogTimeUnitsMigrated)
        {
            for (int i = 0; i < cropCatalog.Count; i++)
            {
                CropShopEntry crop = cropCatalog[i];
                if (crop == null)
                {
                    continue;
                }

                if (crop.growthSeconds > 0f && crop.growthSeconds <= 15f)
                {
                    crop.growthSeconds *= 60f;
                }
            }

            catalogTimeUnitsMigrated = true;
        }

        for (int i = 0; i < cropCatalog.Count; i++)
        {
            CropShopEntry crop = cropCatalog[i];
            if (crop == null)
            {
                continue;
            }

            crop.growthSeconds = Mathf.Max(0.1f, crop.growthSeconds);
            ApplyDefaultCropUnlockRule(crop);
            ApplyCropConfigToTemplate(crop);
        }

        for (int i = 0; i < animalCatalog.Count; i++)
        {
            AnimalShopEntry animal = animalCatalog[i];
            if (animal == null)
            {
                continue;
            }

            animal.productionSeconds = Mathf.Max(0.1f, animal.productionSeconds);
            if (animal.maxOwned <= 0)
            {
                animal.maxOwned = string.Equals(animal.animalId, ChickenAnimalId, StringComparison.OrdinalIgnoreCase)
                    ? 3
                    : string.Equals(animal.animalId, CowAnimalId, StringComparison.OrdinalIgnoreCase)
                        ? 2
                        : 1;
            }

            animal.maxOwned = Mathf.Max(1, animal.maxOwned);
            animal.amountPerCycle = Mathf.Max(1, animal.amountPerCycle);
            animal.maxProductionCycles = Mathf.Max(0, animal.maxProductionCycles);
            ApplyDefaultAnimalUnlockRule(animal);
            AutoPopulateAnimalSceneRefsIfMissing(animal);
        }
    }

    private void ValidateCatalogConsistency()
    {
        ValidateCropCatalog();
        ValidateAnimalCatalog();
    }

    private void ValidateCropCatalog()
    {
        HashSet<string> cropIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seedIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> harvestIds = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < cropCatalog.Count; i++)
        {
            CropShopEntry crop = cropCatalog[i];
            if (crop == null)
            {
                Debug.LogWarning($"ShopManager: cropCatalog[{i}] is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(crop.cropId))
            {
                Debug.LogError($"ShopManager: cropCatalog[{i}] has empty cropId.");
            }
            else if (!cropIds.Add(crop.cropId))
            {
                Debug.LogError($"ShopManager: duplicate cropId '{crop.cropId}' in Crop Catalog.");
            }

            if (string.IsNullOrWhiteSpace(crop.seedItemId))
            {
                Debug.LogError($"ShopManager: crop '{crop.cropId}' has empty seedItemId.");
            }
            else if (!seedIds.Add(crop.seedItemId))
            {
                Debug.LogError($"ShopManager: duplicate seedItemId '{crop.seedItemId}' in Crop Catalog.");
            }

            if (string.IsNullOrWhiteSpace(crop.harvestItemId))
            {
                Debug.LogError($"ShopManager: crop '{crop.cropId}' has empty harvestItemId.");
            }
            else if (!harvestIds.Add(crop.harvestItemId))
            {
                Debug.LogWarning($"ShopManager: duplicate harvestItemId '{crop.harvestItemId}' in Crop Catalog.");
            }

            if (crop.buyPrice < 0)
            {
                Debug.LogWarning($"ShopManager: crop '{crop.cropId}' has negative buyPrice ({crop.buyPrice}).");
            }

            if (crop.baseSellPrice < 0)
            {
                Debug.LogWarning($"ShopManager: crop '{crop.cropId}' has negative baseSellPrice ({crop.baseSellPrice}).");
            }

            if (string.IsNullOrWhiteSpace(crop.unlockByCropId) &&
                !string.Equals(crop.cropId, "wheat", StringComparison.OrdinalIgnoreCase) &&
                crop.requiredPreviousCropSales <= 0 &&
                crop.requiredPrestigeLevel <= 0)
            {
                Debug.LogWarning($"ShopManager: crop '{crop.cropId}' has no unlock requirements configured and will remain locked.");
            }
        }
    }

    private void ValidateAnimalCatalog()
    {
        HashSet<string> animalIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> productIds = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < animalCatalog.Count; i++)
        {
            AnimalShopEntry animal = animalCatalog[i];
            if (animal == null)
            {
                Debug.LogWarning($"ShopManager: animalCatalog[{i}] is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(animal.animalId))
            {
                Debug.LogError($"ShopManager: animalCatalog[{i}] has empty animalId.");
            }
            else if (!animalIds.Add(animal.animalId))
            {
                Debug.LogError($"ShopManager: duplicate animalId '{animal.animalId}' in Animal Catalog.");
            }

            if (string.IsNullOrWhiteSpace(animal.productItemId))
            {
                Debug.LogError($"ShopManager: animal '{animal.animalId}' has empty productItemId.");
            }
            else if (!productIds.Add(animal.productItemId))
            {
                Debug.LogWarning($"ShopManager: duplicate productItemId '{animal.productItemId}' in Animal Catalog.");
            }

            if (animal.buyPrice < 0)
            {
                Debug.LogWarning($"ShopManager: animal '{animal.animalId}' has negative buyPrice ({animal.buyPrice}).");
            }

            if (animal.baseSellPrice < 0)
            {
                Debug.LogWarning($"ShopManager: animal '{animal.animalId}' has negative baseSellPrice ({animal.baseSellPrice}).");
            }

            if (animal.prefab == null)
            {
                if (Application.isPlaying || warnMissingPrefabsInEditor)
                {
                    Debug.LogWarning($"ShopManager: animal '{animal.animalId}' has no prefab assigned in Animal Catalog.");
                }
            }

            if (string.IsNullOrWhiteSpace(animal.unlockByAnimalId) &&
                !string.Equals(animal.animalId, "chicken", StringComparison.OrdinalIgnoreCase) &&
                animal.requiredPreviousAnimalCollections <= 0 &&
                animal.requiredPrestigeLevel <= 0)
            {
                Debug.LogWarning($"ShopManager: animal '{animal.animalId}' has no unlock requirements configured and will remain locked.");
            }
        }
    }

    private static void ApplyCropConfigToTemplate(CropShopEntry crop)
    {
        if (crop == null || crop.plantPrefab == null)
        {
            return;
        }

        CropGrowth growth = crop.plantPrefab.GetComponent<CropGrowth>();
        if (growth == null)
        {
            return;
        }

        growth.ConfigureFromCatalog(crop.cropId, crop.harvestItemId, crop.growthSeconds);
    }

    private static void ApplyDefaultCropUnlockRule(CropShopEntry crop)
    {
        if (crop == null || string.IsNullOrWhiteSpace(crop.cropId))
        {
            return;
        }

        // Do not override explicit inspector configuration.
        if (!string.IsNullOrWhiteSpace(crop.unlockByCropId) || crop.requiredPreviousCropSales > 0 || crop.requiredPrestigeLevel > 0)
        {
            return;
        }

        switch (crop.cropId.Trim().ToLowerInvariant())
        {
            case "wheat":
                break;
            case "carrot":
                crop.unlockByCropId = "wheat";
                crop.requiredPreviousCropSales = 12;
                break;
            case "potato":
                crop.unlockByCropId = "carrot";
                crop.requiredPreviousCropSales = 15;
                break;
            case "corn":
                crop.unlockByCropId = "potato";
                crop.requiredPreviousCropSales = 18;
                break;
            case "beet":
                crop.unlockByCropId = "corn";
                crop.requiredPreviousCropSales = 20;
                break;
            case "tomato":
                crop.unlockByCropId = "beet";
                crop.requiredPreviousCropSales = 22;
                break;
            case "cabbage":
                crop.unlockByCropId = "tomato";
                crop.requiredPreviousCropSales = 14;
                break;
            case "cucumber":
                crop.unlockByCropId = "cabbage";
                crop.requiredPreviousCropSales = 16;
                crop.requiredPrestigeLevel = 1;
                break;
            case "pumpkin":
                crop.unlockByCropId = "cucumber";
                crop.requiredPreviousCropSales = 18;
                crop.requiredPrestigeLevel = 1;
                break;
            case "strawberry":
                crop.unlockByCropId = "pumpkin";
                crop.requiredPreviousCropSales = 20;
                crop.requiredPrestigeLevel = 1;
                break;
        }
    }

    private static void ApplyDefaultAnimalUnlockRule(AnimalShopEntry animal)
    {
        if (animal == null || string.IsNullOrWhiteSpace(animal.animalId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(animal.unlockByAnimalId) || animal.requiredPreviousAnimalCollections > 0 || animal.requiredPrestigeLevel > 0)
        {
            return;
        }

        switch (animal.animalId.Trim().ToLowerInvariant())
        {
            case "chicken":
                break;
            case "goat":
                animal.unlockByAnimalId = "chicken";
                animal.requiredPreviousAnimalCollections = 18;
                break;
            case "sheep":
                animal.unlockByAnimalId = "goat";
                animal.requiredPreviousAnimalCollections = 20;
                break;
            case "cow":
                animal.unlockByAnimalId = "sheep";
                animal.requiredPreviousAnimalCollections = 16;
                animal.requiredPrestigeLevel = 1;
                break;
            case "duck":
                animal.unlockByAnimalId = "cow";
                animal.requiredPreviousAnimalCollections = 18;
                animal.requiredPrestigeLevel = 1;
                break;
        }
    }

    private bool IsCropUnlocked(CropShopEntry crop, out string reason)
    {
        if (crop == null)
        {
            reason = "Данные культуры отсутствуют.";
            return false;
        }

        if (crop.requiredPrestigeLevel > 0 &&
            (PrestigeManager.Instance == null || !PrestigeManager.Instance.MeetsPrestigeRequirement(crop.requiredPrestigeLevel)))
        {
            reason = $"Будет доступно позже (требуется престиж: {crop.requiredPrestigeLevel}).";
            return false;
        }

        if (string.Equals(crop.cropId, "wheat", StringComparison.OrdinalIgnoreCase))
        {
            reason = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(crop.unlockByCropId) || crop.requiredPreviousCropSales <= 0)
        {
            reason = "Условия открытия не настроены.";
            return false;
        }

        ProgressionManager progression = ProgressionManager.Instance;
        int rawSales = progression != null
            ? progression.GetCropLifetimeActions(crop.unlockByCropId)
            : 0;
        int currentSales = PrestigeManager.Instance != null
            ? PrestigeManager.Instance.GetEffectiveCropUnlockProgress(rawSales)
            : rawSales;
        int requiredSales = Mathf.Max(1, crop.requiredPreviousCropSales);

        if (currentSales < requiredSales)
        {
            string requiredCropName = GetCropDisplayName(crop.unlockByCropId);
            reason = $"Продайте {requiredSales} шт. \"{requiredCropName}\" ({currentSales}/{requiredSales}).";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private bool IsAnimalUnlocked(AnimalShopEntry animal, out string reason)
    {
        if (animal == null)
        {
            reason = "Данные животного отсутствуют.";
            return false;
        }

        if (animal.requiredPrestigeLevel > 0 &&
            (PrestigeManager.Instance == null || !PrestigeManager.Instance.MeetsPrestigeRequirement(animal.requiredPrestigeLevel)))
        {
            reason = $"Будет доступно позже (требуется престиж: {animal.requiredPrestigeLevel}).";
            return false;
        }

        if (string.Equals(animal.animalId, "chicken", StringComparison.OrdinalIgnoreCase))
        {
            reason = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(animal.unlockByAnimalId) || animal.requiredPreviousAnimalCollections <= 0)
        {
            reason = "Условия открытия не настроены.";
            return false;
        }

        ProgressionManager progression = ProgressionManager.Instance;
        int rawCollections = progression != null
            ? progression.GetAnimalLifetimeActions(animal.unlockByAnimalId)
            : 0;
        int currentCollections = PrestigeManager.Instance != null
            ? PrestigeManager.Instance.GetEffectiveAnimalUnlockProgress(rawCollections)
            : rawCollections;
        int requiredCollections = Mathf.Max(1, animal.requiredPreviousAnimalCollections);

        if (currentCollections < requiredCollections)
        {
            string requiredAnimalName = GetAnimalDisplayName(animal.unlockByAnimalId);
            reason = $"Соберите {requiredCollections} ед. продукции \"{requiredAnimalName}\" ({currentCollections}/{requiredCollections}).";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void ValidateCatalogPrefabs()
    {
        for (int i = 0; i < cropCatalog.Count; i++)
        {
            CropShopEntry crop = cropCatalog[i];
            if (crop == null)
            {
                continue;
            }

            ValidatePlantPrefab(crop.seedItemId, crop.plantPrefab);
        }
    }

    private static void ValidatePlantPrefab(string seedItemId, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"ShopManager: prefab is NULL for '{seedItemId}'.");
            return;
        }

        CropGrowth cropGrowth = prefab.GetComponent<CropGrowth>();
        if (cropGrowth == null)
        {
            Debug.LogWarning($"ShopManager: prefab '{prefab.name}' has no CropGrowth component.");
            return;
        }

        Debug.Log($"ShopManager: '{seedItemId}' uses '{prefab.name}' (cropId={cropGrowth.CropId}).");
    }

    public bool TryGetPlantPrefabForSeed(string seedItemId, out GameObject prefab)
    {
        if (TryGetCropBySeedId(seedItemId, out CropShopEntry crop))
        {
            prefab = GetPlantTemplate(crop);
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public bool TryGetPlantPrefabForCropId(string cropId, out GameObject prefab)
    {
        if (TryGetCropById(cropId, out CropShopEntry crop))
        {
            prefab = GetPlantTemplate(crop);
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    private static GameObject GetPlantTemplate(CropShopEntry crop)
    {
        if (crop == null)
        {
            return null;
        }

        return crop.plantPrefab != null
            ? crop.plantPrefab
            : CatalogRuntimeFallbackFactory.GetOrCreatePlantTemplate(crop);
    }

    private void EnsureTemplatePlantsAreHidden()
    {
        for (int i = 0; i < cropCatalog.Count; i++)
        {
            CropShopEntry crop = cropCatalog[i];
            if (crop == null)
            {
                continue;
            }

            HideTemplatePlantIfSceneObject(crop.plantPrefab);
        }
    }

    private static void HideTemplatePlantIfSceneObject(GameObject plantTemplate)
    {
        if (plantTemplate == null)
        {
            return;
        }

        if (!plantTemplate.scene.IsValid())
        {
            return;
        }

        if (plantTemplate.name.Contains("(Clone)"))
        {
            return;
        }

        if (plantTemplate.activeSelf)
        {
            plantTemplate.SetActive(false);
        }
    }

    private static void AutoPopulateAnimalSceneRefsIfMissing(AnimalShopEntry animal)
    {
        if (animal == null)
        {
            return;
        }

        if (animal.prefab == null)
        {
            string[] candidateNames =
            {
                string.Equals(animal.animalId, ChickenAnimalId, StringComparison.OrdinalIgnoreCase) ? "Chicken" : null,
                string.Equals(animal.animalId, CowAnimalId, StringComparison.OrdinalIgnoreCase) ? "Cow" : null,
                ToPascalCase(animal.animalId),
                animal.displayName
            };

            for (int i = 0; i < candidateNames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(candidateNames[i]))
                {
                    continue;
                }

                GameObject sceneObject = GameObject.Find(candidateNames[i]);
                if (sceneObject != null)
                {
                    animal.prefab = sceneObject;
                    break;
                }
            }
        }

        if (animal.fallbackParent == null)
        {
            GameObject root = GameObject.Find("AnimalSpawnRoot");
            if (root != null)
            {
                animal.fallbackParent = root.transform;
            }
        }

        if (animal.spawnPoints == null || animal.spawnPoints.Length == 0)
        {
            string[] prefixes =
            {
                string.Equals(animal.animalId, ChickenAnimalId, StringComparison.OrdinalIgnoreCase) ? "ChickenSpawn_" : null,
                string.Equals(animal.animalId, CowAnimalId, StringComparison.OrdinalIgnoreCase) ? "CowSpawn_" : null,
                $"{ToPascalCase(animal.animalId)}Spawn_",
                $"{animal.animalId}Spawn_"
            };

            List<Transform> points = new();
            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform transform = all[i];
                if (transform == null || transform.gameObject == null)
                {
                    continue;
                }

                for (int prefixIndex = 0; prefixIndex < prefixes.Length; prefixIndex++)
                {
                    string prefix = prefixes[prefixIndex];
                    if (!string.IsNullOrWhiteSpace(prefix) &&
                        transform.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        points.Add(transform);
                        break;
                    }
                }
            }

            if (points.Count > 0)
            {
                animal.spawnPoints = points.ToArray();
            }
        }
    }

    public string GetCropDisplayName(string cropId)
    {
        if (TryGetCropById(cropId, out CropShopEntry crop) && !string.IsNullOrWhiteSpace(crop.displayName))
        {
            return crop.displayName;
        }

        return string.IsNullOrWhiteSpace(cropId) ? "культура" : cropId;
    }

    public string GetAnimalDisplayName(string animalId)
    {
        if (TryGetAnimalById(animalId, out AnimalShopEntry animal) && !string.IsNullOrWhiteSpace(animal.displayName))
        {
            return animal.displayName;
        }

        return string.IsNullOrWhiteSpace(animalId) ? "животное" : animalId;
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string[] parts = value.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            if (part.Length == 0)
            {
                continue;
            }

            parts[i] = char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part.Substring(1).ToLowerInvariant() : string.Empty);
        }

        return string.Concat(parts);
    }
}
