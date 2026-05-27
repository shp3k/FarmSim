using UnityEngine;
using UnityEngine.Rendering;

public class ReadyMarkerController : MonoBehaviour
{
    [Header("Optional Override")]
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private bool overrideMarkerOffset;
    [SerializeField] private Vector3 markerOffset = new(0f, 1f, 0f);

    private GameObject markerInstance;
    private Vector3 markerPrefabScale = Vector3.one;
    private bool warnedMissingPrefab;
    private bool wantsMarkerVisible;
    private ReadyMarkerKind lastMarkerKind;

    private void Update()
    {
        if (!wantsMarkerVisible || markerInstance != null)
        {
            return;
        }

        Show(lastMarkerKind);
    }

    public void SetReady(bool isReady, ReadyMarkerKind kind)
    {
        wantsMarkerVisible = isReady;
        lastMarkerKind = kind;

        if (isReady)
        {
            Show(kind);
        }
        else
        {
            Hide();
        }
    }

    private void Show(ReadyMarkerKind kind)
    {
        GameObject prefab = ResolveMarkerPrefab(kind);
        if (prefab == null)
        {
            WarnMissingPrefab(kind);
            return;
        }

        if (markerInstance == null)
        {
            markerInstance = Instantiate(prefab, transform);
            markerInstance.name = $"{prefab.name}_ReadyMarker";
            markerPrefabScale = prefab.transform.localScale;
            warnedMissingPrefab = false;
        }

        markerInstance.transform.localPosition = ResolveMarkerLocalPosition(kind);
        markerInstance.transform.localRotation = Quaternion.identity;
        PreserveMarkerWorldScale();
        MoveMarkerRenderersAboveTarget();
        markerInstance.SetActive(true);
    }

    private void Hide()
    {
        if (markerInstance != null)
        {
            markerInstance.SetActive(false);
        }
    }

    private GameObject ResolveMarkerPrefab(ReadyMarkerKind kind)
    {
        if (markerPrefab != null)
        {
            return markerPrefab;
        }

        ReadyMarkerManager manager = ReadyMarkerManager.Instance ?? FindFirstObjectByType<ReadyMarkerManager>();
        return manager != null ? manager.GetMarkerPrefab(kind) : null;
    }

    private Vector3 ResolveMarkerLocalPosition(ReadyMarkerKind kind)
    {
        if (overrideMarkerOffset)
        {
            return markerOffset;
        }

        ReadyMarkerManager manager = ReadyMarkerManager.Instance ?? FindFirstObjectByType<ReadyMarkerManager>();
        Vector3 offset = manager != null ? manager.GetMarkerOffset(kind) : markerOffset;
        return TryGetVisualTopLocalPosition(out Vector3 visualTopLocalPosition)
            ? visualTopLocalPosition + offset
            : offset;
    }

    private bool TryGetVisualTopLocalPosition(out Vector3 localPosition)
    {
        localPosition = default;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Bounds combinedBounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer candidate = renderers[i];
            if (candidate == null || IsMarkerRenderer(candidate))
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = candidate.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(candidate.bounds);
        }

        if (!hasBounds)
        {
            return false;
        }

        Vector3 worldTop = new(combinedBounds.center.x, combinedBounds.max.y, transform.position.z);
        localPosition = transform.InverseTransformPoint(worldTop);
        localPosition.z = 0f;
        return true;
    }

    private bool IsMarkerRenderer(Renderer candidate)
    {
        return markerInstance != null && candidate.transform.IsChildOf(markerInstance.transform);
    }

    private void PreserveMarkerWorldScale()
    {
        if (markerInstance == null)
        {
            return;
        }

        Vector3 parentScale = transform.lossyScale;
        markerInstance.transform.localScale = new Vector3(
            DivideScale(markerPrefabScale.x, parentScale.x),
            DivideScale(markerPrefabScale.y, parentScale.y),
            DivideScale(markerPrefabScale.z, parentScale.z));
    }

    private static float DivideScale(float value, float parentScale)
    {
        return Mathf.Approximately(parentScale, 0f) ? value : value / parentScale;
    }

    private void MoveMarkerRenderersAboveTarget()
    {
        if (markerInstance == null)
        {
            return;
        }

        int topSortingOrder = 0;
        int sortingLayerId = SortingLayer.NameToID("Default");
        bool hasTargetRenderer = false;

        SortingGroup[] targetGroups = GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < targetGroups.Length; i++)
        {
            SortingGroup targetGroup = targetGroups[i];
            if (targetGroup == null || targetGroup.transform.IsChildOf(markerInstance.transform))
            {
                continue;
            }

            if (!hasTargetRenderer || targetGroup.sortingOrder > topSortingOrder)
            {
                topSortingOrder = targetGroup.sortingOrder;
                sortingLayerId = targetGroup.sortingLayerID;
                hasTargetRenderer = true;
            }
        }

        Renderer[] targetRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null || IsMarkerRenderer(targetRenderer))
            {
                continue;
            }

            if (!hasTargetRenderer || targetRenderer.sortingOrder > topSortingOrder)
            {
                topSortingOrder = targetRenderer.sortingOrder;
                sortingLayerId = targetRenderer.sortingLayerID;
                hasTargetRenderer = true;
            }
        }

        int markerSortingOrder = hasTargetRenderer ? topSortingOrder + 1000 : 1000;
        SortingGroup markerGroup = markerInstance.GetComponent<SortingGroup>();
        if (markerGroup == null)
        {
            markerGroup = markerInstance.AddComponent<SortingGroup>();
        }

        markerGroup.sortingLayerID = sortingLayerId;
        markerGroup.sortingOrder = markerSortingOrder;

        Renderer[] markerRenderers = markerInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < markerRenderers.Length; i++)
        {
            Renderer markerRenderer = markerRenderers[i];
            if (markerRenderer == null)
            {
                continue;
            }

            if (hasTargetRenderer)
            {
                markerRenderer.sortingLayerID = sortingLayerId;
                markerRenderer.sortingOrder = markerSortingOrder;
            }
            else
            {
                markerRenderer.sortingOrder = Mathf.Max(markerRenderer.sortingOrder, 1000);
            }
        }
    }

    private void WarnMissingPrefab(ReadyMarkerKind kind)
    {
        if (warnedMissingPrefab)
        {
            return;
        }

        warnedMissingPrefab = true;
        Debug.LogWarning(
            $"ReadyMarkerController on '{name}': marker prefab is not assigned for {kind}. " +
            "Assign Ready Marker Prefab on ReadyMarkerManager or this component.");
    }
}
