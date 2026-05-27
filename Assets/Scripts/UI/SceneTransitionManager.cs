using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [SerializeField] [Min(0.01f)] private float fadeOutSeconds = 0.35f;
    [SerializeField] [Min(0.01f)] private float fadeInSeconds = 0.85f;
    [SerializeField] [Min(0)] private int settleFramesAfterLoad = 3;
    [SerializeField] private Color fadeColor = Color.black;

    private Canvas canvas;
    private GraphicRaycaster graphicRaycaster;
    private Image fadeImage;
    private Coroutine transitionRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureCreated()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject host = new("SceneTransitionManager");
        host.AddComponent<SceneTransitionManager>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlay();
            SetAlpha(0f);
            SetOverlayVisible(false);
            return;
        }

        if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public static void LoadScene(string sceneName)
    {
        LoadScene(sceneName, false);
    }

    public static void LoadSceneFromBlack(string sceneName)
    {
        LoadScene(sceneName, true);
    }

    private static void LoadScene(string sceneName, bool alreadyBlack)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        EnsureCreated();
        Instance.LoadSceneWithTransition(sceneName, alreadyBlack);
    }

    private void LoadSceneWithTransition(string sceneName, bool alreadyBlack)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        transitionRoutine = StartCoroutine(TransitionRoutine(sceneName, alreadyBlack));
    }

    private IEnumerator TransitionRoutine(string sceneName, bool alreadyBlack)
    {
        BuildOverlay();
        SetOverlayVisible(true);

        if (alreadyBlack)
        {
            SetAlpha(1f);
        }
        else
        {
            yield return Fade(0f, 1f, fadeOutSeconds);
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation != null)
        {
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                yield return null;
            }

            SetAlpha(1f);
            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
                yield return null;
            }
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }

        ClearSelectedUiObject();
        for (int i = 0; i < settleFramesAfterLoad; i++)
        {
            yield return null;
        }

        yield return Fade(1f, 0f, fadeInSeconds);

        SetOverlayVisible(false);
        ClearSelectedUiObject();
        transitionRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(to);
    }

    private void BuildOverlay()
    {
        if (canvas != null && fadeImage != null)
        {
            return;
        }

        GameObject canvasObject = new("SceneTransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        graphicRaycaster = canvasObject.GetComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject fadeObject = new("Fade", typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(canvasObject.transform, false);
        RectTransform fadeRect = fadeObject.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.raycastTarget = false;
        fadeImage.color = fadeColor;
    }

    private void SetAlpha(float alpha)
    {
        if (fadeImage == null)
        {
            return;
        }

        Color color = fadeColor;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
        fadeImage.raycastTarget = canvas != null && canvas.gameObject.activeSelf && color.a > 0.001f;
    }

    private void SetOverlayVisible(bool visible)
    {
        if (canvas != null)
        {
            canvas.gameObject.SetActive(visible);
        }

        if (graphicRaycaster != null)
        {
            graphicRaycaster.enabled = visible;
        }

        if (!visible && fadeImage != null)
        {
            fadeImage.raycastTarget = false;
        }
    }

    private static void ClearSelectedUiObject()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
