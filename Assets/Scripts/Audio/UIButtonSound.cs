using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerClickHandler
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button == null || !button.IsActive() || !button.interactable)
        {
            return;
        }

        AudioManager.Instance?.PlayClick();
    }
}
