using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

#if FARMSIM_FIREBASE
using Firebase.Auth;
#endif

public class FirebaseAuthService : MonoBehaviour
{
    public static FirebaseAuthService Instance { get; private set; }

#if FARMSIM_FIREBASE
    private FirebaseAuth auth;
#endif
    private const string FirebaseApiKey = "AIzaSyA_e0qiRMX6BAGsjvMx6WkcXax8z5KwBNs";
    private const string FallbackUserIdKey = "farmsim_firebase_rest_user_id";
    private const string FallbackEmailKey = "farmsim_firebase_rest_email";
    private const string FallbackDisplayNameKey = "farmsim_firebase_rest_display_name";
    private const string FallbackIdTokenKey = "farmsim_firebase_rest_id_token";
    private const string FallbackRefreshTokenKey = "farmsim_firebase_rest_refresh_token";
    private const string FallbackTokenExpiresAtKey = "farmsim_firebase_rest_token_expires_at";

    private string fallbackUserId;
    private string fallbackEmail;
    private string fallbackDisplayName;
    private string fallbackIdToken;
    private string fallbackRefreshToken;
    private long fallbackTokenExpiresAtUnix;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadFallbackSession();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public async Task<bool> InitializeAuth()
    {
#if FARMSIM_FIREBASE && (UNITY_STANDALONE || UNITY_EDITOR)
        if (!string.IsNullOrEmpty(fallbackUserId))
        {
            await EnsureRestSessionFresh();
        }

        await Task.Yield();
        return true;
#else
        FirebaseInitializer initializer = FirebaseInitializer.Instance ?? FindFirstObjectByType<FirebaseInitializer>();
        if (initializer == null)
        {
            Debug.LogWarning("FirebaseAuthService: FirebaseInitializer not found.");
            return false;
        }

        bool initialized = await initializer.InitializeFirebaseAsync();
        if (!initialized)
        {
            return false;
        }

#if FARMSIM_FIREBASE
        auth ??= FirebaseAuth.DefaultInstance;
        return auth != null;
#else
        return false;
#endif
#endif
    }

    public async Task<AuthResultData> RegisterWithEmailPassword(string email, string password, string confirmPassword, string displayName)
    {
        string validationError = ValidateRegisterInput(email, password, confirmPassword, displayName);
        if (!string.IsNullOrEmpty(validationError))
        {
            return AuthResultData.Fail(validationError);
        }

#if FARMSIM_FIREBASE && (UNITY_STANDALONE || UNITY_EDITOR)
        return await RegisterWithRestApi(email, password, displayName);
#else

        if (!await InitializeAuth())
        {
            return AuthResultData.Fail("Нет подключения к Firebase.");
        }

#if FARMSIM_FIREBASE
        try
        {
            AuthResult result = await auth.CreateUserWithEmailAndPasswordAsync(email.Trim(), password);
            await UpdateDisplayNameIfNeeded(displayName);
            FirebaseSaveService saveService = FirebaseSaveService.Instance ?? FindFirstObjectByType<FirebaseSaveService>();
            if (saveService != null)
            {
                await saveService.InitializeDatabase();
                await saveService.CreateInitialUserDataIfMissing();
                await saveService.UpdateLastLoginAt();
            }

            return AuthResultData.Ok(result.User.UserId);
        }
        catch (Exception exception)
        {
            if (IsInternalFirebaseAuthError(exception))
            {
                AuthResultData restResult = await RegisterWithRestApi(email, password, displayName);
                if (restResult.Success)
                {
                    Debug.LogWarning($"Firebase SDK registration failed with internal error. REST auth fallback succeeded. Original error: {exception}");
                    return restResult;
                }

                return restResult;
            }

            string message = GetReadableAuthError(exception);
            Debug.LogWarning($"Registration failed: {exception}");
            return AuthResultData.Fail(message);
        }
#else
        await Task.Yield();
        return AuthResultData.Fail("Firebase SDK не подключен. Импортируй SDK и добавь FARMSIM_FIREBASE.");
#endif
#endif
    }

