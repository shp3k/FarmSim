using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneAuthGuard : MonoBehaviour
{
    [SerializeField] private string authSceneName = "AuthScene";
#if !FARMSIM_FIREBASE
    [SerializeField] private bool allowPlayWithoutFirebaseSdk = true;
#endif

    private async void Start()
    {
#if !FARMSIM_FIREBASE
        if (allowPlayWithoutFirebaseSdk)
        {
            Debug.LogWarning("GameSceneAuthGuard: Firebase SDK is not compiled. Local play is allowed.");
            return;
        }
#endif

        FirebaseAuthService authService = FirebaseAuthService.Instance ?? FindFirstObjectByType<FirebaseAuthService>();
        if (authService == null)
        {
            authService = CreateFirebaseManager().GetComponent<FirebaseAuthService>();
        }

        await authService.InitializeAuth();
        if (!authService.HasActiveSession())
        {
            Debug.LogWarning("GameSceneAuthGuard: user is not signed in. Returning to AuthScene.");
            SceneTransitionManager.LoadScene(authSceneName);
        }
    }

    private static GameObject CreateFirebaseManager()
    {
        GameObject manager = new("FirebaseManager");
        manager.AddComponent<FirebaseInitializer>();
        manager.AddComponent<FirebaseAuthService>();
        manager.AddComponent<FirebaseSaveService>();
        DontDestroyOnLoad(manager);
        return manager;
    }
}
