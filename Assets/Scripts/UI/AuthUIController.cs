using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AuthUIController : MonoBehaviour
{
    private enum RestoreSessionState
    {
        NotSignedIn,
        Ready,
        CloudFailed
    }

    private readonly struct RestoreSessionResult
    {
        public readonly RestoreSessionState State;
        public readonly string ErrorMessage;

        private RestoreSessionResult(RestoreSessionState state, string errorMessage)
        {
            State = state;
            ErrorMessage = errorMessage;
        }

        public static RestoreSessionResult NotSignedIn()
        {
            return new RestoreSessionResult(RestoreSessionState.NotSignedIn, string.Empty);
        }

        public static RestoreSessionResult Ready()
        {
            return new RestoreSessionResult(RestoreSessionState.Ready, string.Empty);
        }

        public static RestoreSessionResult CloudFailed(string errorMessage)
        {
            return new RestoreSessionResult(RestoreSessionState.CloudFailed, errorMessage);
        }
    }

    [Header("Scene Names")]
    [SerializeField] private string menuSceneName = "MainMenu";

    [Header("Style")]
    [SerializeField] private Sprite backgroundSprite;

    [Header("Startup Splash")]
    [SerializeField] private bool showStartupSplash = true;
    [SerializeField] private Sprite splashSprite;
    [SerializeField] private string splashPrompt = "\u041d\u0430\u0436\u043c\u0438\u0442\u0435 \u043b\u044e\u0431\u0443\u044e \u043a\u043d\u043e\u043f\u043a\u0443 \u0447\u0442\u043e\u0431\u044b \u043f\u0440\u043e\u0434\u043e\u043b\u0436\u0438\u0442\u044c...";
    [SerializeField] private float splashFadeDuration = 0.75f;
    [SerializeField] private float splashExitFadeDuration = 0.25f;
    [SerializeField] private float splashPromptDelay = 0.2f;

    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;

    [Header("Login Fields")]
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("Register Fields")]
    [SerializeField] private TMP_InputField registerEmailInput;
    [SerializeField] private TMP_InputField registerPasswordInput;
    [SerializeField] private TMP_InputField confirmPasswordInput;
    [SerializeField] private TMP_InputField displayNameInput;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Buttons")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button guestButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Button switchToRegisterButton;
    [SerializeField] private Button switchToLoginButton;

    private FirebaseAuthService authService;
    private FirebaseSaveService saveService;
    private bool startupSplashLeftBlack;

    private async void Awake()
    {
        EnsureServices();
        EnsureUi();
        HookButtons();
        SetAuthUiVisible(false);
        Task<RestoreSessionResult> restoreSessionTask = CheckRestoredSession();
        await PlayStartupSplash(restoreSessionTask);
        await FinishRestoreSession(restoreSessionTask);
    }

    private void OnDestroy()
    {
        if (loginButton != null) loginButton.onClick.RemoveListener(HandleLoginClicked);
        if (guestButton != null) guestButton.onClick.RemoveListener(HandleGuestClicked);
        if (registerButton != null) registerButton.onClick.RemoveListener(HandleRegisterClicked);
        if (switchToRegisterButton != null) switchToRegisterButton.onClick.RemoveListener(ShowRegister);
        if (switchToLoginButton != null) switchToLoginButton.onClick.RemoveListener(ShowLogin);
    }

    private void Update()
    {
        if (WasEscapePressed())
        {
#if UNITY_EDITOR
            Debug.Log("Escape pressed on AuthScene. Application.Quit is ignored in the Unity Editor.");
#else
            Application.Quit();
#endif
        }
    }

    public void ShowLogin()
    {
        SetAuthUiVisible(true);
        SetPanelState(true);
        SetStatus(string.Empty);
    }

    public void ShowRegister()
    {
        SetAuthUiVisible(true);
        SetPanelState(false);
        SetStatus(string.Empty);
    }

    private async Task<RestoreSessionResult> CheckRestoredSession()
    {
        await authService.InitializeAuth();
        if (authService.IsGuestSession)
        {
            return RestoreSessionResult.Ready();
        }

        if (!authService.IsSignedIn())
        {
            return RestoreSessionResult.NotSignedIn();
        }

        if (await PrepareSignedInCloudSession())
        {
            return RestoreSessionResult.Ready();
        }

        return RestoreSessionResult.CloudFailed(GetCloudAccessErrorMessage());
    }

    private async Task FinishRestoreSession(Task<RestoreSessionResult> restoreSessionTask)
    {
        if (!restoreSessionTask.IsCompleted)
        {
            SetAuthUiVisible(true);
            SetPanelState(true);
            SetBusy(true, "\u041f\u0440\u043e\u0432\u0435\u0440\u043a\u0430 \u0441\u0435\u0441\u0441\u0438\u0438...");
        }

        RestoreSessionResult result = await restoreSessionTask;
        switch (result.State)
        {
            case RestoreSessionState.Ready:
                SetAuthUiVisible(false);
                if (startupSplashLeftBlack)
                {
                    SceneTransitionManager.LoadSceneFromBlack(menuSceneName);
                }
                else
                {
                    SceneTransitionManager.LoadScene(menuSceneName);
                }
                return;
            case RestoreSessionState.CloudFailed:
                ShowLogin();
                authService.Logout();
                SetBusy(false, result.ErrorMessage);
                return;
            default:
                SetBusy(false, string.Empty);
                ShowLogin();
                return;
        }
    }

    private async void HandleLoginClicked()
    {
        SetBusy(true, "\u0412\u0445\u043e\u0434...");
        AuthResultData result = await authService.LoginWithEmailPassword(emailInput.text, passwordInput.text);
        if (!result.Success)
        {
            SetBusy(false, result.Message);
            return;
        }

        if (!await PrepareSignedInCloudSession())
        {
            authService.Logout();
            SetBusy(false, GetCloudAccessErrorMessage());
            return;
        }

        SetStatus("\u0412\u0445\u043e\u0434 \u0432\u044b\u043f\u043e\u043b\u043d\u0435\u043d.");
        SceneTransitionManager.LoadScene(menuSceneName);
    }

    private void HandleGuestClicked()
    {
        SetBusy(true, "\u0412\u0445\u043e\u0434 \u043a\u0430\u043a \u0433\u043e\u0441\u0442\u044c...");
        AuthResultData result = authService.LoginAsGuest();
        if (!result.Success)
        {
            SetBusy(false, result.Message);
            return;
        }

        SetStatus("\u0413\u043e\u0441\u0442\u0435\u0432\u043e\u0439 \u0440\u0435\u0436\u0438\u043c \u0432\u043a\u043b\u044e\u0447\u0435\u043d.");
        SceneTransitionManager.LoadScene(menuSceneName);
    }

    private async void HandleRegisterClicked()
    {
        SetBusy(true, "\u0420\u0435\u0433\u0438\u0441\u0442\u0440\u0430\u0446\u0438\u044f...");
        AuthResultData result = await authService.RegisterWithEmailPassword(
            GetText(registerEmailInput, emailInput),
            GetText(registerPasswordInput, passwordInput),
            confirmPasswordInput != null ? confirmPasswordInput.text : string.Empty,
            displayNameInput != null ? displayNameInput.text : string.Empty);

        if (!result.Success)
        {
            SetBusy(false, result.Message);
            return;
        }

        SaveManager.Instance?.ResetLocalStateForNewAccount();
        if (!await PrepareSignedInCloudSession())
        {
            authService.Logout();
            SetBusy(false, GetCloudAccessErrorMessage());
            return;
        }

        SetStatus("\u0410\u043a\u043a\u0430\u0443\u043d\u0442 \u0441\u043e\u0437\u0434\u0430\u043d.");
        SceneTransitionManager.LoadScene(menuSceneName);
    }

    private void EnsureServices()
    {
        FirebaseInitializer initializer = FirebaseInitializer.Instance ?? FindFirstObjectByType<FirebaseInitializer>();
        if (initializer == null)
        {
            GameObject manager = new("FirebaseManager");
            manager.AddComponent<FirebaseInitializer>();
            authService = manager.AddComponent<FirebaseAuthService>();
            saveService = manager.AddComponent<FirebaseSaveService>();
            return;
        }

        authService = FirebaseAuthService.Instance ?? initializer.GetComponent<FirebaseAuthService>() ?? initializer.gameObject.AddComponent<FirebaseAuthService>();
        saveService = FirebaseSaveService.Instance ?? initializer.GetComponent<FirebaseSaveService>() ?? initializer.gameObject.AddComponent<FirebaseSaveService>();
    }

    private void HookButtons()
    {
        if (loginButton != null) loginButton.onClick.AddListener(HandleLoginClicked);
        if (guestButton != null) guestButton.onClick.AddListener(HandleGuestClicked);
        if (registerButton != null) registerButton.onClick.AddListener(HandleRegisterClicked);
        if (switchToRegisterButton != null) switchToRegisterButton.onClick.AddListener(ShowRegister);
        if (switchToLoginButton != null) switchToLoginButton.onClick.AddListener(ShowLogin);
    }

    private void SetPanelState(bool showLogin)
    {
        if (loginPanel != null) loginPanel.SetActive(showLogin);
        if (registerPanel != null) registerPanel.SetActive(!showLogin);
    }

    private void SetBusy(bool busy, string message)
    {
        if (loginButton != null) loginButton.interactable = !busy;
        if (guestButton != null) guestButton.interactable = !busy;
        if (registerButton != null) registerButton.interactable = !busy;
        if (switchToRegisterButton != null) switchToRegisterButton.interactable = !busy;
        if (switchToLoginButton != null) switchToLoginButton.interactable = !busy;
        if (loadingIndicator != null) loadingIndicator.SetActive(busy);
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private async Task<bool> PrepareSignedInCloudSession()
    {
        if (saveService == null)
        {
            return false;
        }

        if (!await saveService.InitializeDatabase())
        {
            return false;
        }

        if (!await saveService.CreateInitialUserDataIfMissing())
        {
            return false;
        }

        return await saveService.UpdateLastLoginAt();
    }

    private string GetCloudAccessErrorMessage()
    {
        if (saveService != null && !string.IsNullOrWhiteSpace(saveService.LastError))
        {
            return "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0438\u0442\u044c cloud-save. \u0412\u043e\u0439\u0434\u0438\u0442\u0435 \u0435\u0449\u0435 \u0440\u0430\u0437 \u0438\u043b\u0438 \u043f\u0440\u043e\u0432\u0435\u0440\u044c\u0442\u0435 Firebase Database Rules.";
        }

        return "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u043f\u043e\u0434\u0433\u043e\u0442\u043e\u0432\u0438\u0442\u044c cloud-save.";
    }

    private void SetAuthUiVisible(bool visible)
    {
        if (!visible && loginPanel != null)
        {
            loginPanel.SetActive(false);
        }

        if (!visible && registerPanel != null)
        {
            registerPanel.SetActive(false);
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(visible);
        }

        if (!visible && loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }
    }

    private async Task PlayStartupSplash(Task<RestoreSessionResult> restoreSessionTask)
    {
        startupSplashLeftBlack = false;
        if (!showStartupSplash)
        {
            return;
        }

        Canvas canvas = UIInputUtility.FindSceneCanvas();
        if (canvas == null)
        {
            return;
        }

        GameObject splashObject = new("StartupSplash", typeof(RectTransform), typeof(Image));
        splashObject.transform.SetParent(canvas.transform, false);
        splashObject.transform.SetAsLastSibling();

        RectTransform splashRect = splashObject.GetComponent<RectTransform>();
        splashRect.anchorMin = Vector2.zero;
        splashRect.anchorMax = Vector2.one;
        splashRect.offsetMin = Vector2.zero;
        splashRect.offsetMax = Vector2.zero;

        Image splashBackground = splashObject.GetComponent<Image>();
        splashBackground.color = Color.black;

        GameObject contentObject = new("SplashContent", typeof(RectTransform), typeof(CanvasGroup));
        contentObject.transform.SetParent(splashObject.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        CanvasGroup contentGroup = contentObject.GetComponent<CanvasGroup>();
        contentGroup.alpha = 0f;
        contentGroup.blocksRaycasts = true;
        contentGroup.interactable = true;

        GameObject imageObject = new("SplashImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(contentObject.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.08f, 0.12f);
        imageRect.anchorMax = new Vector2(0.92f, 0.88f);
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        Image splashImage = imageObject.GetComponent<Image>();
        splashImage.sprite = splashSprite != null ? splashSprite : backgroundSprite;
        splashImage.color = splashImage.sprite != null ? Color.white : new Color(0.08f, 0.13f, 0.1f, 1f);
        splashImage.preserveAspect = true;

        TextMeshProUGUI prompt = CreateLabel(contentObject.transform, splashPrompt, 28, 48f);
        prompt.gameObject.name = "SplashPrompt";
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.fontStyle = FontStyles.Bold;
        prompt.color = new Color(1f, 0.94f, 0.78f, 1f);
        RectTransform promptRect = prompt.rectTransform;
        promptRect.anchorMin = new Vector2(0.08f, 0.05f);
        promptRect.anchorMax = new Vector2(0.92f, 0.14f);
        promptRect.offsetMin = Vector2.zero;
        promptRect.offsetMax = Vector2.zero;

        await FadeCanvasGroup(contentGroup, 0f, 1f, splashFadeDuration);
        await WaitSeconds(splashPromptDelay);
        await WaitForAnyPress();
        await FadeCanvasGroup(contentGroup, 1f, 0f, splashExitFadeDuration);

        if (restoreSessionTask.IsCompleted && restoreSessionTask.Result.State == RestoreSessionState.Ready)
        {
            startupSplashLeftBlack = true;
            return;
        }

        if (splashObject != null)
        {
            splashObject.SetActive(false);
        }
    }

    private static async Task FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
        {
            return;
        }

        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < duration && group != null)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            await Task.Yield();
        }

        if (group != null)
        {
            group.alpha = to;
        }
    }

    private static async Task WaitSeconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            await Task.Yield();
        }
    }

    private static async Task WaitForAnyPress()
    {
        while (!WasAnyPressStarted())
        {
            await Task.Yield();
        }
    }

    private static bool WasAnyPressStarted()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null
            && (Mouse.current.leftButton.wasPressedThisFrame
                || Mouse.current.rightButton.wasPressedThisFrame
                || Mouse.current.middleButton.wasPressedThisFrame))
        {
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null)
        {
            foreach (UnityEngine.InputSystem.InputControl control in Gamepad.current.allControls)
            {
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
                {
                    return true;
                }
            }
        }

        return false;
