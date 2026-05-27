using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps gameplay framing and UI scaling consistent across devices.
/// </summary>
public class DisplayAdaptationManager : MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
    private const float TargetAspect = 16f / 9f;
    private const float MatchWidthOrHeight = 0.5f;

    private int cachedScreenWidth;
    private int cachedScreenHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<DisplayAdaptationManager>() != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("DisplayAdaptationManager");
        DontDestroyOnLoad(managerObject);
        managerObject.AddComponent<DisplayAdaptationManager>();
    }

    private void Awake()
    {
        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;

        ApplyAll();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (cachedScreenWidth == Screen.width && cachedScreenHeight == Screen.height)
        {
            return;
        }

        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;
        ApplyAll();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAll();
    }

    private static void ApplyAll()
    {
        NormalizeScreenSpaceCanvasRects();
        ConfigureCanvasScalers();
        ApplyAspectToCameras();
    }

    private static void ConfigureCanvasScalers()
    {
        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (CanvasScaler scaler in scalers)
        {
            if (scaler == null)
            {
                continue;
            }

            Canvas canvas = scaler.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
        }
    }

    private static void NormalizeScreenSpaceCanvasRects()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas.isRootCanvas == false)
            {
                continue;
            }

            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            RectTransform rect = canvas.GetComponent<RectTransform>();
            if (rect == null)
            {
                continue;
            }

            // Some prototype scenes may have malformed root-Canvas transform values.
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }

    private static void ApplyAspectToCameras()
    {
        Camera[] cameras = Camera.allCameras;
        float screenAspect = (float)Screen.width / Screen.height;
        Rect targetRect;

        if (screenAspect < TargetAspect)
        {
            float viewportHeight = screenAspect / TargetAspect;
            targetRect = new Rect(0f, (1f - viewportHeight) * 0.5f, 1f, viewportHeight);
        }
        else
        {
            float viewportWidth = TargetAspect / screenAspect;
            targetRect = new Rect((1f - viewportWidth) * 0.5f, 0f, viewportWidth, 1f);
        }

        foreach (Camera cameraComponent in cameras)
        {
            if (cameraComponent == null || cameraComponent.targetTexture != null)
            {
                continue;
            }

            // Keep projection stable only for gameplay camera.
            if (!cameraComponent.CompareTag("MainCamera"))
            {
                cameraComponent.rect = new Rect(0f, 0f, 1f, 1f);
                continue;
            }

            cameraComponent.rect = targetRect;
        }
    }
}
