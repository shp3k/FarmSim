using UnityEngine;
using UnityEngine.Rendering;

public class FarmPlot : MonoBehaviour
{
    private const string NoFreePlotsMessage = "\u041d\u0435\u0442 \u0441\u0432\u043e\u0431\u043e\u0434\u043d\u044b\u0445 \u0433\u0440\u044f\u0434\u043e\u043a";

    [Header("Plot")]
    [SerializeField] private string plotId;
    [SerializeField] [Min(1)] private int plotIndex = 1;

    [Header("References")]
    [SerializeField] private Transform plantSpawnPoint;

    [Header("State")]
    [SerializeField] private bool isOccupied;
    [SerializeField] private string plantedItemId;
    [SerializeField] private GameObject currentPlantObject;

    public string PlotId => string.IsNullOrWhiteSpace(plotId) ? name : plotId;
    public int PlotIndex => Mathf.Max(1, plotIndex);
    public bool IsOccupied => isOccupied;
    public string PlantedItemId => plantedItemId;

    protected virtual void Awake()
    {
    }

    public bool Plant(string seedItemId, GameObject plantPrefab)
    {
        return TryPlantInternal(
            seedItemId,
            plantPrefab,
            configuredCropId: null,
            configuredHarvestItemId: null,
            configuredGrowthSeconds: 0f);
    }

    public bool PlantFromShop(string seedItemId, GameObject plantPrefab, string cropId, string harvestItemId, float growthSeconds)
    {
        return TryPlantInternal(
            seedItemId,
            plantPrefab,
            configuredCropId: cropId,
            configuredHarvestItemId: harvestItemId,
            configuredGrowthSeconds: growthSeconds);
    }