#else
        return Input.anyKeyDown;
#endif
    }

    public void EnsureUi()
    {
        FindExistingUiReferences();
        EnsureGuestButton();

        if (loginPanel != null && registerPanel != null)
        {
            return;
        }

        if (loginPanel != null && registerPanel != null)
        {
            return;
        }

        Canvas canvas = UIInputUtility.FindSceneCanvas();
        if (canvas == null)
        {
            GameObject canvasObject = new("AuthCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            CreateEventSystem();
        }

        BuildBackground(canvas.transform);

        loginPanel = CreatePanel(canvas.transform, "LoginPanel");
        registerPanel = CreatePanel(canvas.transform, "RegisterPanel");

        TextMeshProUGUI title = CreateLabel(loginPanel.transform, "FarmSim", 44, 58f);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        emailInput = CreateInput(loginPanel.transform, "Email", false);
        passwordInput = CreateInput(loginPanel.transform, "Password", true);
        loginButton = CreateButton(loginPanel.transform, "\u0412\u043e\u0439\u0442\u0438");
        loginButton.gameObject.name = "LoginButton";
        EnsureGuestButton();
        switchToRegisterButton = CreateButton(loginPanel.transform, "\u041f\u0435\u0440\u0435\u0439\u0442\u0438 \u043a \u0440\u0435\u0433\u0438\u0441\u0442\u0440\u0430\u0446\u0438\u0438");
        switchToRegisterButton.gameObject.name = "SwitchToRegisterButton";

        TextMeshProUGUI registerTitle = CreateLabel(registerPanel.transform, "\u0420\u0435\u0433\u0438\u0441\u0442\u0440\u0430\u0446\u0438\u044f", 38, 52f);
        registerTitle.alignment = TextAlignmentOptions.Center;
        registerTitle.fontStyle = FontStyles.Bold;
        registerEmailInput = CreateInput(registerPanel.transform, "Email", false);
        registerPasswordInput = CreateInput(registerPanel.transform, "Password", true);
        confirmPasswordInput = CreateInput(registerPanel.transform, "Confirm Password", true);
        displayNameInput = CreateInput(registerPanel.transform, "Display Name", false);
        registerButton = CreateButton(registerPanel.transform, "\u0417\u0430\u0440\u0435\u0433\u0438\u0441\u0442\u0440\u0438\u0440\u043e\u0432\u0430\u0442\u044c\u0441\u044f");
        registerButton.gameObject.name = "RegisterButton";
        switchToLoginButton = CreateButton(registerPanel.transform, "\u041f\u0435\u0440\u0435\u0439\u0442\u0438 \u043a\u043e \u0432\u0445\u043e\u0434\u0443");
        switchToLoginButton.gameObject.name = "SwitchToLoginButton";

        registerEmailInput.onValueChanged.AddListener(value => SetInputTextWithoutNotify(emailInput, value));
        registerPasswordInput.onValueChanged.AddListener(value => SetInputTextWithoutNotify(passwordInput, value));
        emailInput.onValueChanged.AddListener(value => SetInputTextWithoutNotify(registerEmailInput, value));
        passwordInput.onValueChanged.AddListener(value => SetInputTextWithoutNotify(registerPasswordInput, value));

        statusText = CreateLabel(canvas.transform, "", 24, 54f);
        statusText.gameObject.name = "AuthStatusText";
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0.25f, 0.12f);
        statusRect.anchorMax = new Vector2(0.75f, 0.2f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;
        statusText.color = new Color(1f, 0.86f, 0.58f, 1f);
        statusText.alignment = TextAlignmentOptions.Center;
    }

    private void EnsureGuestButton()
    {
        if (guestButton != null || loginPanel == null)
        {
            return;
        }

        guestButton = CreateButton(loginPanel.transform, "\u0412\u043e\u0439\u0442\u0438 \u043a\u0430\u043a \u0433\u043e\u0441\u0442\u044c");
        guestButton.gameObject.name = "GuestButton";
        if (loginButton != null)
        {
            guestButton.transform.SetSiblingIndex(loginButton.transform.GetSiblingIndex() + 1);
        }
    }

    private void FindExistingUiReferences()
    {
        loginPanel = GameObject.Find("LoginPanel");
        registerPanel = GameObject.Find("RegisterPanel");
        emailInput ??= FindInput(loginPanel, "Email") ?? GameObject.Find("Email")?.GetComponent<TMP_InputField>();
        passwordInput ??= FindInput(loginPanel, "Password") ?? GameObject.Find("Password")?.GetComponent<TMP_InputField>();
        registerEmailInput ??= FindInput(registerPanel, "Email");
        registerPasswordInput ??= FindInput(registerPanel, "Password");
        confirmPasswordInput ??= FindInput(registerPanel, "Confirm Password") ?? GameObject.Find("Confirm Password")?.GetComponent<TMP_InputField>();
        displayNameInput ??= FindInput(registerPanel, "Display Name") ?? GameObject.Find("Display Name")?.GetComponent<TMP_InputField>();
        statusText ??= GameObject.Find("AuthStatusText")?.GetComponent<TextMeshProUGUI>();
        loginButton ??= GameObject.Find("LoginButton")?.GetComponent<Button>();
        guestButton ??= GameObject.Find("GuestButton")?.GetComponent<Button>();
        registerButton ??= GameObject.Find("RegisterButton")?.GetComponent<Button>();
        switchToRegisterButton ??= GameObject.Find("SwitchToRegisterButton")?.GetComponent<Button>();
        switchToLoginButton ??= GameObject.Find("SwitchToLoginButton")?.GetComponent<Button>();
    }

    private static TMP_InputField FindInput(GameObject parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        TMP_InputField[] inputs = parent.GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField input in inputs)
        {
            if (input != null && input.gameObject.name == objectName)
            {
                return input;
            }
        }

        return null;
    }

    private static string GetText(TMP_InputField preferred, TMP_InputField fallback)
    {
        if (preferred != null)
        {
            return preferred.text;
        }

        return fallback != null ? fallback.text : string.Empty;
    }

    private static void SetInputTextWithoutNotify(TMP_InputField input, string value)
    {
        if (input == null || string.Equals(input.text, value, System.StringComparison.Ordinal))
        {
            return;
        }

        input.SetTextWithoutNotify(value);
    }

    private void BuildBackground(Transform parent)
    {
        GameObject backgroundObject = new("AuthBackground", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(parent, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.color = backgroundSprite != null ? Color.white : new Color(0.08f, 0.13f, 0.1f, 1f);
        backgroundImage.preserveAspect = false;

        GameObject fadeObject = new("AuthFade", typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(parent, false);
        RectTransform fadeRect = fadeObject.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fadeObject.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.03f, 0.46f);
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystem = new("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif
    }

    private static bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static GameObject CreatePanel(Transform parent, string name)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(500f, 620f);
        rect.anchoredPosition = Vector2.zero;
        Image image = panel.GetComponent<Image>();
        image.sprite = RoundedSpriteCache.Get(64, 22, Color.white);
        image.type = Image.Type.Sliced;
        image.color = new Color(0.045f, 0.075f, 0.06f, 0.86f);
        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(42, 42, 38, 38);
        layout.spacing = 16f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return panel;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string text, int fontSize, float preferredHeight)
    {
        GameObject label = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        label.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 16;
        tmp.fontSizeMax = fontSize;
        label.GetComponent<LayoutElement>().preferredHeight = preferredHeight;
        return tmp;
    }

    private static TMP_InputField CreateInput(Transform parent, string placeholder, bool password)
    {
        GameObject root = new(placeholder, typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        Image rootImage = root.GetComponent<Image>();
        rootImage.sprite = RoundedSpriteCache.Get(64, 14, Color.white);
        rootImage.type = Image.Type.Sliced;
        rootImage.color = new Color(0.95f, 0.97f, 0.91f, 0.98f);
        root.GetComponent<LayoutElement>().preferredHeight = 58f;

        GameObject textObject = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 8f);
        textRect.offsetMax = new Vector2(-18f, -8f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.color = Color.black;
        text.fontSize = 24;

        GameObject placeholderObject = new("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderObject.transform.SetParent(root.transform, false);
        RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(18f, 8f);
        placeholderRect.offsetMax = new Vector2(-18f, -8f);
        TextMeshProUGUI placeholderText = placeholderObject.GetComponent<TextMeshProUGUI>();
        placeholderText.text = placeholder;
        placeholderText.color = new Color(0f, 0f, 0f, 0.45f);
        placeholderText.fontSize = 24;

        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        return input;
    }

    private static Button CreateButton(Transform parent, string text)
    {
        GameObject root = new(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.sprite = RoundedSpriteCache.Get(64, 14, Color.white);
        image.type = Image.Type.Sliced;
        image.color = new Color(0.52f, 0.78f, 0.18f, 0.98f);
        root.GetComponent<LayoutElement>().preferredHeight = 58f;
        Button button = root.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = Color.Lerp(image.color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(image.color, Color.black, 0.14f);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        TextMeshProUGUI label = CreateLabel(root.transform, text, 23, 58f);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return button;
    }
}
