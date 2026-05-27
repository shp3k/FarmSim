using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

[Serializable]
public class CropSaveData
{
    public string cropId;
    public string harvestItemId;
    [FormerlySerializedAs("totalGrowthMinutes")]
    public float totalGrowthSeconds;
    // Backward compatibility with old save files.
    public float totalGrowthMinutes;
    public int currentStage;
    public float elapsedGrowthSeconds;
    public long lastGrowthUnixSeconds;
}

[Serializable]
public class FarmPlotSaveData
{
    public string plotId;
    public bool isOccupied;
    public string plantedItemId;
    public CropSaveData crop;
}

[Serializable]
public class GameSaveData
{
    public int money;
    public int totalRunEarnings;
    public int currentDay = 1;
    public float dayProgressSeconds;
    public AnimalSaveData animals = new();
    public List<FarmPlotSaveData> plots = new();
    public List<LevelProgressSaveData> cropProgress = new();
    public List<LevelProgressSaveData> animalProgress = new();
}

[Serializable]
public class AnimalSaveData
{
    public int chickensCount;
    public int cowsCount;
    public List<AnimalCountSaveData> ownedAnimals = new();
    public List<AnimalProductSaveData> productStates = new();
}

[Serializable]
public class AnimalCountSaveData
{
    public string animalId;
    public int count;
}

[Serializable]
public class AnimalProductSaveData
{
    public string animalId;
    public int spawnIndex;
    public string productItemId;
    public float productionSeconds;
    public float elapsedProductionSeconds;
    public long lastProductionUnixSeconds;
    public int producedCycles;
    public bool isProductReady;
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;

    [SerializeField] private string saveFileName = "save.json";

