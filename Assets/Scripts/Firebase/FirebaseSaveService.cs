using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

#if FARMSIM_FIREBASE
using Firebase.Database;
#endif

public class FirebaseSaveService : MonoBehaviour
{
    public static FirebaseSaveService Instance { get; private set; }

    private const string FirebaseDatabaseUrl = "https://farmsim-90a19-default-rtdb.europe-west1.firebasedatabase.app";

    [SerializeField] private int operationTimeoutSeconds = 10;

#if FARMSIM_FIREBASE
    private DatabaseReference rootReference;
#endif

    public bool IsReady { get; private set; }
    public string LastError { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public async Task<bool> InitializeDatabase()
    {
        FirebaseAuthService authService = GetAuthService();
        if (ShouldUseRestDatabase(authService))
        {
            IsReady = true;
            LastError = string.Empty;
            return true;
        }

        FirebaseInitializer initializer = FirebaseInitializer.Instance ?? FindFirstObjectByType<FirebaseInitializer>();
        if (initializer == null || !await initializer.InitializeFirebaseAsync())
        {
            LastError = "Firebase не инициализирован.";
            return false;
        }

#if FARMSIM_FIREBASE
        rootReference ??= FirebaseDatabase.DefaultInstance.RootReference;
        IsReady = rootReference != null;
        if (IsReady)
        {
            LastError = string.Empty;
        }
        return IsReady;
#else
        LastError = "Firebase Database SDK не подключен. Импортируй FirebaseDatabase и добавь FARMSIM_FIREBASE.";
        IsReady = false;
        return false;
#endif
    }

    public async Task<bool> CreateInitialUserDataIfMissing()
    {
        if (!await EnsureSignedInAndReady())
        {
            return false;
        }

#if FARMSIM_FIREBASE
        FirebaseAuthService authService = GetAuthService();
        string userId = authService.GetUserId();
        try
        {
            if (ShouldUseRestDatabase(authService))
            {
                string userPath = $"users/{userId}";
                string existingJson = await RestDatabaseRequest(authService, "GET", userPath);
                if (!string.IsNullOrWhiteSpace(existingJson) && existingJson != "null")
                {
                    LastError = string.Empty;
                    return true;
                }

                long restNow = CurrentUnixSeconds();
                PlayerCloudSaveData restInitial = new()
                {
                    profile = new PlayerProfileCloudData
                    {
                        displayName = GetSafeDisplayName(),
                        email = authService.GetUserEmail(),
                        createdAt = restNow,
                        lastLoginAt = restNow,
                        lastSaveAt = 0
                    },
                    progress = BuildInitialProgress(),
                    runState = new RunStateCloudData()
                };

                await RestDatabaseRequest(authService, "PUT", userPath, FirebaseJson.ToJson(restInitial));
                LastError = string.Empty;
                return true;
            }

            DataSnapshot userSnapshot = await WithTimeout(rootReference.Child("users").Child(userId).GetValueAsync());
            if (userSnapshot.Exists)
            {
                return true;
            }

            long now = CurrentUnixSeconds();
            PlayerCloudSaveData initial = new()
            {
                profile = new PlayerProfileCloudData
                {
                    displayName = GetSafeDisplayName(),
                    email = authService.GetUserEmail(),
                    createdAt = now,
                    lastLoginAt = now,
                    lastSaveAt = 0
                },
                progress = BuildInitialProgress(),
                runState = new RunStateCloudData()
            };

            await WithTimeout(rootReference.Child("users").Child(userId).SetRawJsonValueAsync(FirebaseJson.ToJson(initial)));
            LastError = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            LastError = $"Не удалось создать cloud save: {exception.Message}";
            Debug.LogWarning(LastError);
            return false;
        }
#else
        await Task.Yield();
        return false;
#endif
    }

    public async Task<bool> SaveProgressToCloud()
    {
        return SaveManager.Instance != null && await SaveProgressToCloud(SaveManager.Instance.CreateSaveDataSnapshot());
    }

    public async Task<bool> SaveProgressToCloud(GameSaveData data)
    {
        if (data == null || !await EnsureSignedInAndReady())
        {
            return false;
        }

#if FARMSIM_FIREBASE
        FirebaseAuthService authService = GetAuthService();
        string userId = authService.GetUserId();
        try
        {
            PlayerProgressCloudData progress = BuildProgressData(data);
            RunStateCloudData runState = BuildRunStateData(data);
            if (ShouldUseRestDatabase(authService))
            {
                await RestDatabaseRequest(authService, "PUT", $"users/{userId}/progress", FirebaseJson.ToJson(progress));
                await RestDatabaseRequest(authService, "PUT", $"users/{userId}/runState", FirebaseJson.ToJson(runState));
                await RestDatabaseRequest(authService, "PUT", $"users/{userId}/gameSave", JsonUtility.ToJson(data, false));
                await RestDatabaseRequest(authService, "PUT", $"users/{userId}/profile/lastSaveAt", CurrentUnixSeconds().ToString());
                await SaveLeaderboardValuesIfNeeded();
                LastError = string.Empty;
                return true;
            }

            DatabaseReference userReference = rootReference.Child("users").Child(userId);
            await WithTimeout(userReference.Child("progress").SetRawJsonValueAsync(FirebaseJson.ToJson(progress)));
            await WithTimeout(userReference.Child("runState").SetRawJsonValueAsync(FirebaseJson.ToJson(runState)));
            await WithTimeout(userReference.Child("gameSave").SetRawJsonValueAsync(JsonUtility.ToJson(data, false)));
            await WithTimeout(userReference.Child("profile").Child("lastSaveAt").SetValueAsync(CurrentUnixSeconds()));
            await SaveLeaderboardValuesIfNeeded();
            LastError = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            if (IsRestSessionMissingException(exception))
            {
                LastError = string.Empty;
                return false;
            }

            LastError = $"Cloud save failed: {GetDetailedExceptionMessage(exception)}";
            Debug.LogWarning(LastError);
            return false;
        }
#else
        await Task.Yield();
        return false;
#endif
    }

    public async Task<GameSaveData> LoadProgressFromCloud()
    {
        if (!await EnsureSignedInAndReady())
        {
            return null;
        }

#if FARMSIM_FIREBASE
        FirebaseAuthService authService = GetAuthService();
        string userId = authService.GetUserId();
        try
        {
            if (ShouldUseRestDatabase(authService))
            {
                string restJson = await RestDatabaseRequest(authService, "GET", $"users/{userId}/gameSave");
                if (string.IsNullOrWhiteSpace(restJson) || restJson == "null")
                {
                    return null;
                }

                LastError = string.Empty;
                return JsonUtility.FromJson<GameSaveData>(restJson);
            }

            DataSnapshot saveSnapshot = await WithTimeout(rootReference.Child("users").Child(userId).Child("gameSave").GetValueAsync());
            if (!saveSnapshot.Exists)
            {
                return null;
            }

            string json = saveSnapshot.GetRawJsonValue();
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            LastError = string.Empty;
            return JsonUtility.FromJson<GameSaveData>(json);
        }
        catch (Exception exception)
        {
            LastError = $"Cloud load failed: {GetDetailedExceptionMessage(exception)}";
            Debug.LogWarning(LastError);
            return null;
        }
#else
        await Task.Yield();
        return null;
#endif
    }

    public async Task<bool> CloudSaveExists()
    {
        if (!await EnsureSignedInAndReady())
        {
            return false;
        }

#if FARMSIM_FIREBASE
        FirebaseAuthService authService = GetAuthService();
        string userId = authService.GetUserId();
        try
        {
            if (ShouldUseRestDatabase(authService))
            {
                string json = await RestDatabaseRequest(authService, "GET", $"users/{userId}/gameSave");
                return !string.IsNullOrWhiteSpace(json) && json != "null";
            }

            DataSnapshot snapshot = await WithTimeout(rootReference.Child("users").Child(userId).Child("gameSave").GetValueAsync());
            return snapshot.Exists;
        }
        catch (Exception exception)
        {
            LastError = $"Cloud save check failed: {GetDetailedExceptionMessage(exception)}";
            Debug.LogWarning(LastError);
            return false;
        }
#else
        await Task.Yield();
        return false;
#endif
    }

    public async Task<bool> SaveProfile()
    {
        if (!await EnsureSignedInAndReady())
        {
            return false;
        }

#if FARMSIM_FIREBASE
        FirebaseAuthService authService = GetAuthService();
        string userId = authService.GetUserId();
        PlayerProfileCloudData profile = new()
        {
            displayName = GetSafeDisplayName(),
            email = authService.GetUserEmail(),
            lastLoginAt = CurrentUnixSeconds()
        };

        try
        {
            if (ShouldUseRestDatabase(authService))
            {
                await RestDatabaseRequest(authService, "PATCH", $"users/{userId}/profile", FirebaseJson.ToJson(new Dictionary<string, object>
                {
                    ["displayName"] = profile.displayName,
                    ["email"] = profile.email,
                    ["lastLoginAt"] = profile.lastLoginAt
                }));
                return true;
            }

            await WithTimeout(rootReference.Child("users").Child(userId).Child("profile").UpdateChildrenAsync(new Dictionary<string, object>
            {
                ["displayName"] = profile.displayName,
                ["email"] = profile.email,
                ["lastLoginAt"] = profile.lastLoginAt
            }));
            return true;
        }
        catch (Exception exception)
        {
            LastError = $"Profile save failed: {GetDetailedExceptionMessage(exception)}";
            Debug.LogWarning(LastError);
            return false;
        }
#else
        await Task.Yield();
        return false;
#endif
    }

    public async Task<PlayerProfileCloudData> LoadProfile()
    {
        if (!await EnsureSignedInAndReady())
        {
            return null;
        }

#if FARMSIM_FIREBASE
        FirebaseAuthService authService = GetAuthService();
        string userId = authService.GetUserId();
        try
        {
            if (ShouldUseRestDatabase(authService))
            {
                string json = await RestDatabaseRequest(authService, "GET", $"users/{userId}/profile");
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    return null;
                }

                return JsonUtility.FromJson<PlayerProfileCloudData>(json);
            }

            DataSnapshot snapshot = await WithTimeout(rootReference.Child("users").Child(userId).Child("profile").GetValueAsync());
            if (!snapshot.Exists)
            {
                return null;
            }

            PlayerProfileCloudData profile = new()
            {
                displayName = ReadString(snapshot, "displayName", "Player"),
                email = ReadString(snapshot, "email", string.Empty),
                createdAt = ReadLong(snapshot, "createdAt", 0),
                lastLoginAt = ReadLong(snapshot, "lastLoginAt", 0),
                lastSaveAt = ReadLong(snapshot, "lastSaveAt", 0)
            };
            return profile;
        }
        catch (Exception exception)
        {
            LastError = $"Profile load failed: {GetDetailedExceptionMessage(exception)}";
            Debug.LogWarning(LastError);
            return null;
        }
#else
        await Task.Yield();
        return null;
#endif
    }

