using System;
using System.Collections.Generic;
using UnityEngine;

public class AnimalManager : MonoBehaviour
{
    public static event Action<string> OnAnimalPurchased;
    public static event Action<string> OnAnimalSold;

    public static AnimalManager Instance;

    private const string ChickenAnimalId = "chicken";
    private const string CowAnimalId = "cow";
    private const int MaxAnimalsOnMap = 4;
    private const string SharedSpawnRootName = "AnimalSpawnRoot";
    private const string SharedSpawnPointPrefix = "AnimalSpawn_";

    [Header("References")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private GameManager gameManager;

    private readonly Dictionary<string, int> ownedById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GameObject>> spawnedById = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Transform> sharedSpawnPoints = new();
    private readonly Dictionary<GameObject, int> sharedSpawnSlotByAnimal = new();

    public int ChickensCount => GetOwnedCount(ChickenAnimalId);
    public int CowsCount => GetOwnedCount(CowAnimalId);
    public int MaxAnimalSlots => MaxAnimalsOnMap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (FindFirstObjectByType<AnimalManager>() != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("AnimalManager");
        managerObject.AddComponent<AnimalManager>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        ResolveDependencies();
        EnsureStateFromCatalog();
    }

    public int GetOwnedCount(string animalId)
    {
        if (string.IsNullOrWhiteSpace(animalId))
        {
            return 0;
        }

        return ownedById.TryGetValue(animalId, out int count) ? Mathf.Max(0, count) : 0;
    }

    public int GetCurrentAnimalCount()
    {
        EnsureStateFromCatalog();

        int total = 0;
        foreach (int count in ownedById.Values)
        {
            total += Mathf.Max(0, count);
        }

        return total;
    }

    public bool HasFreeAnimalSlot()
    {
        return GetCurrentAnimalCount() < MaxAnimalsOnMap;
    }

    public int GetAnimalSellPrice(string animalId)
    {
        ResolveDependencies();
        if (shopManager == null || !shopManager.TryGetAnimalById(animalId, out ShopManager.AnimalShopEntry animal))
        {
            return 0;
        }

        int buyPrice = shopManager.GetAnimalBuyPrice(animal);
        return Mathf.Max(0, Mathf.FloorToInt(buyPrice * 0.5f));
    }

    public List<AnimalCountSaveData> GetOwnedAnimalsForSave()
    {
        ResolveDependencies();
        EnsureStateFromCatalog();

        List<AnimalCountSaveData> output = new();
        if (shopManager == null)
        {
            return output;
        }

        IReadOnlyList<ShopManager.AnimalShopEntry> catalog = shopManager.GetAnimalCatalog();
        for (int i = 0; i < catalog.Count; i++)
        {
            ShopManager.AnimalShopEntry entry = catalog[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.animalId))
            {
                continue;
            }

            output.Add(new AnimalCountSaveData
            {
                animalId = entry.animalId,
                count = GetOwnedCount(entry.animalId)
            });
        }

        return output;
    }

    public List<AnimalProductSaveData> GetAnimalProductStatesForSave()
    {
        ResolveDependencies();
        EnsureStateFromCatalog();

        List<AnimalProductSaveData> output = new();
        foreach (KeyValuePair<string, List<GameObject>> pair in spawnedById)
        {
            List<GameObject> spawned = pair.Value;
            for (int i = 0; i < spawned.Count; i++)
            {
                GameObject animalObject = spawned[i];
                if (animalObject == null)
                {
                    continue;
                }

                AnimalProductProducer producer = animalObject.GetComponent<AnimalProductProducer>();
                if (producer != null)
                {
                    output.Add(producer.GetSaveData(i));
                }
            }
        }

        return output;
    }

