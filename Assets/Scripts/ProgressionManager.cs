using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LevelProgressSaveData
{
    public string id;
    public int level = 1;
    public int progressToNext;
    public int lifetimeActions;
}

public class ProgressionManager : MonoBehaviour
{
    [Serializable]
    private class ProgressState
    {
        public int level = 1;
        public int progressToNext;
        public int lifetimeActions;
    }

    public static ProgressionManager Instance { get; private set; }
    public static event Action OnProgressionChanged;

    private const int MaxLevel = 5;
    private static readonly int[] LevelThresholds = { 0, 10, 25, 50, 100 };
    private static readonly float[] ProfitMultipliers = { 1f, 1.1f, 1.2f, 1.35f, 1.5f };
    private static readonly float[] TimeMultipliers = { 1f, 0.97f, 0.94f, 0.91f, 0.88f };

    private readonly Dictionary<string, ProgressState> cropProgress = new();
    private readonly Dictionary<string, ProgressState> animalProgress = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (FindFirstObjectByType<ProgressionManager>() != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("ProgressionManager");
        managerObject.AddComponent<ProgressionManager>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public int GetCropLevel(string cropId)
    {
        ProgressState state = GetOrCreateState(cropProgress, cropId);
        RefreshStateFromLifetimeActions(state);
        return state.level;
    }

    public int GetAnimalLevel(string animalId)
    {
        ProgressState state = GetOrCreateState(animalProgress, animalId);
        RefreshStateFromLifetimeActions(state);
        return state.level;
    }

    public int GetCropProgressToNextLevel(string cropId)
    {
        ProgressState state = GetOrCreateState(cropProgress, cropId);
        RefreshStateFromLifetimeActions(state);
        return state.progressToNext;
    }

    public int GetAnimalProgressToNextLevel(string animalId)
    {
        ProgressState state = GetOrCreateState(animalProgress, animalId);
        RefreshStateFromLifetimeActions(state);
        return state.progressToNext;
    }

    public int GetRequiredActionsForNextCropLevel(string cropId)
    {
        ProgressState state = GetOrCreateState(cropProgress, cropId);
        RefreshStateFromLifetimeActions(state);
        return GetRequiredActionsForNextLevel(state.level);
    }

    public int GetRequiredActionsForNextAnimalLevel(string animalId)
    {
        ProgressState state = GetOrCreateState(animalProgress, animalId);
        RefreshStateFromLifetimeActions(state);
        return GetRequiredActionsForNextLevel(state.level);
    }

    public int GetCropSellPrice(string cropId, int basePrice)
    {
        float prestigeMultiplier = PrestigeManager.Instance != null
            ? PrestigeManager.Instance.GetCropProfitPrestigeMultiplier()
            : 1f;
        return Mathf.Max(1, Mathf.FloorToInt(basePrice * GetCropProfitMultiplier(cropId) * prestigeMultiplier));
    }

    public int GetAnimalProductSellPrice(string animalId, int basePrice)
    {
        float prestigeMultiplier = PrestigeManager.Instance != null
            ? PrestigeManager.Instance.GetAnimalProfitPrestigeMultiplier()
            : 1f;
        return Mathf.Max(1, Mathf.FloorToInt(basePrice * GetAnimalProfitMultiplier(animalId) * prestigeMultiplier));
    }

    public float GetCropGrowthSeconds(string cropId, float baseGrowthSeconds)
    {
        float prestigeMultiplier = PrestigeManager.Instance != null
            ? PrestigeManager.Instance.GetCropGrowthPrestigeMultiplier()
            : 1f;
        return Mathf.Max(0.1f, baseGrowthSeconds * GetCropGrowthTimeMultiplier(cropId) * prestigeMultiplier);
    }

    public float GetAnimalProductionSeconds(string animalId, float baseProductionSeconds)
    {
        float prestigeMultiplier = PrestigeManager.Instance != null
            ? PrestigeManager.Instance.GetAnimalProductionPrestigeMultiplier()
            : 1f;
        return Mathf.Max(0.1f, baseProductionSeconds * GetAnimalProductionTimeMultiplier(animalId) * prestigeMultiplier);
    }

    public float GetCropProfitMultiplier(string cropId)
    {
        return GetProfitMultiplier(GetCropLevel(cropId));
    }

    public float GetAnimalProfitMultiplier(string animalId)
    {
        return GetProfitMultiplier(GetAnimalLevel(animalId));
    }

    public float GetCropGrowthTimeMultiplier(string cropId)
    {
        return GetTimeMultiplier(GetCropLevel(cropId));
    }

    public float GetAnimalProductionTimeMultiplier(string animalId)
    {
        return GetTimeMultiplier(GetAnimalLevel(animalId));
    }

    public int GetMaxLevel()
    {
        return MaxLevel;
    }

    public int GetRequiredLifetimeActionsForLevel(int level)
    {
        int safeLevel = Mathf.Clamp(level, 1, MaxLevel);
        return LevelThresholds[safeLevel - 1];
    }

    public float GetLevelProfitMultiplier(int level)
    {
        return GetProfitMultiplier(level);
    }

    public float GetLevelTimeMultiplier(int level)
    {
        return GetTimeMultiplier(level);
    }

    public int GetCropLifetimeActions(string cropId)
    {
        return GetOrCreateState(cropProgress, cropId).lifetimeActions;
    }

    public int GetAnimalLifetimeActions(string animalId)
    {
        return GetOrCreateState(animalProgress, animalId).lifetimeActions;
    }

    public void RegisterCropSales(string cropId, int soldCount)
    {
        if (soldCount <= 0)
        {
            return;
        }

        ProgressState state = GetOrCreateState(cropProgress, cropId);
        RegisterActions(state, soldCount);
        OnProgressionChanged?.Invoke();
        SaveManager.Instance?.SaveGame();
    }

    public void RegisterAnimalCollection(string animalId, int collectedCount)
    {
        if (collectedCount <= 0)
        {
            return;
        }

        ProgressState state = GetOrCreateState(animalProgress, animalId);
        RegisterActions(state, collectedCount);
        OnProgressionChanged?.Invoke();
        SaveManager.Instance?.SaveGame();
    }

    public List<LevelProgressSaveData> GetCropProgressForSave()
    {
        return ToSaveList(cropProgress);
    }

    public List<LevelProgressSaveData> GetAnimalProgressForSave()
    {
        return ToSaveList(animalProgress);
    }

    public void LoadProgressFromSave(List<LevelProgressSaveData> savedCrops, List<LevelProgressSaveData> savedAnimals)
    {
        cropProgress.Clear();
        animalProgress.Clear();

        ApplySavedEntries(savedCrops, cropProgress);
        ApplySavedEntries(savedAnimals, animalProgress);
    }

    public void ResetRunProgress()
    {
        cropProgress.Clear();
        animalProgress.Clear();
        OnProgressionChanged?.Invoke();
    }

    private static int GetRequiredActionsForNextLevel(int currentLevel)
    {
        int safeLevel = Mathf.Clamp(currentLevel, 1, MaxLevel);
        if (safeLevel >= MaxLevel)
        {
            return 0;
        }

        return LevelThresholds[safeLevel] - LevelThresholds[safeLevel - 1];
    }

    private static ProgressState GetOrCreateState(Dictionary<string, ProgressState> source, string id)
    {
        string key = string.IsNullOrWhiteSpace(id) ? "unknown" : id.Trim();

        if (!source.TryGetValue(key, out ProgressState state))
        {
            state = new ProgressState();
            source[key] = state;
        }

        state.level = Mathf.Max(1, state.level);
        RefreshStateFromLifetimeActions(state);
        return state;
    }

    private static void RegisterActions(ProgressState state, int actionsCount)
    {
        state.lifetimeActions = Mathf.Max(0, state.lifetimeActions + actionsCount);
        RefreshStateFromLifetimeActions(state);
    }

    private static List<LevelProgressSaveData> ToSaveList(Dictionary<string, ProgressState> source)
    {
        List<LevelProgressSaveData> output = new();
        foreach (KeyValuePair<string, ProgressState> pair in source)
        {
            RefreshStateFromLifetimeActions(pair.Value);
            output.Add(new LevelProgressSaveData
            {
                id = pair.Key,
                level = Mathf.Max(1, pair.Value.level),
                progressToNext = Mathf.Max(0, pair.Value.progressToNext),
                lifetimeActions = Mathf.Max(0, pair.Value.lifetimeActions)
            });
        }

        return output;
    }

    private static void ApplySavedEntries(List<LevelProgressSaveData> savedEntries, Dictionary<string, ProgressState> target)
    {
        if (savedEntries == null)
        {
            return;
        }

        for (int i = 0; i < savedEntries.Count; i++)
        {
            LevelProgressSaveData entry = savedEntries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.id))
            {
                continue;
            }

            string key = entry.id.Trim();
            int lifetimeActions = Mathf.Max(0, entry.lifetimeActions);
            if (lifetimeActions <= 0 && entry.level > 1)
            {
                lifetimeActions = GetLifetimeActionsFromLegacyLevelProgress(entry.level, entry.progressToNext);
            }

            ProgressState state = new ProgressState
            {
                lifetimeActions = lifetimeActions
            };
            RefreshStateFromLifetimeActions(state);
            target[key] = new ProgressState
            {
                level = state.level,
                progressToNext = state.progressToNext,
                lifetimeActions = state.lifetimeActions
            };
        }
    }

    private static void RefreshStateFromLifetimeActions(ProgressState state)
    {
        if (state == null)
        {
            return;
        }

        state.lifetimeActions = Mathf.Max(0, state.lifetimeActions);
        state.level = ResolveLevel(state.lifetimeActions);
        state.progressToNext = ResolveProgressToNextLevel(state.lifetimeActions, state.level);
    }

    private static int ResolveLevel(int lifetimeActions)
    {
        int safeActions = Mathf.Max(0, lifetimeActions);
        for (int i = LevelThresholds.Length - 1; i >= 0; i--)
        {
            if (safeActions >= LevelThresholds[i])
            {
                return Mathf.Clamp(i + 1, 1, MaxLevel);
            }
        }

        return 1;
    }

    private static int ResolveProgressToNextLevel(int lifetimeActions, int currentLevel)
    {
        int safeLevel = Mathf.Clamp(currentLevel, 1, MaxLevel);
        if (safeLevel >= MaxLevel)
        {
            return 0;
        }

        int currentThreshold = LevelThresholds[safeLevel - 1];
        int nextThreshold = LevelThresholds[safeLevel];
        return Mathf.Clamp(Mathf.Max(0, lifetimeActions) - currentThreshold, 0, nextThreshold - currentThreshold);
    }

    private static float GetProfitMultiplier(int level)
    {
        return ProfitMultipliers[Mathf.Clamp(level, 1, MaxLevel) - 1];
    }

    private static float GetTimeMultiplier(int level)
    {
        return TimeMultipliers[Mathf.Clamp(level, 1, MaxLevel) - 1];
    }

    private static int GetLifetimeActionsFromLegacyLevelProgress(int legacyLevel, int legacyProgressToNext)
    {
        int safeLevel = Mathf.Clamp(legacyLevel, 1, MaxLevel);
        int threshold = LevelThresholds[safeLevel - 1];
        if (safeLevel >= MaxLevel)
        {
            return threshold;
        }

        int progressLimit = LevelThresholds[safeLevel] - threshold;
        return threshold + Mathf.Clamp(legacyProgressToNext, 0, progressLimit);
    }
}
