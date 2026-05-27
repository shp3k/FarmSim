using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class CatalogRuntimeFallbackFactory
{
    private static readonly Dictionary<string, GameObject> PlantTemplates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, GameObject> AnimalTemplates = new(StringComparer.OrdinalIgnoreCase);
    private static Sprite fallbackSprite;

    public static GameObject GetOrCreatePlantTemplate(ShopManager.CropShopEntry crop)
    {
        if (crop == null || string.IsNullOrWhiteSpace(crop.cropId))
        {
            return null;
        }

        if (PlantTemplates.TryGetValue(crop.cropId, out GameObject existing) && existing != null)
        {
            return existing;
        }

        GameObject template = new GameObject($"RuntimeFallbackPlant_{crop.cropId}");
        template.SetActive(false);
        template.hideFlags = HideFlags.HideAndDontSave;

        CropGrowth growth = template.AddComponent<CropGrowth>();
        CropVisualController visuals = template.AddComponent<CropVisualController>();

        GameObject planted = CreatePlantStage(template.transform, "Stage_0_Planted", new Color(0.45f, 0.72f, 0.30f), new Vector2(0.32f, 0.18f));
        GameObject growing = CreatePlantStage(template.transform, "Stage_1_Growing", new Color(0.35f, 0.82f, 0.28f), new Vector2(0.42f, 0.34f));
        GameObject grown = CreatePlantStage(template.transform, "Stage_2_Grown", new Color(0.96f, 0.78f, 0.24f), new Vector2(0.48f, 0.52f));

        visuals.ConfigureStages(planted, growing, grown);
        visuals.SetStage(0);
        growth.ConfigureFromCatalog(crop.cropId, crop.harvestItemId, crop.growthSeconds);

        PlantTemplates[crop.cropId] = template;
        return template;
    }

    public static GameObject GetOrCreateAnimalTemplate(ShopManager.AnimalShopEntry animal)
    {
        if (animal == null || string.IsNullOrWhiteSpace(animal.animalId))
        {
            return null;
        }

        if (AnimalTemplates.TryGetValue(animal.animalId, out GameObject existing) && existing != null)
        {
            return existing;
        }

        GameObject template = new GameObject($"RuntimeFallbackAnimal_{animal.animalId}");
        template.SetActive(false);
        template.hideFlags = HideFlags.HideAndDontSave;

        SpriteRenderer renderer = template.AddComponent<SpriteRenderer>();
        renderer.sprite = GetFallbackSprite();
        renderer.color = new Color(0.92f, 0.86f, 0.68f);
        renderer.sortingOrder = 120;
        template.transform.localScale = new Vector3(0.6f, 0.45f, 1f);

        SortingGroup sortingGroup = template.AddComponent<SortingGroup>();
        sortingGroup.sortingOrder = 120;

        AnimalTemplates[animal.animalId] = template;
        return template;
    }

    private static GameObject CreatePlantStage(Transform parent, string name, Color color, Vector2 scale)
    {
        GameObject stage = new GameObject(name);
        stage.transform.SetParent(parent, false);
        stage.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        SpriteRenderer renderer = stage.AddComponent<SpriteRenderer>();
        renderer.sprite = GetFallbackSprite();
        renderer.color = color;
        renderer.sortingOrder = 100;

        return stage;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            1f);
        fallbackSprite.hideFlags = HideFlags.HideAndDontSave;
        return fallbackSprite;
    }
}
