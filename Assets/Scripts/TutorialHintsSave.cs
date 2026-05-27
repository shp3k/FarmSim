using System;
using System.IO;
using UnityEngine;

[Serializable]
public class TutorialHintsData
{
    public bool tabHintSeen;
    public bool shopHintSeen;
    public bool harvestHintSeen;
    public bool animalHintSeen;
    public bool encyclopediaHintSeen;
    public bool prestigeHintSeen;
}

public static class TutorialHintsSave
{
    public const string SaveFileName = "tutorial_hints.json";
    public const string LegacyTabHintPlayerPrefsKey = "farmsim_tab_hint_seen";

    private static TutorialHintsData cachedData;
    private static bool hasLoaded;
    private static string loadedPath;

    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveManager.GetScopedFileName(SaveFileName));

    public static TutorialHintsData Data
    {
        get
        {
            EnsureLoaded();
            return cachedData;
        }
    }

    public static bool TabHintSeen => Data.tabHintSeen;
    public static bool ShopHintSeen => Data.shopHintSeen;
    public static bool HarvestHintSeen => Data.harvestHintSeen;
    public static bool AnimalHintSeen => Data.animalHintSeen;
    public static bool EncyclopediaHintSeen => Data.encyclopediaHintSeen;
    public static bool PrestigeHintSeen => Data.prestigeHintSeen;

    public static void MarkTabHintSeen()
    {
        MarkSeen(data => data.tabHintSeen = true);
    }

    public static void MarkShopHintSeen()
    {
        MarkSeen(data => data.shopHintSeen = true);
    }

    public static void MarkHarvestHintSeen()
    {
        MarkSeen(data => data.harvestHintSeen = true);
    }

    public static void MarkAnimalHintSeen()
    {
        MarkSeen(data => data.animalHintSeen = true);
    }

    public static void MarkEncyclopediaHintSeen()
    {
        MarkSeen(data => data.encyclopediaHintSeen = true);
    }

    public static void MarkPrestigeHintSeen()
    {
        MarkSeen(data => data.prestigeHintSeen = true);
    }

    public static void ClearTutorialHints()
    {
        string currentPath = SavePath;
        cachedData = new TutorialHintsData();
        hasLoaded = true;
        loadedPath = currentPath;

        if (File.Exists(currentPath))
        {
            File.Delete(currentPath);
        }

        ClearLegacyPlayerPrefsKey();
        Debug.Log($"Tutorial hints cleared. File: {currentPath}");
    }

    public static void Save()
    {
        EnsureLoaded();
        string json = JsonUtility.ToJson(cachedData, true);
        File.WriteAllText(loadedPath, json);
    }

    private static void EnsureLoaded()
    {
        string currentPath = SavePath;
        if (hasLoaded && string.Equals(loadedPath, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ClearLegacyPlayerPrefsKey();
        loadedPath = currentPath;

        if (!File.Exists(currentPath))
        {
            cachedData = new TutorialHintsData();
            hasLoaded = true;
            return;
        }

        try
        {
            string json = File.ReadAllText(currentPath);
            cachedData = JsonUtility.FromJson<TutorialHintsData>(json) ?? new TutorialHintsData();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Tutorial hints save is invalid and will be reset: {exception.Message}");
            cachedData = new TutorialHintsData();
        }

        hasLoaded = true;
    }

    private static void MarkSeen(Action<TutorialHintsData> apply)
    {
        EnsureLoaded();
        apply?.Invoke(cachedData);
        Save();
    }

    private static void ClearLegacyPlayerPrefsKey()
    {
        if (PlayerPrefs.HasKey(LegacyTabHintPlayerPrefsKey))
        {
            PlayerPrefs.DeleteKey(LegacyTabHintPlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