    public bool TryBuyAnimal(string animalId, out string message)
    {
        ResolveDependencies();
        EnsureStateFromCatalog();

        if (shopManager == null)
        {
            message = "ShopManager not found.";
            return false;
        }

        if (gameManager == null)
        {
            message = "GameManager not found.";
            return false;
        }

        if (!shopManager.TryGetAnimalById(animalId, out ShopManager.AnimalShopEntry animal))
        {
            message = $"Unknown animal id '{animalId}'.";
            return false;
        }

        if (!HasFreeAnimalSlot())
        {
            message = "Нет свободного места для животного. Продайте одно из животных.";
            return false;
        }

        int buyPrice = shopManager.GetAnimalBuyPrice(animal);

        if (gameManager.money < buyPrice)
        {
            message = $"Не хватает монет. Нужно: {buyPrice}, есть: {gameManager.money}.";
            return false;
        }

        int currentCount = GetOwnedCount(animal.animalId);
        gameManager.SpendMoney(buyPrice);
        ownedById[animal.animalId] = currentCount + 1;
        SpawnAnimalByEntry(animal);
        OnAnimalPurchased?.Invoke(animal.animalId);
        SaveManager.Instance?.SaveGame();

        message = $"{animal.displayName} куплено.";
        return true;
    }

    public bool TrySellAnimal(string animalId, out string message)
    {
        ResolveDependencies();
        EnsureStateFromCatalog();

        if (shopManager == null)
        {
            message = "ShopManager not found.";
            return false;
        }

        if (gameManager == null)
        {
            message = "GameManager not found.";
            return false;
        }

        if (!shopManager.TryGetAnimalById(animalId, out ShopManager.AnimalShopEntry animal))
        {
            message = $"Неизвестное животное: {animalId}.";
            return false;
        }

        int currentCount = GetOwnedCount(animal.animalId);
        if (currentCount <= 0)
        {
            message = $"{animal.displayName} не куплено.";
            return false;
        }

        RemoveLastSpawnedAnimal(animal.animalId);
        ownedById[animal.animalId] = Mathf.Max(0, currentCount - 1);

        int sellPrice = GetAnimalSellPrice(animal.animalId);
        if (sellPrice > 0)
        {
            gameManager.AddMoney(sellPrice, false);
        }

        OnAnimalSold?.Invoke(animal.animalId);
        SaveManager.Instance?.SaveGame();

        message = $"{animal.displayName} продано за {sellPrice}.";
        return true;
    }

    public void SetOwnedAnimalsFromSave(int chickens, int cows)
    {
        SetOwnedAnimalsFromSave(chickens, cows, null);
    }

    public void SetOwnedAnimalsFromSave(int chickens, int cows, List<AnimalProductSaveData> productStates)
    {
        ResolveDependencies();
        EnsureStateFromCatalog();

        foreach (string key in new List<string>(ownedById.Keys))
        {
            ownedById[key] = 0;
        }

        ownedById[ChickenAnimalId] = Mathf.Max(0, chickens);
        ownedById[CowAnimalId] = Mathf.Max(0, cows);
        ClampOwnedToAnimalSlotLimit();
        RebuildSpawnedAnimals(productStates);
    }

    public void ResetRunAnimals()
    {
        ResolveDependencies();
        EnsureStateFromCatalog();
        foreach (string key in new List<string>(ownedById.Keys))
        {
            ownedById[key] = 0;
        }

        ClearAllSpawnedAnimals();
    }

    public void SetOwnedAnimalsFromSave(List<AnimalCountSaveData> animalCounts)
    {
        SetOwnedAnimalsFromSave(animalCounts, null);
    }

    public void SetOwnedAnimalsFromSave(List<AnimalCountSaveData> animalCounts, List<AnimalProductSaveData> productStates)
    {
        ResolveDependencies();
        EnsureStateFromCatalog();

        foreach (string key in new List<string>(ownedById.Keys))
        {
            ownedById[key] = 0;
        }

        if (animalCounts != null)
        {
            for (int i = 0; i < animalCounts.Count; i++)
            {
                AnimalCountSaveData entry = animalCounts[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.animalId))
                {
                    continue;
                }

                if (!ownedById.ContainsKey(entry.animalId))
                {
                    Debug.LogWarning($"AnimalManager: saved animal id '{entry.animalId}' is missing in Animal Catalog and will be ignored.");
                    continue;
                }

                ownedById[entry.animalId] = Mathf.Max(0, entry.count);
            }
        }