    public Task<bool> UpdateLastLoginAt()
    {
        return UpdateProfileTimestamp("lastLoginAt");
    }

    public Task<bool> UpdateLastSaveAt()
    {
        return UpdateProfileTimestamp("lastSaveAt");
    }

    public async Task<bool> SaveLeaderboardValuesIfNeeded()
    {
        if (!await EnsureSignedInAndReady())
        {
            return false;
        }

#if FARMSIM_FIREBASE
        PrestigeManager prestigeManager = PrestigeManager.Instance;
        GameManager gameManager = GameManager.Instance;
        FirebaseAuthService authService = GetAuthService();
        string userId = authService.GetUserId();
        string displayName = GetSafeDisplayName();
        long now = CurrentUnixSeconds();

        try
        {
            Dictionary<string, object> updates = new()
            {
                [$"leaderboards/prestigePoints/{userId}/displayName"] = displayName,
                [$"leaderboards/prestigePoints/{userId}/prestigePoints"] = prestigeManager != null ? prestigeManager.PrestigePoints : 0,
                [$"leaderboards/prestigePoints/{userId}/totalPrestigeCount"] = prestigeManager != null ? prestigeManager.TotalPrestigeCount : 0,
                [$"leaderboards/prestigePoints/{userId}/updatedAt"] = now,
                [$"leaderboards/totalEarnings/{userId}/displayName"] = displayName,
                [$"leaderboards/totalEarnings/{userId}/totalEarnings"] = gameManager != null ? gameManager.totalRunEarnings : 0,
                [$"leaderboards/totalEarnings/{userId}/updatedAt"] = now
            };

            if (ShouldUseRestDatabase(authService))
            {
                await RestDatabaseRequest(authService, "PATCH", string.Empty, FirebaseJson.ToJson(updates));
                return true;
            }

            await WithTimeout(rootReference.UpdateChildrenAsync(updates));
            return true;
        }
        catch (Exception exception)
        {
            LastError = $"Leaderboard save failed: {GetDetailedExceptionMessage(exception)}";
            Debug.LogWarning(LastError);
            return false;
        }
#else
        await Task.Yield();
        return false;
#endif
    }