    public bool HarvestForProfit(out int earned, out string message)
    {
        earned = 0;

        if (!isOccupied || currentPlantObject == null)
        {
            message = $"FarmPlot '{name}' has nothing to harvest.";
            Debug.Log(message);
            return false;
        }

        CropGrowth cropGrowth = currentPlantObject.GetComponent<CropGrowth>();
        if (cropGrowth == null)
        {
            message = $"FarmPlot '{name}': plant has no CropGrowth component.";
            Debug.LogWarning(message);
            return false;
        }

        if (!cropGrowth.IsReadyToHarvest)
        {
            message = $"FarmPlot '{name}': растение ещё не выросло.";
            Debug.Log(message);
            return false;
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        if (gameManager == null)
        {
            message = $"FarmPlot '{name}': GameManager not found.";
            Debug.LogError(message);
            return false;
        }

        string cropId = string.IsNullOrWhiteSpace(cropGrowth.CropId) ? plantedItemId : cropGrowth.CropId;
        int baseSellPrice = ResolveBaseSellPrice(cropId, plantedItemId);
        earned = ProgressionManager.Instance != null
            ? ProgressionManager.Instance.GetCropSellPrice(cropId, baseSellPrice)
            : baseSellPrice;

        gameManager.AddMoney(earned);
        ProgressionManager.Instance?.RegisterCropSales(cropId, 1);

        ClearHarvestedPlant();
        SaveManager.Instance?.SaveGame();

        message = $"FarmPlot '{name}': harvested '{cropId}' for {earned} coins.";
        Debug.Log(message);
        return true;
    }

    public void ClearPlot(bool suppressSave = false)
    {
        if (!isOccupied)
        {
            Debug.Log($"FarmPlot '{name}' is already empty.");
            return;
        }

        if (currentPlantObject != null)
        {
            Destroy(currentPlantObject);
            currentPlantObject = null;
        }

        Debug.Log($"FarmPlot '{name}' was cleared.");
        isOccupied = false;
        plantedItemId = string.Empty;
        if (!suppressSave)
        {
            SaveManager.Instance?.SaveGame();
        }
    }

    public FarmPlotSaveData GetSaveData()
    {
        CropSaveData cropData = null;
        if (currentPlantObject != null)
        {
            CropGrowth crop = currentPlantObject.GetComponent<CropGrowth>();
            if (crop != null)
            {
                cropData = crop.GetSaveData();
            }
        }

        return new FarmPlotSaveData
        {
            plotId = PlotId,
            isOccupied = isOccupied,
            plantedItemId = plantedItemId,
            crop = cropData
        };
    }

    public void LoadFromSave(FarmPlotSaveData data, SaveManager saveManager)
    {
        ClearPlotInternal();

        if (data == null || !data.isOccupied || data.crop == null)
        {
            return;
        }

        if (saveManager == null)
        {
            Debug.LogError($"FarmPlot '{name}': SaveManager missing while loading.");
            return;
        }

        GameObject plantPrefab = saveManager.GetPlantPrefab(data.crop.cropId);
        if (plantPrefab == null)
        {
            Debug.LogWarning($"FarmPlot '{name}': no prefab for crop '{data.crop.cropId}'.");
            return;
        }

        Transform spawn = GetResolvedSpawnPoint();
        currentPlantObject = Instantiate(plantPrefab, spawn.position, Quaternion.identity, transform);
        currentPlantObject.SetActive(true);

        CropGrowth cropGrowth = currentPlantObject.GetComponent<CropGrowth>();
        if (cropGrowth != null)
        {
            cropGrowth.ConfigureFromCatalog(data.crop.cropId, data.crop.harvestItemId, 0f);
            cropGrowth.LoadFromSave(data.crop);
        }

        PrepareSpawnedPlant(currentPlantObject, spawn);

        isOccupied = true;
        plantedItemId = data.plantedItemId;
    }

    private void ClearPlotInternal()
    {
        if (currentPlantObject != null)
        {
            Destroy(currentPlantObject);
            currentPlantObject = null;
        }

        isOccupied = false;
        plantedItemId = string.Empty;
    }

    private void ClearHarvestedPlant()
    {
        if (currentPlantObject != null)
        {
            Destroy(currentPlantObject);
            currentPlantObject = null;
        }

        isOccupied = false;
        plantedItemId = string.Empty;
    }

    private void PrepareSpawnedPlant(GameObject plantedObject, Transform spawnPoint)
    {
        if (plantedObject == null)
        {
            return;
        }

        plantedObject.SetActive(true);
        plantedObject.hideFlags = HideFlags.None;
        Vector3 plantedPosition = spawnPoint.position;
        plantedPosition.z += 0.1f;
        plantedObject.transform.position = plantedPosition;
        plantedObject.transform.rotation = Quaternion.identity;
        plantedObject.transform.localScale = Vector3.one;

        // Keep plants visually above soil to avoid "spawned but not visible" situations.
        int targetSortingLayerId = 0;
        int targetSortingOrder = 10;
        ResolvePlantSorting(out targetSortingLayerId, out targetSortingOrder);

        SpriteRenderer[] renderers = plantedObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
                Color color = renderers[i].color;
                color.a = 1f;
                renderers[i].color = color;
                renderers[i].sortingLayerID = targetSortingLayerId;
                renderers[i].sortingOrder = Mathf.Max(renderers[i].sortingOrder, targetSortingOrder);
            }
        }

        SortingGroup sortingGroup = plantedObject.GetComponent<SortingGroup>();
        if (sortingGroup == null)
        {
            sortingGroup = plantedObject.AddComponent<SortingGroup>();
        }

        sortingGroup.sortingLayerID = targetSortingLayerId;
        sortingGroup.sortingOrder = targetSortingOrder;

