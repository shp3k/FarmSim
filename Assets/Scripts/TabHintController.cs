using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TabHintController : MonoBehaviour
{
    private const string DefaultHintText = "\u041d\u0430\u0436\u043c\u0438\u0442\u0435 TAB, \u0447\u0442\u043e\u0431\u044b \u043e\u0442\u043a\u0440\u044b\u0442\u044c \u043c\u0430\u0433\u0430\u0437\u0438\u043d \u043a\u0443\u043b\u044c\u0442\u0443\u0440 \u0438 \u0436\u0438\u0432\u043e\u0442\u043d\u044b\u0445";

    [Header("Hint Content")]
    [SerializeField] private string hintTextValue = DefaultHintText;

    [Header("Hint Motion")]
    [SerializeField] [Min(0.05f)] private float showDuration = 0.35f;
    [SerializeField] [Min(0.05f)] private float hideDuration = 0.28f;
    [SerializeField] private Vector2 hiddenAnchoredPosition = new(0f, -150f);
    [SerializeField] private Vector2 shownAnchoredPosition = new(0f, 104f);
    [SerializeField] private AnimationCurve showCurve = null;
    [SerializeField] private AnimationCurve hideCurve = null;

    private Canvas canvas;
    private RectTransform rootRect;
    private CanvasGroup canvasGroup;
    private TMP_Text hintText;
    private Coroutine animationRoutine;
    private bool isDismissed;

    public bool IsDismissed => isDismissed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachToGameplayScene()
    {
        EnsureInScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInScene(scene);
    }

    private static void EnsureInScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "MainScene")
        {
            return;
        }

        if (FindFirstObjectByType<TabHintController>() != null)
        {
            return;
        }

        GameObject host = new GameObject("TabHintController");
        host.AddComponent<TabHintController>();
    }

    private void Awake()
    {
        if (showCurve == null || showCurve.length == 0)
        {
            showCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (hideCurve == null || hideCurve.length == 0)
        {
            hideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        canvas = UIInputUtility.FindSceneCanvas();
        BuildHintUI();
        ApplySeenStateFromSave();
    }

    private void OnEnable()
    {
        SideShopPanelController.OnPanelOpenedByTab += HandlePanelOpenedByTab;
    }

    private void Start()
    {
        if (isDismissed)
        {
            ApplyHiddenImmediate();
            return;
        }

        PlayShowAnimation();
    }

    private void OnDisable()
    {
        SideShopPanelController.OnPanelOpenedByTab -= HandlePanelOpenedByTab;
    }

    private void BuildHintUI()
    {
        if (canvas == null)
        {
            return;
        }

        GameObject root = new GameObject("TabHint", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.sizeDelta = new Vector2(820f, 76f);
        rootRect.anchoredPosition = hiddenAnchoredPosition;

        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.62f);
        bg.raycastTarget = false;

        canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;

        GameObject textObject = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rootRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 12f);
        textRect.offsetMax = new Vector2(-18f, -12f);

        hintText = textObject.GetComponent<TextMeshProUGUI>();
        hintText.fontSize = 24f;
        hintText.color = Color.white;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.textWrappingMode = TextWrappingModes.Normal;
        hintText.text = hintTextValue;
        hintText.raycastTarget = false;
    }

    private void ApplySeenStateFromSave()
    {
        isDismissed = TutorialHintsSave.TabHintSeen;
    }

    private void HandlePanelOpenedByTab()
    {
        if (isDismissed)
        {
            return;
        }

        isDismissed = true;
        TutorialHintsSave.MarkTabHintSeen();
        PlayHideAnimation();
    }

    public void ResetHintForTesting()
    {
        isDismissed = false;
        PlayShowAnimation();
    }

    private void PlayShowAnimation()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(AnimateHint(hiddenAnchoredPosition, shownAnchoredPosition, 0f, 1f, showDuration, showCurve));
    }

    private void PlayHideAnimation()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(AnimateHint(rootRect.anchoredPosition, hiddenAnchoredPosition, canvasGroup.alpha, 0f, hideDuration, hideCurve, disableAtEnd: true));
    }

    private void ApplyHiddenImmediate()
    {
        if (rootRect == null || canvasGroup == null)
        {
            return;
        }

        rootRect.anchoredPosition = hiddenAnchoredPosition;
        canvasGroup.alpha = 0f;
        if (rootRect.gameObject.activeSelf)
        {
            rootRect.gameObject.SetActive(false);
        }
    }

    private IEnumerator AnimateHint(
        Vector2 fromPos,
        Vector2 toPos,
        float fromAlpha,
        float toAlpha,
        float duration,
        AnimationCurve curve,
        bool disableAtEnd = false)
    {
        if (rootRect == null || canvasGroup == null)
        {
            yield break;
        }

        if (!rootRect.gameObject.activeSelf)
        {
            rootRect.gameObject.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curved = curve.Evaluate(t);
            rootRect.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, curved);
            canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, curved);
            yield return null;
        }

        rootRect.anchoredPosition = toPos;
        canvasGroup.alpha = toAlpha;

        if (disableAtEnd && rootRect.gameObject.activeSelf)
        {
            rootRect.gameObject.SetActive(false);
        }

        animationRoutine = null;
    }
}

