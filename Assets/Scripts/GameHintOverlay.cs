using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHintOverlay : MonoBehaviour
{
    private class QueuedHint
    {
        public string key;
        public string text;
        public float durationSeconds;
        public float delaySeconds;
    }

    private readonly Queue<QueuedHint> hintQueue = new();

    private RectTransform rootRect;
    private CanvasGroup canvasGroup;
    private TMP_Text hintText;
    private Coroutine playbackRoutine;
    private string currentKey;

    private readonly Vector2 hiddenPosition = new(0f, -92f);
    private readonly Vector2 shownPosition = new(0f, 18f);
    private const float ShowDurationSeconds = 0.32f;
    private const float HideDurationSeconds = 0.28f;
    private const float MinimumDurationSeconds = 0.5f;

    public static GameHintOverlay EnsureInCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            return null;
        }

        GameHintOverlay existing = canvas.GetComponentInChildren<GameHintOverlay>(true);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject("HintOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(GameHintOverlay));
        root.transform.SetAsLastSibling();
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(820f, 78f);
        rect.anchoredPosition = new Vector2(0f, -92f);

        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.62f);
        bg.raycastTarget = false;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;

        GameObject textObject = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 12f);
        textRect.offsetMax = new Vector2(-18f, -12f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = Color.white;
        text.raycastTarget = false;

        GameHintOverlay overlay = root.GetComponent<GameHintOverlay>();
        overlay.rootRect = rect;
        overlay.canvasGroup = group;
        overlay.hintText = text;
        overlay.HideImmediate();
        return overlay;
    }

    public void EnqueueHint(string key, string text, float durationSeconds, float delaySeconds = 0f)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        hintQueue.Enqueue(new QueuedHint
        {
            key = key,
            text = text,
            durationSeconds = Mathf.Max(MinimumDurationSeconds, durationSeconds),
            delaySeconds = Mathf.Max(0f, delaySeconds)
        });

        if (playbackRoutine == null)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            playbackRoutine = StartCoroutine(PlayQueuedHints());
        }
    }

    public void ShowHint(string key, string text)
    {
        EnqueueHint(key, text, 4f);
    }

    public void HideHint(string key)
    {
        if (!string.IsNullOrWhiteSpace(key) && string.Equals(currentKey, key, System.StringComparison.Ordinal))
        {
            StopAllHints();
        }
    }

    public void StopAllHints()
    {
        hintQueue.Clear();
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        currentKey = null;
        HideImmediate();
    }

    private IEnumerator PlayQueuedHints()
    {
        while (hintQueue.Count > 0)
        {
            QueuedHint hint = hintQueue.Dequeue();
            currentKey = hint.key;

            if (hint.delaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(hint.delaySeconds);
            }

            ShowImmediate(hint.text);
            yield return Animate(hiddenPosition, shownPosition, 0f, 1f, ShowDurationSeconds);
            yield return new WaitForSecondsRealtime(hint.durationSeconds);
            yield return Animate(shownPosition, hiddenPosition, 1f, 0f, HideDurationSeconds);
            HideImmediate();
            currentKey = null;
        }

        playbackRoutine = null;
    }

    private void ShowImmediate(string text)
    {
        if (rootRect == null || canvasGroup == null || hintText == null)
        {
            return;
        }

        hintText.text = text;
        rootRect.gameObject.SetActive(true);
        rootRect.anchoredPosition = hiddenPosition;
        canvasGroup.alpha = 0f;
    }

    private void HideImmediate()
    {
        if (hintText != null)
        {
            hintText.text = string.Empty;
        }

        if (rootRect != null)
        {
            rootRect.anchoredPosition = hiddenPosition;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private IEnumerator Animate(Vector2 fromPos, Vector2 toPos, float fromAlpha, float toAlpha, float durationSeconds)
    {
        if (rootRect == null || canvasGroup == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            rootRect.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, eased);
            canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, eased);
            yield return null;
        }

        rootRect.anchoredPosition = toPos;
        canvasGroup.alpha = toAlpha;
    }
}

