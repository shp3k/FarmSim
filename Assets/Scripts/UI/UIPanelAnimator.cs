using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIPanelAnimator : MonoBehaviour
{
    [SerializeField] [Min(0.01f)] private float duration = 0.2f;
    [SerializeField] private bool hideOnAwake = true;

    private CanvasGroup canvasGroup;
    private Coroutine animationRoutine;
    private bool isVisible;

    public bool IsVisible => isVisible;
    public bool IsAnimating => animationRoutine != null;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (hideOnAwake)
        {
            ApplyImmediate(false);
        }
    }

    public void Show()
    {
        AnimateTo(true);
    }

    public void Hide()
    {
        AnimateTo(false);
    }

    public void Toggle()
    {
        AnimateTo(!isVisible);
    }

    public void ApplyImmediate(bool visible)
    {
        canvasGroup ??= GetComponent<CanvasGroup>();
        isVisible = visible;
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        gameObject.SetActive(visible);
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void AnimateTo(bool visible)
    {
        canvasGroup ??= GetComponent<CanvasGroup>();
        isVisible = visible;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        gameObject.SetActive(true);
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        animationRoutine = StartCoroutine(AnimateRoutine(visible));
    }

    private IEnumerator AnimateRoutine(bool visible)
    {
        float startAlpha = canvasGroup.alpha;
        float endAlpha = visible ? 1f : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, 1f - Mathf.Pow(1f - t, 3f));
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        if (!visible)
        {
            gameObject.SetActive(false);
        }

        animationRoutine = null;
    }
}
