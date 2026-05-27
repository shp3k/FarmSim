using UnityEngine;
using UnityEngine.Serialization;

public class CropVisualController : MonoBehaviour
{
    private const int LastStageIndex = 2;

    [Header("Stage References")]
    [SerializeField] private GameObject stage0Planted;
    [SerializeField] private GameObject stage1Growing;
    [SerializeField] private GameObject stage2Grown;

    [Header("Ready State")]
    [SerializeField] private GameObject readyIndicator;

    private void Awake()
    {
        AutoAssignStageReferencesIfMissing();
    }

    public void SetStage(int stageIndex)
    {
        AutoAssignStageReferencesIfMissing();

        int clamped = Mathf.Clamp(stageIndex, 0, LastStageIndex);
        SetActiveSafe(stage0Planted, clamped == 0);
        SetActiveSafe(stage1Growing, clamped == 1);
        SetActiveSafe(stage2Grown, clamped == 2);
    }

    public void SetReadyToHarvest(bool isReady)
    {
        // Ready markers are handled by ReadyMarkerController with an inspector-assigned prefab.
        // This legacy indicator reference is kept only so old prefab fields stay harmless.
        SetActiveSafe(readyIndicator, false);
    }

    public void TryInitializeFromLegacy(GameObject[] legacyStageModels)
    {
        if (legacyStageModels == null || legacyStageModels.Length < 3)
        {
            return;
        }

        if (stage0Planted == null)
        {
            stage0Planted = legacyStageModels[0];
        }

        if (stage1Growing == null)
        {
            stage1Growing = legacyStageModels[1];
        }

        if (stage2Grown == null)
        {
            stage2Grown = legacyStageModels[2];
        }

        AutoAssignStageReferencesIfMissing();
    }

    public void ConfigureStages(GameObject planted, GameObject growing, GameObject grown)
    {
        stage0Planted = planted;
        stage1Growing = growing;
        stage2Grown = grown;
        AutoAssignStageReferencesIfMissing();
    }

    public void ConfigureReadyIndicator(GameObject indicator)
    {
        readyIndicator = indicator;
        SetReadyToHarvest(false);
    }

    private void AutoAssignStageReferencesIfMissing()
    {
        if (stage0Planted == null)
        {
            stage0Planted = FindStageChild(0);
        }

        if (stage1Growing == null)
        {
            stage1Growing = FindStageChild(1);
        }

        if (stage2Grown == null)
        {
            stage2Grown = FindStageChild(2);
        }
    }

    private GameObject FindStageChild(int stageIndex)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
            {
                continue;
            }

            if (IsStageName(child.name, stageIndex))
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static bool IsStageName(string objectName, int stageIndex)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        string normalized = objectName.Trim().ToLowerInvariant();
        return normalized == $"stage_{stageIndex}_planted" ||
               normalized == $"stage_{stageIndex}_growing" ||
               normalized == $"stage_{stageIndex}_grown" ||
               normalized.Contains($"stage_{stageIndex}") ||
               normalized.Contains($"stage{stageIndex}");
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

}