    private async Task<bool> UpdateProfileTimestamp(string fieldName)
    {
        if (!await EnsureSignedInAndReady())
        {
            return false;
        }

#if FARMSIM_FIREBASE
        FirebaseAuthService authService = GetAuthService();
        string userId = authService.GetUserId();
        try
        {
            if (ShouldUseRestDatabase(authService))
            {
                await RestDatabaseRequest(authService, "PUT", $"users/{userId}/profile/{fieldName}", CurrentUnixSeconds().ToString());
                return true;
            }

            await WithTimeout(rootReference.Child("users").Child(userId).Child("profile").Child(fieldName).SetValueAsync(CurrentUnixSeconds()));
            return true;
        }
        catch (Exception exception)
        {
            LastError = $"Profile timestamp update failed: {GetDetailedExceptionMessage(exception)}";
            Debug.LogWarning(LastError);
            return false;
        }
#else
        await Task.Yield();
        return false;
#endif
    }

    private async Task<bool> EnsureSignedInAndReady()
    {
        FirebaseAuthService authService = GetAuthService();
        if (authService == null || !authService.IsSignedIn())
        {
            LastError = string.Empty;
            return false;
        }

        if (authService.HasRestSession() && !await authService.EnsureRestSessionFresh())
        {
            LastError = string.Empty;
            IsReady = false;
            return false;
        }

        return IsReady || await InitializeDatabase();
    }

