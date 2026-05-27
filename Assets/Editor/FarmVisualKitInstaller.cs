using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class FarmVisualKitInstaller
{
    private const string GeneratedRoot = "Assets/ArtGenerated";
    private const string TileOutputPath = "Assets/ArtGenerated/Tiles";

    private const string GrassSpritePath = "Assets/ArtGenerated/Tilemap/tile_grass.png";
    private const string SoilSpritePath = "Assets/ArtGenerated/Tilemap/tile_soil.png";
    private const string PathSpritePath = "Assets/ArtGenerated/Tilemap/tile_path.png";
    private const string WaterSpritePath = "Assets/ArtGenerated/Tilemap/tile_water.png";
    private const string TilledSpritePath = "Assets/ArtGenerated/Tilemap/tile_tilled.png";

    private const string PanelSpritePath = "Assets/ArtGenerated/UI/ui_panel_bg.png";
    private const string PrimaryButtonSpritePath = "Assets/ArtGenerated/UI/ui_button_primary.png";
    private const string SecondaryButtonSpritePath = "Assets/ArtGenerated/UI/ui_button_secondary.png";
    private const string CoinSpritePath = "Assets/ArtGenerated/UI/ui_icon_coin.png";
    private const string ShopWindowSpritePath = "Assets/ArtGenerated/UI/ui_shop_window.png";

    [MenuItem("Tools/FarmSim/Generated Art/Setup Import Settings")]
    public static void SetupImportSettings()
    {
        ConfigureSpriteImport(GrassSpritePath, 256f);
        ConfigureSpriteImport(SoilSpritePath, 256f);
        ConfigureSpriteImport(PathSpritePath, 256f);
        ConfigureSpriteImport(WaterSpritePath, 256f);
        ConfigureSpriteImport(TilledSpritePath, 256f);

        ConfigureSpriteImport(PanelSpritePath, 100f);
        ConfigureSpriteImport(PrimaryButtonSpritePath, 100f);
        ConfigureSpriteImport(SecondaryButtonSpritePath, 100f);
        ConfigureSpriteImport(CoinSpritePath, 100f);
        ConfigureSpriteImport(ShopWindowSpritePath, 100f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Generated art import settings configured.");
    }

    [MenuItem("Tools/FarmSim/Generated Art/Build Demo Tilemap")]
    public static void BuildDemoTilemap()
    {
        SetupImportSettings();
        EnsureTileOutputFolder();

        Sprite grassSprite = LoadSprite(GrassSpritePath);
        Sprite soilSprite = LoadSprite(SoilSpritePath);
        Sprite pathSprite = LoadSprite(PathSpritePath);
        Sprite waterSprite = LoadSprite(WaterSpritePath);
        Sprite tilledSprite = LoadSprite(TilledSpritePath);

        if (grassSprite == null || soilSprite == null || pathSprite == null || waterSprite == null || tilledSprite == null)
        {
            Debug.LogError("Could not load generated tile sprites. Make sure files exist under Assets/ArtGenerated/Tilemap.");
            return;
        }

        Tile grassTile = CreateOrLoadTile("tile_grass.asset", grassSprite);
        Tile soilTile = CreateOrLoadTile("tile_soil.asset", soilSprite);
        Tile pathTile = CreateOrLoadTile("tile_path.asset", pathSprite);
        Tile waterTile = CreateOrLoadTile("tile_water.asset", waterSprite);
        Tile tilledTile = CreateOrLoadTile("tile_tilled.asset", tilledSprite);

        GameObject gridObject = GameObject.Find("GeneratedGrid");
        if (gridObject == null)
        {
            gridObject = new GameObject("GeneratedGrid");
            gridObject.AddComponent<Grid>();
        }

        Tilemap ground = FindOrCreateTilemap(gridObject.transform, "Ground");
        Tilemap decor = FindOrCreateTilemap(gridObject.transform, "Decor");
        Tilemap water = FindOrCreateTilemap(gridObject.transform, "Water");

        ground.ClearAllTiles();
        decor.ClearAllTiles();
        water.ClearAllTiles();

        for (int x = -18; x <= 18; x++)
        {
            for (int y = -10; y <= 10; y++)
            {
                bool border = x == -18 || x == 18 || y == -10 || y == 10;
                ground.SetTile(new Vector3Int(x, y, 0), border ? pathTile : grassTile);
            }
        }

        for (int x = -8; x <= 8; x++)
        {
            for (int y = -3; y <= 3; y++)
            {
                decor.SetTile(new Vector3Int(x, y, 0), soilTile);
            }
        }

        for (int x = -7; x <= 7; x += 2)
        {
            for (int y = -2; y <= 2; y++)
            {
                decor.SetTile(new Vector3Int(x, y, 0), tilledTile);
            }
        }

        for (int x = 11; x <= 17; x++)
        {
            for (int y = 4; y <= 10; y++)
            {
                water.SetTile(new Vector3Int(x, y, 0), waterTile);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeObject = gridObject;
        Debug.Log("Demo tilemap created under GeneratedGrid.");
    }

    [MenuItem("Tools/FarmSim/Generated Art/Build Demo UI")]
    public static void BuildDemoUI()
    {
        SetupImportSettings();

        Sprite panelSprite = LoadSprite(PanelSpritePath);
        Sprite primaryButtonSprite = LoadSprite(PrimaryButtonSpritePath);
        Sprite secondaryButtonSprite = LoadSprite(SecondaryButtonSpritePath);
        Sprite coinSprite = LoadSprite(CoinSpritePath);
        Sprite shopWindowSprite = LoadSprite(ShopWindowSpritePath);

        if (panelSprite == null || primaryButtonSprite == null || secondaryButtonSprite == null || coinSprite == null || shopWindowSprite == null)
        {
            Debug.LogError("Could not load generated UI sprites. Make sure files exist under Assets/ArtGenerated/UI.");
            return;
        }

        EnsureEventSystem();

        GameObject canvasObject = GameObject.Find("GeneratedUICanvas");
        Canvas canvas;

        if (canvasObject == null)
        {
            canvasObject = new GameObject("GeneratedUICanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            canvas = canvasObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        ClearChildren(canvasObject.transform);

        GameObject shopWindow = CreateImage("ShopWindow", canvasObject.transform, shopWindowSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(920f, 640f), Vector2.zero);
        GameObject topPanel = CreateImage("TopPanel", canvasObject.transform, panelSprite, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 190f), new Vector2(0f, -24f));

        GameObject shopButton = CreateButton("ShopButton", topPanel.transform, primaryButtonSprite, "SHOP", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(280f, 90f), new Vector2(190f, 0f));
        GameObject sellButton = CreateButton("SellButton", topPanel.transform, secondaryButtonSprite, "SELL ALL", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(280f, 90f), new Vector2(-190f, 0f));

        GameObject coinPanel = CreateImage("CoinsPanel", topPanel.transform, panelSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 100f), Vector2.zero);
        GameObject coinIcon = CreateImage("CoinIcon", coinPanel.transform, coinSprite, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, 72f), new Vector2(54f, 0f));
        coinIcon.GetComponent<Image>().preserveAspect = true;

        GameObject coinTextObject = new GameObject("CoinsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        coinTextObject.transform.SetParent(coinPanel.transform, false);
        RectTransform coinTextRect = coinTextObject.GetComponent<RectTransform>();
        coinTextRect.anchorMin = new Vector2(0f, 0f);
        coinTextRect.anchorMax = new Vector2(1f, 1f);
        coinTextRect.offsetMin = new Vector2(112f, 0f);
        coinTextRect.offsetMax = new Vector2(-24f, 0f);

        TextMeshProUGUI coinText = coinTextObject.GetComponent<TextMeshProUGUI>();
        coinText.text = "1 250";
        coinText.fontSize = 46;
        coinText.alignment = TextAlignmentOptions.MidlineLeft;
        coinText.color = new Color32(86, 60, 34, 255);

        shopButton.GetComponent<Button>().targetGraphic = shopButton.GetComponent<Image>();
        sellButton.GetComponent<Button>().targetGraphic = sellButton.GetComponent<Image>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeObject = canvasObject;
        Debug.Log("Demo UI created under GeneratedUICanvas.");
    }

    private static void ConfigureSpriteImport(string path, float pixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (Mathf.Abs(importer.spritePixelsPerUnit - pixelsPerUnit) > 0.001f)
        {
            importer.spritePixelsPerUnit = pixelsPerUnit;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureTileOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedRoot))
        {
            AssetDatabase.CreateFolder("Assets", "ArtGenerated");
        }

        if (!AssetDatabase.IsValidFolder(TileOutputPath))
        {
            AssetDatabase.CreateFolder("Assets/ArtGenerated", "Tiles");
        }
    }

    private static Tile CreateOrLoadTile(string fileName, Sprite sprite)
    {
        string assetPath = $"{TileOutputPath}/{fileName}";
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);

        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            AssetDatabase.CreateAsset(tile, assetPath);
        }
        else
        {
            tile.sprite = sprite;
            EditorUtility.SetDirty(tile);
        }

        return tile;
    }

    private static Tilemap FindOrCreateTilemap(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Tilemap tilemap = existing.GetComponent<Tilemap>();
            if (tilemap != null)
            {
                return tilemap;
            }
        }

        GameObject layer = new GameObject(name);
        layer.transform.SetParent(parent, false);
        layer.AddComponent<Tilemap>();
        layer.AddComponent<TilemapRenderer>();
        return layer.GetComponent<Tilemap>();
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Object.DontDestroyOnLoad(eventSystemObject);
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(root.GetChild(i).gameObject);
        }
    }

    private static GameObject CreateImage(string name, Transform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPosition)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;

        return go;
    }

    private static GameObject CreateButton(string name, Transform parent, Sprite sprite, string title, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPosition)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(go.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = title;
        label.fontSize = 40;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color32(250, 244, 224, 255);

        return go;
    }
}