        ClampOwnedToAnimalSlotLimit();
        RebuildSpawnedAnimals(productStates);
    }

    private void ResolveDependencies()
    {
        if (shopManager == null)
        {
            shopManager = FindFirstObjectByType<ShopManager>();
        }

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
        }
    }

    private void EnsureStateFromCatalog()
    {
        if (shopManager == null)
        {
            return;
        }

        IReadOnlyList<ShopManager.AnimalShopEntry> catalog = shopManager.GetAnimalCatalog();
        for (int i = 0; i < catalog.Count; i++)
        {
            ShopManager.AnimalShopEntry entry = catalog[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.animalId))
            {
                continue;
            }

            if (!ownedById.ContainsKey(entry.animalId))
            {
                ownedById[entry.animalId] = 0;
            }

            if (!spawnedById.ContainsKey(entry.animalId))
            {
                spawnedById[entry.animalId] = new List<GameObject>();
            }
        }
    }

    private void RebuildSpawnedAnimals(List<AnimalProductSaveData> productStates = null)
    {
        ResolveDependencies();
        EnsureStateFromCatalog();
        ClearAllSpawnedAnimals();

        if (shopManager == null)
        {
            return;
        }

        IReadOnlyList<ShopManager.AnimalShopEntry> catalog = shopManager.GetAnimalCatalog();
        for (int i = 0; i < catalog.Count; i++)
        {
            ShopManager.AnimalShopEntry entry = catalog[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.animalId))
            {
                continue;
            }

            int count = GetOwnedCount(entry.animalId);
            for (int index = 0; index < count; index++)
            {
                GameObject spawned = SpawnAnimalByEntry(entry);
                ApplySavedProductState(spawned, entry.animalId, index, productStates);
            }
        }
    }

    private void ClearAllSpawnedAnimals()
    {
        sharedSpawnSlotByAnimal.Clear();

        foreach (KeyValuePair<string, List<GameObject>> pair in spawnedById)
        {
            List<GameObject> spawned = pair.Value;
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Destroy(spawned[i]);
                }
            }

            spawned.Clear();
        }
    }

    private void ClampOwnedToAnimalSlotLimit()
    {
        int remainingSlots = MaxAnimalsOnMap;
        HashSet<string> processedIds = new(StringComparer.OrdinalIgnoreCase);

        if (shopManager != null)
        {
            IReadOnlyList<ShopManager.AnimalShopEntry> catalog = shopManager.GetAnimalCatalog();
            for (int i = 0; i < catalog.Count; i++)
            {
                ShopManager.AnimalShopEntry entry = catalog[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.animalId) || !ownedById.ContainsKey(entry.animalId))
                {
                    continue;
                }

                int clamped = Mathf.Clamp(ownedById[entry.animalId], 0, remainingSlots);
                ownedById[entry.animalId] = clamped;
                processedIds.Add(entry.animalId);
                remainingSlots -= clamped;
            }
        }

        foreach (string key in new List<string>(ownedById.Keys))
        {
            if (processedIds.Contains(key))
            {
                continue;
            }

            if (remainingSlots <= 0)
            {
                ownedById[key] = 0;
                continue;
            }

            int clamped = Mathf.Clamp(ownedById[key], 0, remainingSlots);
            ownedById[key] = clamped;
            remainingSlots -= clamped;
        }
    }

    private void RemoveLastSpawnedAnimal(string animalId)
    {
        if (!spawnedById.TryGetValue(animalId, out List<GameObject> spawned) || spawned == null)
        {
            return;
        }

        if (spawned.Count <= 0)
        {
            return;
        }

        int lastIndex = spawned.Count - 1;
        GameObject animalObject = spawned[lastIndex];
        spawned.RemoveAt(lastIndex);
        if (animalObject != null)
        {
            sharedSpawnSlotByAnimal.Remove(animalObject);
            Destroy(animalObject);
        }
    }

    private GameObject SpawnAnimalByEntry(ShopManager.AnimalShopEntry animal)
    {
        if (animal == null || string.IsNullOrWhiteSpace(animal.animalId))
        {
            return null;
        }

        if (!spawnedById.TryGetValue(animal.animalId, out List<GameObject> spawnedList))
        {
            spawnedList = new List<GameObject>();
            spawnedById[animal.animalId] = spawnedList;
        }

        int sharedSpawnSlot = FindFirstFreeSharedSpawnSlot();
        GameObject spawned = SpawnAnimal(animal, spawnedList.Count, sharedSpawnSlot);
        if (spawned == null)
        {
            return null;
        }

        spawnedList.Add(spawned);
        if (sharedSpawnSlot >= 0)
        {
            sharedSpawnSlotByAnimal[spawned] = sharedSpawnSlot;
        }

        AnimalProductProducer producer = spawned.GetComponent<AnimalProductProducer>();
        if (producer == null)
        {
            producer = spawned.AddComponent<AnimalProductProducer>();
        }

        producer.Configure(
            animal.animalId,
            animal.productItemId,
            animal.baseSellPrice,
            animal.productionSeconds,
            animal.amountPerCycle,
            animal.maxProductionCycles);

        return spawned;
    }

    private static void ApplySavedProductState(GameObject spawned, string animalId, int spawnIndex, List<AnimalProductSaveData> productStates)
    {
        if (spawned == null || productStates == null)
        {
            return;
        }

        AnimalProductProducer producer = spawned.GetComponent<AnimalProductProducer>();
        if (producer == null)
        {
            return;
        }

        for (int i = 0; i < productStates.Count; i++)
        {
            AnimalProductSaveData state = productStates[i];
            if (state == null)
            {
                continue;
            }

            if (string.Equals(state.animalId, animalId, StringComparison.OrdinalIgnoreCase) &&
                state.spawnIndex == spawnIndex)
            {
                producer.LoadFromSave(state);
                return;
            }
        }
    }

    private int FindFirstFreeSharedSpawnSlot()
    {
        RefreshSharedSpawnPoints();

        for (int i = 0; i < sharedSpawnPoints.Count && i < MaxAnimalsOnMap; i++)
        {
            if (!sharedSpawnSlotByAnimal.ContainsValue(i))
            {
                return i;
            }
        }

        return -1;
    }

    private void RefreshSharedSpawnPoints()
    {
        sharedSpawnPoints.RemoveAll(point => point == null);
        if (sharedSpawnPoints.Count > 0)
        {
            return;
        }

        Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate != null &&
                candidate.name.StartsWith(SharedSpawnPointPrefix, StringComparison.OrdinalIgnoreCase))
            {
                sharedSpawnPoints.Add(candidate);
            }
        }

        sharedSpawnPoints.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));

        if (sharedSpawnPoints.Count > MaxAnimalsOnMap)
        {
            sharedSpawnPoints.RemoveRange(MaxAnimalsOnMap, sharedSpawnPoints.Count - MaxAnimalsOnMap);
        }
    }

    private GameObject SpawnAnimal(ShopManager.AnimalShopEntry animal, int animalIndex, int sharedSpawnSlot)
    {
        GameObject animalTemplate = animal.prefab != null
            ? animal.prefab
            : CatalogRuntimeFallbackFactory.GetOrCreateAnimalTemplate(animal);

        if (animalTemplate == null)
        {
            Debug.LogWarning($"AnimalManager: prefab is not assigned for '{animal.animalId}'.");
            return null;
        }

        Transform selectedPoint = null;
        if (sharedSpawnSlot >= 0 && sharedSpawnSlot < sharedSpawnPoints.Count)
        {
            selectedPoint = sharedSpawnPoints[sharedSpawnSlot];
        }
        else if (animal.spawnPoints != null && animal.spawnPoints.Length > 0)
        {
            selectedPoint = animal.spawnPoints[Mathf.Clamp(animalIndex, 0, animal.spawnPoints.Length - 1)];
        }

        Vector3 spawnPosition = selectedPoint != null
            ? selectedPoint.position
            : (animal.fallbackParent != null ? animal.fallbackParent.position : transform.position);

        Quaternion spawnRotation = selectedPoint != null
            ? selectedPoint.rotation
            : (animal.fallbackParent != null ? animal.fallbackParent.rotation : Quaternion.identity);

        GameObject sharedRoot = GameObject.Find(SharedSpawnRootName);
        Transform spawnParent = animal.fallbackParent != null
            ? animal.fallbackParent
            : (sharedRoot != null ? sharedRoot.transform : transform);

        GameObject spawned = Instantiate(animalTemplate, spawnPosition, spawnRotation, spawnParent);
        spawned.hideFlags = HideFlags.None;
        if (!spawned.activeSelf)
        {
            spawned.SetActive(true);
        }

        return spawned;
    }
}
