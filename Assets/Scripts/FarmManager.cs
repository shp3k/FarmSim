using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FarmManager : MonoBehaviour
{
    public static FarmManager Instance;

    private const string NoFreePlotsMessage = "\u041d\u0435\u0442 \u0441\u0432\u043e\u0431\u043e\u0434\u043d\u044b\u0445 \u0433\u0440\u044f\u0434\u043e\u043a";

    [Header("Tilemap Plot Points")]
    [SerializeField] private Transform[] plotPoints = Array.Empty<Transform>();

    [Header("Legacy Farm Plots")]
    [SerializeField] [HideInInspector] private List<FarmPlot> farmPlots = new();

    private readonly List<FarmPlot> resolvedPlots = new();
    private bool warnedMissingPlotPoints;

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

        RebuildFarmPlotsIfNeeded();
    }

    public int GetFreePlotsCount()
    {
        int freeCount = 0;

        RebuildFarmPlotsIfNeeded();

        foreach (FarmPlot plot in resolvedPlots)
        {
            if (plot != null && !plot.IsOccupied)
            {
                freeCount++;
            }
        }

        return freeCount;
    }

    public bool HasFreePlots()
    {
        return GetFreePlotsCount() > 0;
    }

    public bool TryPlantInFreePlot(string seedItemId, GameObject plantPrefab)
    {
        if (!HasFreePlots())
        {
            Debug.Log(NoFreePlotsMessage);
            return false;
        }

        FarmPlot freePlot = GetFirstFreePlot();
        if (freePlot == null)
        {
            Debug.Log(NoFreePlotsMessage);
            return false;
        }

        return freePlot.Plant(seedItemId, plantPrefab);
    }

    public bool TryPlantInPriorityPlot(
        string seedItemId,
        GameObject plantPrefab,
        string cropId,
        string harvestItemId,
        float growthSeconds,
        out string resultMessage)
    {
        RebuildFarmPlotsIfNeeded();

        if (string.IsNullOrWhiteSpace(seedItemId))
        {
            resultMessage = "Seed item id is empty.";
            return false;
        }

        if (plantPrefab == null)
        {
            resultMessage = "Plant prefab is missing.";
            return false;
        }

        FarmPlot freePlot = GetFirstFreePlotByPriority();
        if (freePlot == null)
        {
            resultMessage = NoFreePlotsMessage;
            Debug.Log(NoFreePlotsMessage);
            return false;
        }

        bool planted = freePlot.PlantFromShop(seedItemId, plantPrefab, cropId, harvestItemId, growthSeconds);
        resultMessage = planted
            ? $"Planted on {freePlot.PlotId}."
            : $"Failed to plant on {freePlot.PlotId}.";
        return planted;
    }

    public List<FarmPlot> GetAllPlots()
    {
        RebuildFarmPlotsIfNeeded();
        return resolvedPlots.Where(plot => plot != null).ToList();
    }

    public void ClearAllPlots()
    {
        RebuildFarmPlotsIfNeeded();
        foreach (FarmPlot plot in resolvedPlots)
        {
            if (plot != null)
            {
                plot.ClearPlot(suppressSave: true);
            }
        }
    }

    private FarmPlot GetFirstFreePlot()
    {
        RebuildFarmPlotsIfNeeded();

        foreach (FarmPlot plot in resolvedPlots)
        {
            if (plot != null && !plot.IsOccupied)
            {
                return plot;
            }
        }

        return null;
    }

    private FarmPlot GetFirstFreePlotByPriority()
    {
        RebuildFarmPlotsIfNeeded();

        List<FarmPlot> orderedPlots = HasAssignedPlotPoints()
            ? resolvedPlots.Where(plot => plot != null).ToList()
            : resolvedPlots
                .Where(plot => plot != null)
                .OrderBy(plot => plot.PlotIndex)
                .ThenBy(plot => plot.PlotId)
                .ToList();

        foreach (FarmPlot plot in orderedPlots)
        {
            if (!plot.IsOccupied)
            {
                return plot;
            }
        }

        return null;
    }

    private void RebuildFarmPlotsIfNeeded()
    {
        resolvedPlots.Clear();

        if (HasAssignedPlotPoints())
        {
            for (int i = 0; i < plotPoints.Length; i++)
            {
                Transform pointTransform = plotPoints[i];
                if (pointTransform == null)
                {
                    continue;
                }

                FarmPlot plot = pointTransform.GetComponent<FarmPlot>();
                if (plot == null)
                {
                    plot = pointTransform.gameObject.AddComponent<PlotPoint>();
                }

                resolvedPlots.Add(plot);
            }

            farmPlots = resolvedPlots.ToList();
            return;
        }

        WarnMissingPlotPoints();

        if (farmPlots != null)
        {
            resolvedPlots.AddRange(farmPlots.Where(plot => plot != null));
        }

        if (resolvedPlots.Count > 0)
        {
            return;
        }

        resolvedPlots.AddRange(FindObjectsByType<FarmPlot>(FindObjectsSortMode.None)
            .OrderBy(plot => plot.PlotIndex)
            .ThenBy(plot => plot.PlotId));

        farmPlots = resolvedPlots.ToList();
    }

    private bool HasAssignedPlotPoints()
    {
        return plotPoints != null && plotPoints.Any(point => point != null);
    }

    private void WarnMissingPlotPoints()
    {
        if (warnedMissingPlotPoints)
        {
            return;
        }

        Debug.LogWarning(
            "FarmManager: plotPoints are not assigned. " +
            "Create empty PlotPoint objects above Tilemap beds and assign them to FarmManager.plotPoints. " +
            "Using legacy FarmPlot objects as fallback if any exist.");
        warnedMissingPlotPoints = true;
    }
}
