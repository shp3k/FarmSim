using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FarmUIManager : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Update()
    {
        if (WasEscapePressedThisFrame())
        {
            if (CloseOpenPanel())
            {
                return;
            }

            ExitToMainMenu();
        }
    }

    public void ExitToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning("Main menu scene name is empty.");
            return;
        }

        SaveManager.Instance?.SaveGame();
        SceneTransitionManager.LoadScene(mainMenuSceneName);
    }

    public void RefreshUI()
    {
        // MoneyCounterUI and SideShopPanelController refresh themselves.
    }

    public void SetStatusMessage(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Debug.Log(value);
        }
    }

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static bool CloseOpenPanel()
    {
        if (PrestigePanelController.Instance != null && PrestigePanelController.Instance.IsOpen)
        {
            PrestigePanelController.Instance.Close();
            return true;
        }

        if (FarmEncyclopediaController.Instance != null && FarmEncyclopediaController.Instance.IsOpen)
        {
            FarmEncyclopediaController.Instance.Close();
            return true;
        }

        if (SideShopPanelController.Instance != null && SideShopPanelController.Instance.IsOpen)
        {
            SideShopPanelController.Instance.ClosePanel();
            return true;
        }

        return false;
    }
}
