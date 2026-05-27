using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public class PrestigeUpgradeSaveData
{
    public string upgradeId;
    public int level;
}

[Serializable]
public class PrestigeSaveData
{
    public int prestigePoints;
    public int totalPrestigeCount;
    public List<PrestigeUpgradeSaveData> upgrades = new();
}

public class PrestigeManager : MonoBehaviour
{
    public static PrestigeManager Instance { get; private set; }
    public static event Action OnPrestigeChanged;

    public const string FastCropGrowthId = "fast_crop_growth";
    public const string ValuableHarvestId = "valuable_harvest";
    public const string FastAnimalProductionId = "fast_animal_production";
    public const string ValuableAnimalProductId = "valuable_animal_product";
    public const string StartingCapitalId = "starting_capital";
    public const string CheapCropsId = "cheap_crops";
    public const string CheapAnimalsId = "cheap_animals";
    public const string ExperiencedFarmerId = "experienced_farmer";
    public const string ExperiencedRancherId = "experienced_rancher";
    public const string SkillfulHarvestId = "skillful_harvest";

    private const string SaveFileName = "prestige.json";
    private const int BaseStartingMoney = 100;
    private readonly Dictionary<string, int> upgradeLevels = new(StringComparer.OrdinalIgnoreCase);

    public int PrestigePoints { get; private set; }
    public int TotalPrestigeCount { get; private set; }
    public string SavePath => Path.Combine(Application.persistentDataPath, SaveManager.GetScopedFileName(SaveFileName));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (FindFirstObjectByType<PrestigeManager>() != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("PrestigeManager");
        managerObject.AddComponent<PrestigeManager>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeUpgradeKeys();
            LoadPrestige();
            return;
        }

        if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public IReadOnlyList<PrestigeUpgradeDefinition> GetUpgradeDefinitions()
    {
        return PrestigeUpgradeDefinition.All;
    }

    public int GetUpgradeLevel(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            return 0;
        }