    private static bool ShouldUseRestDatabase(FirebaseAuthService authService)
    {
        return authService != null && authService.HasRestSession();
    }

    private static PlayerProgressCloudData BuildInitialProgress()
    {
        PlayerProgressCloudData progress = new()
        {
            coins = 100,
            totalRunEarnings = 0,
            totalPrestigeCount = 0,
            prestigePoints = 0,
            tutorialHints = new TutorialHintsCloudData(),
            prestigeUpgrades = new PrestigeUpgradesCloudData()
        };

        progress.crops["wheat"] = new CropProgressCloudData { soldCount = 0, unlocked = true };
        progress.crops["carrot"] = new CropProgressCloudData { soldCount = 0, unlocked = false };
        progress.animals["chicken"] = new AnimalProgressCloudData { ownedCount = 0, collectedProductCount = 0, unlocked = true };
        progress.animals["cow"] = new AnimalProgressCloudData { ownedCount = 0, collectedProductCount = 0, unlocked = false };
        return progress;
    }

    private static PlayerProgressCloudData BuildProgressData(GameSaveData data)
    {
        PlayerProgressCloudData progress = BuildInitialProgress();
        progress.coins = data.money;
        progress.totalRunEarnings = data.totalRunEarnings;

        if (PrestigeManager.Instance != null)
        {
            progress.prestigePoints = PrestigeManager.Instance.PrestigePoints;
            progress.totalPrestigeCount = PrestigeManager.Instance.TotalPrestigeCount;
            progress.prestigeUpgrades = BuildPrestigeUpgradesData(PrestigeManager.Instance);
        }

        TutorialHintsData hints = TutorialHintsSave.Data;
        progress.tutorialHints = new TutorialHintsCloudData
        {
            tabHintSeen = hints.tabHintSeen,
            shopHintSeen = hints.shopHintSeen,
            harvestHintSeen = hints.harvestHintSeen,
            animalHintSeen = hints.animalHintSeen,
            prestigeHintSeen = hints.prestigeHintSeen
        };

        if (data.cropProgress != null)
        {
            foreach (LevelProgressSaveData item in data.cropProgress)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id))
                {
                    continue;
                }