    private string SavePath => Path.Combine(Application.persistentDataPath, GetScopedFileName(saveFileName));

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private async void Start()
    {
        await LoadGameAsync();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveGame();
        }
    }

    public void SaveGame()
    {
        GameSaveData data = CreateSaveDataSnapshot();
        if (data == null)
        {
            return;
        }

        SaveLocalGame(data);
        _ = SaveCloudGame(data);
    }

    public GameSaveData CreateSaveDataSnapshot()
    {
        GameManager gameManager = GameManager.Instance;
        TimeManager timeManager = TimeManager.Instance;
        AnimalManager animalManager = AnimalManager.Instance;
        ProgressionManager progressionManager = ProgressionManager.Instance;

        if (gameManager == null)
        {
            return null;
        }

        return new GameSaveData
        {
            money = gameManager.money,
            totalRunEarnings = gameManager.totalRunEarnings,
            currentDay = timeManager != null ? timeManager.CurrentDay : 1,
            dayProgressSeconds = timeManager != null ? timeManager.DayProgressSeconds : 0f,
            animals = new AnimalSaveData
            {
                chickensCount = animalManager != null ? animalManager.ChickensCount : 0,
                cowsCount = animalManager != null ? animalManager.CowsCount : 0,
                ownedAnimals = animalManager != null ? animalManager.GetOwnedAnimalsForSave() : new List<AnimalCountSaveData>(),
                productStates = animalManager != null ? animalManager.GetAnimalProductStatesForSave() : new List<AnimalProductSaveData>()
            },
            plots = GetPlotsSaveData(),
            cropProgress = progressionManager != null ? progressionManager.GetCropProgressForSave() : new List<LevelProgressSaveData>(),
            animalProgress = progressionManager != null ? progressionManager.GetAnimalProgressForSave() : new List<LevelProgressSaveData>()
        };
    }

    public async Task LoadGameAsync()
    {
        PrestigeManager.Instance?.ReloadForCurrentUser();

        FirebaseSaveService firebaseSaveService = FirebaseSaveService.Instance ?? FindFirstObjectByType<FirebaseSaveService>();
        if (firebaseSaveService != null)
        {
            GameSaveData cloudData = await firebaseSaveService.LoadProgressFromCloud();
            if (cloudData != null)
            {
                ApplyGameSaveData(cloudData);
                SaveLocalGame(cloudData);
                Debug.Log("Game loaded from Firebase cloud save.");
                return;
            }
        }

        LoadLocalGame();
    }

    public void LoadGame()
    {
        _ = LoadGameAsync();
    }

    private void SaveLocalGame(GameSaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Game saved: {SavePath}");
    }

    private void LoadLocalGame()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("Save file not found. Starting new game.");
            ApplyFreshRunDefaults();
            return;
        }

        string json = File.ReadAllText(SavePath);
        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
        if (data == null)
        {
            Debug.LogWarning("Save file is empty or invalid.");
            ApplyFreshRunDefaults();
            return;
        }

        ApplyGameSaveData(data);
        Debug.Log("Game loaded.");
    }

    private void ApplyGameSaveData(GameSaveData data)
    {
        GameManager gameManager = GameManager.Instance;
        TimeManager timeManager = TimeManager.Instance;
        AnimalManager animalManager = AnimalManager.Instance;
        ProgressionManager progressionManager = ProgressionManager.Instance;

        if (gameManager != null)
        {
            gameManager.SetMoneyFromSave(data.money, data.totalRunEarnings);
        }

        if (timeManager != null)
        {
            timeManager.SetDayFromSave(data.currentDay, data.dayProgressSeconds);
        }

        if (progressionManager != null)
        {
            progressionManager.LoadProgressFromSave(data.cropProgress, data.animalProgress);
        }

        if (animalManager != null)
        {
            if (data.animals != null && data.animals.ownedAnimals != null && data.animals.ownedAnimals.Count > 0)
            {
                animalManager.SetOwnedAnimalsFromSave(data.animals.ownedAnimals, data.animals.productStates);
            }
            else
            {
                int chickens = data.animals != null ? data.animals.chickensCount : 0;
                int cows = data.animals != null ? data.animals.cowsCount : 0;
                animalManager.SetOwnedAnimalsFromSave(chickens, cows, data.animals != null ? data.animals.productStates : null);
            }
        }

        ApplyPlotsSaveData(data.plots);
    }

    public GameObject GetPlantPrefab(string cropId)
    {
        return ResolvePrefabFromShopManager(cropId);
    }

    private List<FarmPlotSaveData> GetPlotsSaveData()
    {
        List<FarmPlotSaveData> plotData = new();
        FarmPlot[] plots = FindObjectsByType<FarmPlot>(FindObjectsSortMode.None);

        foreach (FarmPlot plot in plots)
        {
            plotData.Add(plot.GetSaveData());
        }

        return plotData;
    }

    private void ApplyPlotsSaveData(List<FarmPlotSaveData> savedPlots)
    {
        if (savedPlots == null)
        {
            return;
        }

        FarmPlot[] plots = FindObjectsByType<FarmPlot>(FindObjectsSortMode.None);

        foreach (FarmPlot plot in plots)
        {
            FarmPlotSaveData data = FindPlotData(savedPlots, plot.PlotId);
            if (data != null)
            {
                plot.LoadFromSave(data, this);
            }
            else
            {
                plot.LoadFromSave(new FarmPlotSaveData { isOccupied = false }, this);
            }
        }
    }

    private FarmPlotSaveData FindPlotData(List<FarmPlotSaveData> savedPlots, string plotId)
    {
        foreach (FarmPlotSaveData plotData in savedPlots)
        {
            if (plotData.plotId == plotId)
            {
                return plotData;
            }
        }

        return null;
    }

    private GameObject ResolvePrefabFromShopManager(string cropId)
    {
        ShopManager shopManager = FindFirstObjectByType<ShopManager>();
        if (shopManager == null)
        {
            Debug.LogWarning($"SaveManager: ShopManager not found while resolving crop prefab for '{cropId}'.");
            return null;
        }

        if (shopManager.TryGetPlantPrefabForCropId(cropId, out GameObject byCropId))
        {
            return byCropId;
        }

        if (shopManager.TryGetPlantPrefabForSeed(cropId, out GameObject bySeedId))
        {
            return bySeedId;
        }

        string knownCropIds = string.Join(", ", shopManager
            .GetCropCatalog()
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.cropId))
            .Select(entry => entry.cropId));

        Debug.LogWarning(
            $"SaveManager: no plant prefab found for crop id '{cropId}'. " +
            $"Known crop ids: [{knownCropIds}]");

        return null;
    }

    private async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FarmPlot[] plots = FindObjectsByType<FarmPlot>(FindObjectsSortMode.None);
        if (plots == null || plots.Length == 0)
        {
            return;
        }

        await LoadGameAsync();
    }

    private async Task SaveCloudGame(GameSaveData data)
    {
        FirebaseSaveService firebaseSaveService = FirebaseSaveService.Instance ?? FindFirstObjectByType<FirebaseSaveService>();
        if (firebaseSaveService == null)
        {
            return;
        }

        bool saved = await firebaseSaveService.SaveProgressToCloud(data);
        if (!saved && !string.IsNullOrWhiteSpace(firebaseSaveService.LastError))
        {
            Debug.LogWarning($"Cloud save skipped: {firebaseSaveService.LastError}");
        }
    }

    private void ApplyFreshRunDefaults()
    {
        GameManager gameManager = GameManager.Instance;
        PrestigeManager prestigeManager = PrestigeManager.Instance ?? FindFirstObjectByType<PrestigeManager>();
        if (gameManager != null)
        {
            int startingMoney = prestigeManager != null ? prestigeManager.GetStartingMoney() : 100;
            gameManager.ResetForNewRun(startingMoney);
        }

        TimeManager.Instance?.ResetRunTime();
        ProgressionManager.Instance?.ResetRunProgress();
        AnimalManager.Instance?.ResetRunAnimals();
        FarmManager.Instance?.ClearAllPlots();
    }

    public void ResetLocalStateForNewAccount()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        PrestigeManager.Instance?.ResetForNewAccount();
        TutorialHintsSave.ClearTutorialHints();
        FindFirstObjectByType<TabHintController>()?.ResetHintForTesting();
        FindFirstObjectByType<TutorialHintsController>()?.ClearTutorialHintsFromInspector();

        ApplyFreshRunDefaults();
    }

    public static string GetScopedFileName(string baseFileName)
    {
        FirebaseAuthService authService = FirebaseAuthService.Instance ?? FindFirstObjectByType<FirebaseAuthService>();
        string userId = authService != null ? authService.GetUserId() : string.Empty;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return baseFileName;
        }

        string extension = Path.GetExtension(baseFileName);
        string name = Path.GetFileNameWithoutExtension(baseFileName);
        return $"{name}_{SanitizeFileName(userId)}{extension}";
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char invalidChar in invalidChars)
        {
            value = value.Replace(invalidChar, '_');
        }

        return value;
    }
}