        AlignPlantVisualBottomToSpawnPoint(plantedObject, spawnPoint);
        EnsurePlantClickSupport(plantedObject);
    }

    private void EnsurePlantClickSupport(GameObject plantedObject)
    {
        if (plantedObject == null)
        {
            return;
        }

        PlantHarvestClickHandler clickHandler = plantedObject.GetComponent<PlantHarvestClickHandler>();
        if (clickHandler == null)
        {
            clickHandler = plantedObject.AddComponent<PlantHarvestClickHandler>();
        }

        clickHandler.Configure(this);

        BoxCollider2D clickCollider = plantedObject.GetComponent<BoxCollider2D>();
        if (clickCollider == null)
        {
            clickCollider = plantedObject.AddComponent<BoxCollider2D>();
        }

        clickCollider.isTrigger = true;
        clickHandler.RefreshClickColliderFromVisuals();
    }

    private Transform GetResolvedSpawnPoint()
    {
        return plantSpawnPoint != null ? plantSpawnPoint : transform;
    }

    private void ResolvePlantSorting(out int sortingLayerId, out int sortingOrder)
    {
        sortingLayerId = SortingLayer.NameToID("Default");
        sortingOrder = 100;

        GameObject background = GameObject.Find("MainBackGround");
        if (background == null)
        {
            return;
        }

        SpriteRenderer backgroundRenderer = background.GetComponent<SpriteRenderer>();
        if (backgroundRenderer == null)
        {
            return;
        }

        sortingLayerId = backgroundRenderer.sortingLayerID;
        sortingOrder = Mathf.Max(backgroundRenderer.sortingOrder + 200, 50);
    }

    private void AlignPlantVisualBottomToSpawnPoint(GameObject plantedObject, Transform spawnPoint)
    {
        if (plantedObject == null || spawnPoint == null)
        {
            return;
        }

        SpriteRenderer[] renderers = plantedObject.GetComponentsInChildren<SpriteRenderer>(true);
        Bounds? activeBounds = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (activeBounds == null)
            {
                activeBounds = renderer.bounds;
            }
            else
            {
                Bounds bounds = activeBounds.Value;
                bounds.Encapsulate(renderer.bounds);
                activeBounds = bounds;
            }
        }

        if (activeBounds == null)
        {
            return;
        }

        Bounds visualBounds = activeBounds.Value;
        Vector3 visualBottomCenter = new(visualBounds.center.x, visualBounds.min.y, plantedObject.transform.position.z);
        Vector3 targetBottomCenter = new(spawnPoint.position.x, spawnPoint.position.y, plantedObject.transform.position.z);
        Vector3 offset = targetBottomCenter - visualBottomCenter;
        plantedObject.transform.position += offset;
    }

    private bool TryPlantInternal(
        string seedItemId,
        GameObject plantPrefab,
        string configuredCropId,
        string configuredHarvestItemId,
        float configuredGrowthSeconds)
    {
        if (FarmManager.Instance != null && FarmManager.Instance.GetFreePlotsCount() <= 0)
        {
            Debug.Log(NoFreePlotsMessage);
            return false;
        }

        if (isOccupied)
        {
            Debug.Log($"FarmPlot '{name}' is already occupied by '{plantedItemId}'.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(seedItemId))
        {
            Debug.LogWarning($"FarmPlot '{name}': seed item id is empty.");
            return false;
        }

        if (plantPrefab == null)
        {
            Debug.LogWarning($"FarmPlot '{name}': plant prefab is not assigned.");
            return false;
        }

        Transform spawn = GetResolvedSpawnPoint();
        currentPlantObject = Instantiate(plantPrefab, spawn.position, Quaternion.identity, transform);
        ConfigureSpawnedCrop(currentPlantObject, configuredCropId, configuredHarvestItemId, configuredGrowthSeconds);
        PrepareSpawnedPlant(currentPlantObject, spawn);

        isOccupied = true;
        plantedItemId = seedItemId;

        string objectName = currentPlantObject != null ? currentPlantObject.name : "NULL";
        Debug.Log($"FarmPlot '{name}': planted '{seedItemId}', spawn={spawn.position}, object={objectName}.");

        SaveManager.Instance?.SaveGame();
        return true;
    }

    private void ConfigureSpawnedCrop(GameObject plantObject, string cropId, string harvestItemId, float growthSeconds)
    {
        if (plantObject == null)
        {
            return;
        }

        CropGrowth cropGrowth = plantObject.GetComponent<CropGrowth>();
        if (cropGrowth == null)
        {
            cropGrowth = plantObject.AddComponent<CropGrowth>();
        }

        cropGrowth.ConfigureFromCatalog(cropId, harvestItemId, growthSeconds);
    }

    private int ResolveBaseSellPrice(string cropId, string seedItemId)
    {
        ShopManager shopManager = FindFirstObjectByType<ShopManager>();
        if (shopManager == null)
        {
            Debug.LogWarning($"FarmPlot '{name}': ShopManager not found while resolving sell price for '{cropId}'.");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(cropId) &&
            shopManager.TryGetCropById(cropId, out ShopManager.CropShopEntry cropById))
        {
            return Mathf.Max(1, cropById.baseSellPrice);
        }

        if (!string.IsNullOrWhiteSpace(seedItemId) &&
            shopManager.TryGetCropBySeedId(seedItemId, out ShopManager.CropShopEntry cropBySeedId))
        {
            return Mathf.Max(1, cropBySeedId.baseSellPrice);
        }

        Debug.LogWarning($"FarmPlot '{name}': crop '{cropId}' is missing in Crop Catalog.");
        return 1;
    }
}
