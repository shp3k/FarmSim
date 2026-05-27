using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class MoneyCounterUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform counterRoot;
    [SerializeField] private Image panelImage;
    [SerializeField] private Image coinIconImage;
    [SerializeField] private TMP_Text moneyText;

    [Header("Content")]
    [FormerlySerializedAs("labelPrefix")]
    [SerializeField] private string prefix = "";
    [SerializeField] private int editorPreviewMoney = 100;

    [Header("Sprites")]
    [FormerlySerializedAs("panelSprite")]
    [SerializeField] private Sprite panelSpriteOverride;
    [FormerlySerializedAs("coinIconSprite")]
    [SerializeField] private Sprite coinIconSpriteOverride;

    private Coroutine pulseRoutine;
#if UNITY_EDITOR
    private bool editorRefreshQueued;
#endif

    private void Reset()
    {
        AutoWireReferences();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorRefresh();
            return;
        }
#endif
        RefreshEditorView();
    }

    private void Awake()
    {
        AutoWireReferences();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorRefresh();
            return;
        }
#endif
        ApplyStyle();
    }

    private void OnEnable()
    {
        AutoWireReferences();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorRefresh();
            return;
        }
#endif
        ApplyStyle();

        if (Application.isPlaying)
        {
            GameManager.OnMoneyChanged += HandleMoneyChanged;
            RefreshFromGameManager();
        }
        else
        {
            SetMoneyText(editorPreviewMoney);
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            GameManager.OnMoneyChanged -= HandleMoneyChanged;
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            AutoWireReferences();
            ApplyStyle();
            return;
        }

#if UNITY_EDITOR
        QueueEditorRefresh();
#endif
    }

#if UNITY_EDITOR
    private void QueueEditorRefresh()
    {
        if (editorRefreshQueued)
        {
            return;
        }

        editorRefreshQueued = true;
        EditorApplication.delayCall += DelayedEditorRefresh;
    }

    private void DelayedEditorRefresh()
    {
        editorRefreshQueued = false;

        if (this == null)
        {
            return;
        }

        RefreshEditorView();
    }
#endif

    private void HandleMoneyChanged(int currentMoney)
    {
        SetMoneyText(currentMoney);
        PlayPulse();
    }

    private void AutoWireReferences()
    {
        if (counterRoot == null)
        {
            counterRoot = transform as RectTransform;
        }

        if (panelImage == null)
        {
            panelImage = GetComponent<Image>();
        }

        if (moneyText == null)
        {
            moneyText = GetComponentInChildren<TMP_Text>(true);
        }

        if (coinIconImage == null)
        {
            Transform iconTransform = transform.Find("CoinIcon");
            coinIconImage = iconTransform != null
                ? iconTransform.GetComponent<Image>()
                : GetComponentInChildren<Image>(true);
        }
    }

    private void ApplyStyle()
    {
        if (panelImage != null)
        {
            if (panelSpriteOverride != null)
            {
                panelImage.sprite = panelSpriteOverride;
            }

            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.raycastTarget = false;
        }

        if (coinIconImage != null)
        {
            if (coinIconSpriteOverride != null)
            {
                coinIconImage.sprite = coinIconSpriteOverride;
            }

            coinIconImage.enabled = coinIconImage.sprite != null;
            coinIconImage.preserveAspect = true;
            coinIconImage.raycastTarget = false;
        }

        if (moneyText != null)
        {
            moneyText.raycastTarget = false;
        }
    }

    private void RefreshEditorView()
    {
        AutoWireReferences();
        ApplyStyle();
        SetMoneyText(editorPreviewMoney);
    }

    private void RefreshFromGameManager()
    {
        SetMoneyText(GameManager.Instance != null ? GameManager.Instance.money : editorPreviewMoney);
    }

    private void SetMoneyText(int value)
    {
        if (moneyText == null)
        {
            return;
        }

        string formattedValue = value.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");
        moneyText.text = string.IsNullOrWhiteSpace(prefix)
            ? formattedValue
            : $"{prefix} {formattedValue}";
    }

    private void PlayPulse()
    {
        if (!Application.isPlaying || counterRoot == null)
        {
            return;
        }

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
        }

        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private System.Collections.IEnumerator PulseRoutine()
    {
        counterRoot.localScale = Vector3.one * 1.08f;
        yield return new WaitForSeconds(0.08f);
        counterRoot.localScale = Vector3.one;
        pulseRoutine = null;
    }
}
