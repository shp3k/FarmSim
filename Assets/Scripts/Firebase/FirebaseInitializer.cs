using System;
using System.Threading.Tasks;
using UnityEngine;

#if FARMSIM_FIREBASE
using Firebase;
#endif

public class FirebaseInitializer : MonoBehaviour
{
    public static FirebaseInitializer Instance { get; private set; }

    [SerializeField] private bool initializeOnAwake = true;

    public bool IsInitialized { get; private set; }
    public bool IsInitializing { get; private set; }
    public string LastError { get; private set; }

    public event Action<bool, string> InitializationCompleted;

    private Task<bool> initializationTask;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (initializeOnAwake)
        {
            _ = InitializeFirebaseAsync();
        }
    }

    public Task<bool> InitializeFirebaseAsync()
    {
        initializationTask ??= InitializeInternalAsync();
        return initializationTask;
    }

    private async Task<bool> InitializeInternalAsync()
    {
        if (IsInitialized)
        {
            return true;
        }

        IsInitializing = true;
        LastError = string.Empty;

#if FARMSIM_FIREBASE
        try
        {
            DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status == DependencyStatus.Available)
            {
                IsInitialized = true;
                Debug.Log("Firebase initialized.");
                InitializationCompleted?.Invoke(true, string.Empty);
                return true;
            }

            LastError = $"Firebase dependencies are not available: {status}";
            Debug.LogError(LastError);
        }
        catch (Exception exception)
        {
            LastError = $"Firebase initialization failed: {exception.Message}";
            Debug.LogError(LastError);
        }
        finally
        {
            IsInitializing = false;
        }
#else
        await Task.Yield();
        LastError = "Firebase SDK is not compiled. Import Firebase Unity SDK and add FARMSIM_FIREBASE to Scripting Define Symbols.";
        Debug.LogWarning(LastError);
        IsInitializing = false;
#endif

        InitializationCompleted?.Invoke(false, LastError);
        return false;
    }
}