                progress.crops[item.id] = new CropProgressCloudData
                {
                    soldCount = Mathf.Max(0, item.lifetimeActions),
                    unlocked = item.lifetimeActions > 0 || string.Equals(item.id, "wheat", StringComparison.OrdinalIgnoreCase)
                };
            }
        }

        if (data.animalProgress != null)
        {
            foreach (LevelProgressSaveData item in data.animalProgress)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id))
                {
                    continue;
                }

                progress.animals[item.id] = new AnimalProgressCloudData
                {
                    collectedProductCount = Mathf.Max(0, item.lifetimeActions),
                    ownedCount = GetOwnedAnimalCount(data, item.id),
                    unlocked = item.lifetimeActions > 0 || string.Equals(item.id, "chicken", StringComparison.OrdinalIgnoreCase)
                };
            }
        }

        return progress;
    }

    private static RunStateCloudData BuildRunStateData(GameSaveData data)
    {
        RunStateCloudData runState = new();
        if (data.plots != null)
        {
            for (int i = 0; i < data.plots.Count; i++)
            {
                FarmPlotSaveData plot = data.plots[i];
                if (plot == null || !plot.isOccupied || plot.crop == null)
                {
                    continue;
                }

                string key = string.IsNullOrWhiteSpace(plot.plotId) ? $"plot_{i}" : plot.plotId;
                runState.plantedCrops[key] = new PlantedCropCloudData
                {
                    cropId = plot.crop.cropId,
                    growthProgressSeconds = plot.crop.elapsedGrowthSeconds,
                    stage = plot.crop.currentStage,
                    readyToHarvest = plot.crop.elapsedGrowthSeconds >= plot.crop.totalGrowthSeconds
                };
            }
        }

        if (data.animals?.productStates != null)
        {
            for (int i = 0; i < data.animals.productStates.Count; i++)
            {
                AnimalProductSaveData animal = data.animals.productStates[i];
                if (animal == null)
                {
                    continue;
                }

                runState.spawnedAnimals[$"animal_{i}"] = new SpawnedAnimalCloudData
                {
                    animalId = animal.animalId,
                    spawnPointIndex = animal.spawnIndex,
                    productionProgressSeconds = animal.elapsedProductionSeconds,
                    productReady = animal.isProductReady
                };
            }
        }

        return runState;
    }

    private static PrestigeUpgradesCloudData BuildPrestigeUpgradesData(PrestigeManager prestige)
    {
        return new PrestigeUpgradesCloudData
        {
            cropGrowthSpeed = prestige.GetUpgradeLevel(PrestigeManager.FastCropGrowthId),
            cropProfit = prestige.GetUpgradeLevel(PrestigeManager.ValuableHarvestId),
            animalProductionSpeed = prestige.GetUpgradeLevel(PrestigeManager.FastAnimalProductionId),
            animalProfit = prestige.GetUpgradeLevel(PrestigeManager.ValuableAnimalProductId),
            startingCapital = prestige.GetUpgradeLevel(PrestigeManager.StartingCapitalId),
            cropDiscount = prestige.GetUpgradeLevel(PrestigeManager.CheapCropsId),
            animalDiscount = prestige.GetUpgradeLevel(PrestigeManager.CheapAnimalsId),
            farmerExperience = prestige.GetUpgradeLevel(PrestigeManager.ExperiencedFarmerId),
            animalKeeperExperience = prestige.GetUpgradeLevel(PrestigeManager.ExperiencedRancherId),
            skillfulHarvest = prestige.GetUpgradeLevel(PrestigeManager.SkillfulHarvestId)
        };
    }

    private static int GetOwnedAnimalCount(GameSaveData data, string animalId)
    {
        if (data.animals?.ownedAnimals == null)
        {
            return 0;
        }

        foreach (AnimalCountSaveData animal in data.animals.ownedAnimals)
        {
            if (animal != null && string.Equals(animal.animalId, animalId, StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Max(0, animal.count);
            }
        }

        return 0;
    }

    private static long CurrentUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static string GetSafeDisplayName()
    {
        FirebaseAuthService authService = GetAuthService();
        string displayName = authService != null ? authService.GetDisplayName() : string.Empty;
        return string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
    }

    private static string GetDetailedExceptionMessage(Exception exception)
    {
        if (exception == null)
        {
            return "Unknown error.";
        }

        if (exception is AggregateException aggregateException)
        {
            aggregateException = aggregateException.Flatten();
            if (aggregateException.InnerExceptions.Count > 0)
            {
                List<string> messages = new();
                foreach (Exception innerException in aggregateException.InnerExceptions)
                {
                    messages.Add(GetDetailedExceptionMessage(innerException));
                }

                return string.Join("; ", messages);
            }
        }

        return exception.InnerException != null
            ? $"{exception.Message}: {GetDetailedExceptionMessage(exception.InnerException)}"
            : exception.Message;
    }

    private static bool IsRestSessionMissingException(Exception exception)
    {
        if (exception == null)
        {
            return false;
        }

        if (exception is AggregateException aggregateException)
        {
            aggregateException = aggregateException.Flatten();
            foreach (Exception innerException in aggregateException.InnerExceptions)
            {
                if (IsRestSessionMissingException(innerException))
                {
                    return true;
                }
            }
        }

        string message = exception.Message ?? string.Empty;
        return message.IndexOf("REST Firebase auth token is missing", StringComparison.OrdinalIgnoreCase) >= 0 ||
               message.IndexOf("REST Firebase session expired", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static FirebaseAuthService GetAuthService()
    {
        return FirebaseAuthService.Instance ?? FindFirstObjectByType<FirebaseAuthService>();
    }

#if FARMSIM_FIREBASE
    private async Task<string> RestDatabaseRequest(FirebaseAuthService authService, string method, string path, string jsonBody = null)
    {
        if (authService == null || !await authService.EnsureRestSessionFresh())
        {
            throw new InvalidOperationException("REST Firebase session expired. Please sign in again.");
        }

        string idToken = authService.GetRestIdToken();
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidOperationException("REST Firebase auth token is missing.");
        }

        string normalizedPath = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim('/');
        string url = string.IsNullOrEmpty(normalizedPath)
            ? $"{FirebaseDatabaseUrl}/.json?auth={UnityWebRequest.EscapeURL(idToken)}"
            : $"{FirebaseDatabaseUrl}/{normalizedPath}.json?auth={UnityWebRequest.EscapeURL(idToken)}";

        using UnityWebRequest request = new(url, method);
        request.downloadHandler = new DownloadHandlerBuffer();

        if (jsonBody != null)
        {
            byte[] body = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.SetRequestHeader("Content-Type", "application/json");
        }

        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        float timeoutAt = Time.realtimeSinceStartup + Mathf.Max(1, operationTimeoutSeconds);
        while (!operation.isDone)
        {
            if (Time.realtimeSinceStartup >= timeoutAt)
            {
                request.Abort();
                throw new TimeoutException("Firebase REST request timeout.");
            }

            await Task.Yield();
        }

        string response = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new InvalidOperationException($"Firebase REST {method} {normalizedPath} failed: {request.responseCode} {request.error}. {response}");
        }

        return response;
    }

    private async Task<T> WithTimeout<T>(Task<T> task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(Mathf.Max(1, operationTimeoutSeconds))));
        if (completed != task)
        {
            throw new TimeoutException("Firebase request timeout.");
        }

        return await task;
    }

    private async Task WithTimeout(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(Mathf.Max(1, operationTimeoutSeconds))));
        if (completed != task)
        {
            throw new TimeoutException("Firebase request timeout.");
        }

        await task;
    }

    private static string ReadString(DataSnapshot snapshot, string childName, string fallback)
    {
        object value = snapshot.Child(childName).Value;
        return value != null ? value.ToString() : fallback;
    }

    private static long ReadLong(DataSnapshot snapshot, string childName, long fallback)
    {
        object value = snapshot.Child(childName).Value;
        if (value == null)
        {
            return fallback;
        }

        return long.TryParse(value.ToString(), out long result) ? result : fallback;
    }
#endif
}