    public async Task<AuthResultData> LoginWithEmailPassword(string email, string password)
    {
        string validationError = ValidateLoginInput(email, password);
        if (!string.IsNullOrEmpty(validationError))
        {
            return AuthResultData.Fail(validationError);
        }

#if FARMSIM_FIREBASE && (UNITY_STANDALONE || UNITY_EDITOR)
        return await LoginWithRestApi(email, password);
#else

        if (!await InitializeAuth())
        {
            return AuthResultData.Fail("Нет подключения к Firebase.");
        }

#if FARMSIM_FIREBASE
        try
        {
            AuthResult result = await auth.SignInWithEmailAndPasswordAsync(email.Trim(), password);
            FirebaseSaveService saveService = FirebaseSaveService.Instance ?? FindFirstObjectByType<FirebaseSaveService>();
            if (saveService != null)
            {
                await saveService.InitializeDatabase();
                await saveService.CreateInitialUserDataIfMissing();
                await saveService.UpdateLastLoginAt();
            }

            return AuthResultData.Ok(result.User.UserId);
        }
        catch (Exception exception)
        {
            if (IsInternalFirebaseAuthError(exception))
            {
                AuthResultData restResult = await LoginWithRestApi(email, password);
                if (restResult.Success)
                {
                    Debug.LogWarning($"Firebase SDK login failed with internal error. REST auth fallback succeeded. Original error: {exception}");
                    return restResult;
                }

                return restResult;
            }

            string message = GetReadableAuthError(exception);
            Debug.LogWarning($"Login failed: {exception}");
            return AuthResultData.Fail(message);
        }
#else
        await Task.Yield();
        return AuthResultData.Fail("Firebase SDK не подключен. Импортируй SDK и добавь FARMSIM_FIREBASE.");
#endif
#endif
    }

    public void Logout()
    {
#if FARMSIM_FIREBASE
        auth ??= FirebaseAuth.DefaultInstance;
        auth?.SignOut();
#endif
        ClearFallbackSession();
    }

    public bool IsSignedIn()
    {
#if FARMSIM_FIREBASE
        auth ??= FirebaseAuth.DefaultInstance;
        return auth?.CurrentUser != null || HasRestSession();
#else
        return HasRestSession();
#endif
    }

    public string GetUserId()
    {
        if (!string.IsNullOrEmpty(fallbackUserId))
        {
            return fallbackUserId;
        }

#if FARMSIM_FIREBASE
        auth ??= FirebaseAuth.DefaultInstance;
        return auth?.CurrentUser?.UserId ?? string.Empty;
#else
        return string.Empty;
#endif
    }

    public string GetUserEmail()
    {
        if (!string.IsNullOrEmpty(fallbackEmail))
        {
            return fallbackEmail;
        }

#if FARMSIM_FIREBASE
        auth ??= FirebaseAuth.DefaultInstance;
        return auth?.CurrentUser?.Email ?? string.Empty;
#else
        return string.Empty;
#endif
    }

    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(fallbackDisplayName))
        {
            return fallbackDisplayName;
        }

#if FARMSIM_FIREBASE
        auth ??= FirebaseAuth.DefaultInstance;
        return auth?.CurrentUser?.DisplayName ?? string.Empty;
#else
        return string.Empty;