        return upgradeLevels.TryGetValue(upgradeId, out int level) ? Mathf.Max(0, level) : 0;
    }

    public bool CanPrestige(out string reason)
    {
        if (GameManager.Instance != null && GameManager.Instance.totalRunEarnings >= 500)
        {
            reason = string.Empty;
            return true;
        }

        ShopManager shopManager = FindFirstObjectByType<ShopManager>();
        if (shopManager != null && shopManager.IsCropUnlocked("carrot", out _))
        {
            reason = string.Empty;
            return true;
        }

        reason = "Нужно заработать 500 монет за забег или открыть Морковь.";
        return false;
    }

    public int CalculatePrestigePointsForCurrentRun()
    {
        if (!CanPrestige(out _))
        {
            return 0;
        }

        int basePoints = 1 + CalculateUnlockPrestigePoints() + CalculateIncomePrestigePoints();
        int skillfulHarvestLevel = GetUpgradeLevel(SkillfulHarvestId);
        return Mathf.FloorToInt(basePoints * (1f + 0.05f * skillfulHarvestLevel));
    }

    public bool TryPerformPrestige(out string message)
    {
        if (!CanPrestige(out string reason))
        {
            message = reason;
            return false;
        }

        int earnedPoints = CalculatePrestigePointsForCurrentRun();
        if (earnedPoints <= 0)
        {
            message = "Престиж пока не даст очков.";
            return false;
        }

        PrestigePoints += earnedPoints;
        TotalPrestigeCount++;
        SavePrestige();

        ResetCurrentRun();
        OnPrestigeChanged?.Invoke();
        message = $"Престиж выполнен. Получено ОП: {earnedPoints}.";
        return true;
    }

    public bool TryBuyUpgrade(string upgradeId, out string message)
    {
        PrestigeUpgradeDefinition definition = GetUpgradeDefinitions().FirstOrDefault(item =>
            string.Equals(item.Id, upgradeId, StringComparison.OrdinalIgnoreCase));
        if (definition == null)
        {
            message = "Улучшение не найдено.";
            return false;
        }

        int currentLevel = GetUpgradeLevel(definition.Id);
        if (currentLevel >= definition.MaxLevel)
        {
            message = "Достигнут максимальный уровень.";
            return false;
        }

        if (PrestigePoints < definition.Cost)
        {
            message = $"Недостаточно ОП. Нужно: {definition.Cost}.";
            return false;
        }

        PrestigePoints -= definition.Cost;
        upgradeLevels[definition.Id] = currentLevel + 1;
        SavePrestige();
        OnPrestigeChanged?.Invoke();
        SaveManager.Instance?.SaveGame();
        message = $"Куплено: {definition.Title} ур. {currentLevel + 1}.";
        return true;
    }

    public float GetCropGrowthPrestigeMultiplier()
    {
        return Mathf.Max(0.1f, 1f - 0.05f * GetUpgradeLevel(FastCropGrowthId));
    }

    public float GetCropProfitPrestigeMultiplier()
    {
        return 1f + 0.05f * GetUpgradeLevel(ValuableHarvestId);
    }

    public float GetAnimalProductionPrestigeMultiplier()
    {
        return Mathf.Max(0.1f, 1f - 0.05f * GetUpgradeLevel(FastAnimalProductionId));
    }

    public float GetAnimalProfitPrestigeMultiplier()
    {
        return 1f + 0.05f * GetUpgradeLevel(ValuableAnimalProductId);
    }

    public int GetStartingMoney()
    {
        return BaseStartingMoney + 50 * GetUpgradeLevel(StartingCapitalId);
    }

    public int GetCropBuyPrice(int basePrice)
    {
        return Mathf.Max(0, Mathf.FloorToInt(basePrice * Mathf.Max(0.1f, 1f - 0.04f * GetUpgradeLevel(CheapCropsId))));
    }

    public int GetAnimalBuyPrice(int basePrice)
    {
        return Mathf.Max(0, Mathf.FloorToInt(basePrice * Mathf.Max(0.1f, 1f - 0.04f * GetUpgradeLevel(CheapAnimalsId))));
    }

    public int GetEffectiveCropUnlockProgress(int rawSales)
    {
        return Mathf.FloorToInt(Mathf.Max(0, rawSales) * (1f + 0.1f * GetUpgradeLevel(ExperiencedFarmerId)));
    }

    public int GetEffectiveAnimalUnlockProgress(int rawCollections)
    {
        return Mathf.FloorToInt(Mathf.Max(0, rawCollections) * (1f + 0.1f * GetUpgradeLevel(ExperiencedRancherId)));
    }

    public bool MeetsPrestigeRequirement(int requiredPrestigeLevel)
    {
        return TotalPrestigeCount >= Mathf.Max(0, requiredPrestigeLevel);
    }

    private int CalculateUnlockPrestigePoints()
    {
        ShopManager shopManager = FindFirstObjectByType<ShopManager>();
        if (shopManager == null)
        {
            return 0;
        }

        int points = 0;
        foreach (ShopManager.CropShopEntry crop in shopManager.GetCropCatalog())
        {
            if (crop == null || !shopManager.IsCropUnlocked(crop.cropId, out _))
            {
                continue;
            }

            points += GetCropUnlockPrestigePoints(crop.cropId);
        }

        foreach (ShopManager.AnimalShopEntry animal in shopManager.GetAnimalCatalog())
        {
            if (animal == null || !shopManager.IsAnimalUnlocked(animal.animalId, out _))
            {
                continue;
            }

            points += GetAnimalUnlockPrestigePoints(animal.animalId);
        }

        return points;
    }

    private static int CalculateIncomePrestigePoints()
    {
        int earnings = GameManager.Instance != null ? GameManager.Instance.totalRunEarnings : 0;
        int points = 0;

        if (earnings >= 500) points += 1;
        if (earnings >= 1500) points += 1;
        if (earnings >= 3000) points += 1;
        if (earnings >= 6000) points += 2;
        if (earnings >= 10000) points += 2;
        if (earnings >= 15000) points += 3;

        return points;
    }

    private static int GetCropUnlockPrestigePoints(string cropId)
    {
        return NormalizeId(cropId) switch
        {
            "carrot" => 1,
            "potato" => 1,
            "corn" => 1,
            "beet" => 1,
            "tomato" => 2,
            "cabbage" => 2,
            "cucumber" => 3,
            "pumpkin" => 3,
            "strawberry" => 4,
            _ => 0
        };
    }

    private static int GetAnimalUnlockPrestigePoints(string animalId)
    {
        return NormalizeId(animalId) switch
        {
            "goat" => 1,
            "sheep" => 1,
            "cow" => 2,
            "duck" => 3,
            _ => 0
        };
    }

    private void ResetCurrentRun()
    {
        GameManager.Instance?.ResetForNewRun(GetStartingMoney());
        TimeManager.Instance?.ResetRunTime();
        ProgressionManager.Instance?.ResetRunProgress();
        AnimalManager.Instance?.ResetRunAnimals();
        FarmManager.Instance?.ClearAllPlots();
        SaveManager.Instance?.SaveGame();
    }

    private void InitializeUpgradeKeys()
    {
        foreach (PrestigeUpgradeDefinition definition in PrestigeUpgradeDefinition.All)
        {
            if (!upgradeLevels.ContainsKey(definition.Id))
            {
                upgradeLevels[definition.Id] = 0;
            }
        }
    }

    public void ReloadForCurrentUser()
    {
        PrestigePoints = 0;
        TotalPrestigeCount = 0;
        ResetUpgradeLevels();
        LoadPrestige();
        OnPrestigeChanged?.Invoke();
    }

    public void ResetForNewAccount()
    {
        PrestigePoints = 0;
        TotalPrestigeCount = 0;
        ResetUpgradeLevels();
        SavePrestige();
        OnPrestigeChanged?.Invoke();
    }

    private void ResetUpgradeLevels()
    {
        upgradeLevels.Clear();
        foreach (PrestigeUpgradeDefinition definition in PrestigeUpgradeDefinition.All)
        {
            upgradeLevels[definition.Id] = 0;
        }
    }

    private void LoadPrestige()
    {
        if (!File.Exists(SavePath))
        {
            SavePrestige();
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            PrestigeSaveData data = JsonUtility.FromJson<PrestigeSaveData>(json);
            if (data == null)
            {
                return;
            }

            PrestigePoints = Mathf.Max(0, data.prestigePoints);
            TotalPrestigeCount = Mathf.Max(0, data.totalPrestigeCount);
            if (data.upgrades != null)
            {
                for (int i = 0; i < data.upgrades.Count; i++)
                {
                    PrestigeUpgradeSaveData saved = data.upgrades[i];
                    if (saved == null || string.IsNullOrWhiteSpace(saved.upgradeId))
                    {
                        continue;
                    }

                    PrestigeUpgradeDefinition definition = GetUpgradeDefinitions().FirstOrDefault(item =>
                        string.Equals(item.Id, saved.upgradeId, StringComparison.OrdinalIgnoreCase));
                    if (definition != null)
                    {
                        upgradeLevels[definition.Id] = Mathf.Clamp(saved.level, 0, definition.MaxLevel);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Prestige save is invalid and will be reset: {exception.Message}");
            PrestigePoints = 0;
            TotalPrestigeCount = 0;
            InitializeUpgradeKeys();
        }
    }

    private void SavePrestige()
    {
        PrestigeSaveData data = new PrestigeSaveData
        {
            prestigePoints = Mathf.Max(0, PrestigePoints),
            totalPrestigeCount = Mathf.Max(0, TotalPrestigeCount),
            upgrades = upgradeLevels.Select(pair => new PrestigeUpgradeSaveData
            {
                upgradeId = pair.Key,
                level = Mathf.Max(0, pair.Value)
            }).ToList()
        };

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}

public class PrestigeUpgradeDefinition
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public int Cost { get; }
    public int MaxLevel { get; }

    private PrestigeUpgradeDefinition(string id, string title, string description, int cost, int maxLevel)
    {
        Id = id;
        Title = title;
        Description = description;
        Cost = cost;
        MaxLevel = maxLevel;
    }

    public static readonly IReadOnlyList<PrestigeUpgradeDefinition> All = new List<PrestigeUpgradeDefinition>
    {
        new(PrestigeManager.FastCropGrowthId, "Быстрый рост культур", "Время роста культур -5% за уровень.", 1, 10),
        new(PrestigeManager.ValuableHarvestId, "Выгодный урожай", "Прибыль культур +5% за уровень.", 2, 10),
        new(PrestigeManager.FastAnimalProductionId, "Быстрая продукция животных", "Время продукции животных -5% за уровень.", 2, 10),
        new(PrestigeManager.ValuableAnimalProductId, "Ценная продукция животных", "Прибыль продукции животных +5% за уровень.", 2, 10),
        new(PrestigeManager.StartingCapitalId, "Стартовый капитал", "+50 монет в начале забега за уровень.", 1, 10),
        new(PrestigeManager.CheapCropsId, "Дешёвые культуры", "Цена культур -4% за уровень.", 2, 8),
        new(PrestigeManager.CheapAnimalsId, "Дешёвые животные", "Цена животных -4% за уровень.", 2, 8),
        new(PrestigeManager.ExperiencedFarmerId, "Опытный фермер", "Продажи для открытия культур засчитываются на 10% быстрее за уровень.", 3, 5),
        new(PrestigeManager.ExperiencedRancherId, "Опытный скотовод", "Продукция для открытия животных засчитывается на 10% быстрее за уровень.", 3, 5),
        new(PrestigeManager.SkillfulHarvestId, "Умелый сбор", "Очки престижа за забег +5% за уровень.", 4, 5)
    };
}