#endif
    }

    public bool HasRestSession()
    {
        return !string.IsNullOrWhiteSpace(fallbackUserId) &&
               !string.IsNullOrWhiteSpace(fallbackIdToken) &&
               !string.IsNullOrWhiteSpace(fallbackRefreshToken);
    }

    public string GetRestIdToken()
    {
        return fallbackIdToken ?? string.Empty;
    }

    public async Task<bool> EnsureRestSessionFresh()
    {
        if (string.IsNullOrWhiteSpace(fallbackUserId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(fallbackRefreshToken))
        {
            ClearFallbackSession();
            return false;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!string.IsNullOrWhiteSpace(fallbackIdToken) && fallbackTokenExpiresAtUnix > now + 60)
        {
            return true;
        }

        RestRefreshResult refreshResult = await RefreshRestIdToken(fallbackRefreshToken);
        if (!refreshResult.Success)
        {
            Debug.LogWarning($"Firebase REST session refresh failed: {refreshResult.Message}");
            ClearFallbackSession();
            return false;
        }

        SaveFallbackSession(
            string.IsNullOrWhiteSpace(refreshResult.UserId) ? fallbackUserId : refreshResult.UserId,
            fallbackEmail,
            fallbackDisplayName,
            refreshResult.IdToken,
            refreshResult.RefreshToken,
            refreshResult.ExpiresInSeconds);

        return true;
    }

    public async Task<AuthResultData> UpdateDisplayNameIfNeeded(string displayName)
    {
        string safeName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();

#if FARMSIM_FIREBASE
        auth ??= FirebaseAuth.DefaultInstance;
        FirebaseUser user = auth?.CurrentUser;
        if (user == null)
        {
            return AuthResultData.Fail("Пользователь не авторизован.");
        }

        if (string.Equals(user.DisplayName, safeName, StringComparison.Ordinal))
        {
            return AuthResultData.Ok(user.UserId);
        }

        try
        {
            UserProfile profile = new() { DisplayName = safeName };
            await user.UpdateUserProfileAsync(profile);
            return AuthResultData.Ok(user.UserId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Display name update failed: {exception}");
            return AuthResultData.Fail("Не удалось сохранить имя игрока.");
        }
#else
        await Task.Yield();
        return AuthResultData.Fail("Firebase SDK не подключен.");
#endif
    }

    private static string ValidateRegisterInput(string email, string password, string confirmPassword, string displayName)
    {
        string loginError = ValidateLoginInput(email, password);
        if (!string.IsNullOrEmpty(loginError))
        {
            return loginError;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return "Пароли не совпадают.";
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "Введите имя игрока.";
        }

        return string.Empty;
    }

    private static string ValidateLoginInput(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return "Введите корректный email.";
        }

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            return "Пароль должен быть не короче 6 символов.";
        }

        return string.Empty;
    }

    private async Task<AuthResultData> LoginWithRestApi(string email, string password)
    {
        RestAuthResult result = await SendRestAuthRequest("accounts:signInWithPassword", email, password, null);
        if (!result.Success)
        {
            return AuthResultData.Fail(result.Message);
        }

        SaveFallbackSession(result.UserId, result.Email, result.DisplayName, result.IdToken, result.RefreshToken, result.ExpiresInSeconds);
        return AuthResultData.Ok(result.UserId);
    }

    private async Task<AuthResultData> RegisterWithRestApi(string email, string password, string displayName)
    {
        RestAuthResult result = await SendRestAuthRequest("accounts:signUp", email, password, displayName);
        if (!result.Success)
        {
            return AuthResultData.Fail(result.Message);
        }

        string safeName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
        SaveFallbackSession(result.UserId, result.Email, safeName, result.IdToken, result.RefreshToken, result.ExpiresInSeconds);
        return AuthResultData.Ok(result.UserId);
    }

    private static async Task<RestAuthResult> SendRestAuthRequest(string method, string email, string password, string displayName)
    {
        string url = $"https://identitytoolkit.googleapis.com/v1/{method}?key={FirebaseApiKey}";
        RestAuthRequest request = new()
        {
            email = email.Trim(),
            password = password,
            returnSecureToken = true,
            displayName = displayName
        };

        string json = JsonUtility.ToJson(request);
        using UnityWebRequest webRequest = new(url, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");

        UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
        while (!operation.isDone)
        {
            await Task.Yield();
        }

        string responseText = webRequest.downloadHandler != null ? webRequest.downloadHandler.text : string.Empty;
        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            return RestAuthResult.Fail(GetReadableRestAuthError(responseText, webRequest.error));
        }

        RestAuthSuccessResponse response = JsonUtility.FromJson<RestAuthSuccessResponse>(responseText);
        if (response == null || string.IsNullOrEmpty(response.localId))
        {
            return RestAuthResult.Fail("Firebase REST Auth вернул пустой ответ.");
        }

        return RestAuthResult.Ok(response.localId, response.email, response.displayName, response.idToken, response.refreshToken, ParseExpiresIn(response.expiresIn));
    }

    private static async Task<RestRefreshResult> RefreshRestIdToken(string refreshToken)
    {
        string url = $"https://securetoken.googleapis.com/v1/token?key={FirebaseApiKey}";
        string body = $"grant_type=refresh_token&refresh_token={UnityWebRequest.EscapeURL(refreshToken)}";

        using UnityWebRequest webRequest = new(url, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);
        webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

        UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
        while (!operation.isDone)
        {
            await Task.Yield();
        }

        string responseText = webRequest.downloadHandler != null ? webRequest.downloadHandler.text : string.Empty;
        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            return RestRefreshResult.Fail(GetReadableRestAuthError(responseText, webRequest.error));
        }

        RestRefreshResponse response = JsonUtility.FromJson<RestRefreshResponse>(responseText);
        if (response == null || string.IsNullOrWhiteSpace(response.id_token))
        {
            return RestRefreshResult.Fail("Firebase REST Auth refresh вернул пустой токен.");
        }

        return RestRefreshResult.Ok(response.user_id, response.id_token, response.refresh_token, ParseExpiresIn(response.expires_in));
    }

    private static int ParseExpiresIn(string expiresIn)
    {
        return int.TryParse(expiresIn, out int seconds) ? Mathf.Max(60, seconds) : 3600;
    }

    private static string GetReadableRestAuthError(string responseText, string fallbackError)
    {
        string normalized = (responseText ?? string.Empty).ToUpperInvariant();
        if (normalized.Contains("EMAIL_EXISTS"))
        {
            return "Email уже используется.";
        }

        if (normalized.Contains("INVALID_LOGIN_CREDENTIALS") || normalized.Contains("INVALID_PASSWORD"))
        {
            return "Неверный email или пароль.";
        }

        if (normalized.Contains("EMAIL_NOT_FOUND"))
        {
            return "Пользователь не найден.";
        }

        if (normalized.Contains("OPERATION_NOT_ALLOWED"))
        {
            return "В Firebase Console не включен вход по Email/Password.";
        }

        if (normalized.Contains("WEAK_PASSWORD"))
        {
            return "Пароль слишком слабый.";
        }

        return $"Ошибка Firebase Auth REST: {fallbackError}. {responseText}";
    }

    private static bool IsInternalFirebaseAuthError(Exception exception)
    {
        string message = exception.Message ?? string.Empty;
        return message.IndexOf("internal", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("iternal", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SaveFallbackSession(string userId, string email, string displayName, string idToken, string refreshToken, int expiresInSeconds)
    {
        fallbackUserId = userId ?? string.Empty;
        fallbackEmail = email ?? string.Empty;
        fallbackDisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
        fallbackIdToken = idToken ?? string.Empty;
        fallbackRefreshToken = refreshToken ?? string.Empty;
        fallbackTokenExpiresAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Mathf.Max(60, expiresInSeconds);
        PlayerPrefs.SetString(FallbackUserIdKey, fallbackUserId);
        PlayerPrefs.SetString(FallbackEmailKey, fallbackEmail);
        PlayerPrefs.SetString(FallbackDisplayNameKey, fallbackDisplayName);
        PlayerPrefs.SetString(FallbackIdTokenKey, fallbackIdToken);
        PlayerPrefs.SetString(FallbackRefreshTokenKey, fallbackRefreshToken);
        PlayerPrefs.SetString(FallbackTokenExpiresAtKey, fallbackTokenExpiresAtUnix.ToString());
        PlayerPrefs.Save();
    }

    private void LoadFallbackSession()
    {
        fallbackUserId = PlayerPrefs.GetString(FallbackUserIdKey, string.Empty);
        fallbackEmail = PlayerPrefs.GetString(FallbackEmailKey, string.Empty);
        fallbackDisplayName = PlayerPrefs.GetString(FallbackDisplayNameKey, string.Empty);
        fallbackIdToken = PlayerPrefs.GetString(FallbackIdTokenKey, string.Empty);
        fallbackRefreshToken = PlayerPrefs.GetString(FallbackRefreshTokenKey, string.Empty);
        string expiresAt = PlayerPrefs.GetString(FallbackTokenExpiresAtKey, string.Empty);
        fallbackTokenExpiresAtUnix = long.TryParse(expiresAt, out long parsedExpiresAt) ? parsedExpiresAt : 0;

        if (!string.IsNullOrEmpty(fallbackUserId) &&
            (string.IsNullOrEmpty(fallbackIdToken) || string.IsNullOrEmpty(fallbackRefreshToken)))
        {
            ClearFallbackSession();
        }
    }

    private void ClearFallbackSession()
    {
        fallbackUserId = string.Empty;
        fallbackEmail = string.Empty;
        fallbackDisplayName = string.Empty;
        fallbackIdToken = string.Empty;
        fallbackRefreshToken = string.Empty;
        fallbackTokenExpiresAtUnix = 0;
        PlayerPrefs.DeleteKey(FallbackUserIdKey);
        PlayerPrefs.DeleteKey(FallbackEmailKey);
        PlayerPrefs.DeleteKey(FallbackDisplayNameKey);
        PlayerPrefs.DeleteKey(FallbackIdTokenKey);
        PlayerPrefs.DeleteKey(FallbackRefreshTokenKey);
        PlayerPrefs.DeleteKey(FallbackTokenExpiresAtKey);
        PlayerPrefs.Save();
    }

    private static string GetReadableAuthError(Exception exception)
    {
#if FARMSIM_FIREBASE
        string rawMessage = exception.Message ?? string.Empty;
        string normalizedMessage = rawMessage.ToUpperInvariant();
        if (normalizedMessage.Contains("INTERNAL") || normalizedMessage.Contains("ITERNAL"))
        {
            return $"Внутренняя ошибка Firebase Auth SDK. Для ПК проверьте, что Email/Password включен в Firebase Console, файл Assets/StreamingAssets/google-services-desktop.json существует, а ПК имеет доступ к identitytoolkit.googleapis.com. {rawMessage}";
        }

        if (normalizedMessage.Contains("OPERATION_NOT_ALLOWED"))
        {
            return "В Firebase Console не включен вход по Email/Password.";
        }

        if (normalizedMessage.Contains("EMAIL_EXISTS"))
        {
            return "Email уже используется.";
        }

        if (normalizedMessage.Contains("INVALID_LOGIN_CREDENTIALS") || normalizedMessage.Contains("INVALID_PASSWORD"))
        {
            return "Неверный email или пароль.";
        }

        if (normalizedMessage.Contains("EMAIL_NOT_FOUND"))
        {
            return "Пользователь не найден.";
        }

        if (exception is Firebase.FirebaseException firebaseException)
        {
            Debug.LogWarning($"Firebase Auth error code: {firebaseException.ErrorCode}, message: {rawMessage}");
            AuthError error = (AuthError)firebaseException.ErrorCode;
            return error switch
            {
                AuthError.InvalidEmail => "Неверный email.",
                AuthError.WrongPassword => "Неверный пароль.",
                AuthError.UserNotFound => "Пользователь не найден.",
                AuthError.EmailAlreadyInUse => "Email уже используется.",
                AuthError.NetworkRequestFailed => "Нет подключения к Firebase.",
                AuthError.WeakPassword => "Пароль слишком слабый.",
                AuthError.MissingEmail => "Введите email.",
                AuthError.MissingPassword => "Введите пароль.",
                _ => $"Ошибка авторизации: {error}. {exception.Message}"
            };
        }

#endif

        return $"Ошибка авторизации: {exception.Message}";
    }
}

public readonly struct AuthResultData
{
    public readonly bool Success;
    public readonly string Message;
    public readonly string UserId;

    private AuthResultData(bool success, string message, string userId)
    {
        Success = success;
        Message = message;
        UserId = userId;
    }

    public static AuthResultData Ok(string userId)
    {
        return new AuthResultData(true, string.Empty, userId);
    }

    public static AuthResultData Fail(string message)
    {
        return new AuthResultData(false, message, string.Empty);
    }
}

[Serializable]
public class RestAuthRequest
{
    public string email;
    public string password;
    public bool returnSecureToken;
    public string displayName;
}

[Serializable]
public class RestAuthSuccessResponse
{
    public string localId;
    public string email;
    public string displayName;
    public string idToken;
    public string refreshToken;
    public string expiresIn;
}

[Serializable]
public class RestRefreshResponse
{
    public string user_id;
    public string id_token;
    public string refresh_token;
    public string expires_in;
}

public readonly struct RestAuthResult
{
    public readonly bool Success;
    public readonly string Message;
    public readonly string UserId;
    public readonly string Email;
    public readonly string DisplayName;
    public readonly string IdToken;
    public readonly string RefreshToken;
    public readonly int ExpiresInSeconds;

    private RestAuthResult(bool success, string message, string userId, string email, string displayName, string idToken, string refreshToken, int expiresInSeconds)
    {
        Success = success;
        Message = message;
        UserId = userId;
        Email = email;
        DisplayName = displayName;
        IdToken = idToken;
        RefreshToken = refreshToken;
        ExpiresInSeconds = expiresInSeconds;
    }

    public static RestAuthResult Ok(string userId, string email, string displayName, string idToken, string refreshToken, int expiresInSeconds)
    {
        return new RestAuthResult(true, string.Empty, userId, email, displayName, idToken, refreshToken, expiresInSeconds);
    }

    public static RestAuthResult Fail(string message)
    {
        return new RestAuthResult(false, message, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0);
    }
}

public readonly struct RestRefreshResult
{
    public readonly bool Success;
    public readonly string Message;
    public readonly string UserId;
    public readonly string IdToken;
    public readonly string RefreshToken;
    public readonly int ExpiresInSeconds;

    private RestRefreshResult(bool success, string message, string userId, string idToken, string refreshToken, int expiresInSeconds)
    {
        Success = success;
        Message = message;
        UserId = userId;
        IdToken = idToken;
        RefreshToken = refreshToken;
        ExpiresInSeconds = expiresInSeconds;
    }

    public static RestRefreshResult Ok(string userId, string idToken, string refreshToken, int expiresInSeconds)
    {
        return new RestRefreshResult(true, string.Empty, userId, idToken, refreshToken, expiresInSeconds);
    }

    public static RestRefreshResult Fail(string message)
    {
        return new RestRefreshResult(false, message, string.Empty, string.Empty, string.Empty, 0);
    }
}
