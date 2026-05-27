using UnityEngine;
using UnityEngine.SceneManagement;

public enum ReadyMarkerKind
{
    Crop,
    Animal
}

public class ReadyMarkerManager : MonoBehaviour
{
    public static ReadyMarkerManager Instance { get; private set; }

    [Header("Marker Prefabs")]
    [SerializeField] private GameObject readyMarkerPrefab;
    [SerializeField] private GameObject cropReadyMarkerPrefab;
    [SerializeField] private GameObject animalReadyMarkerPrefab;

    [Header("Offsets")]
    [SerializeField] private Vector3 cropMarkerOffset = new(0f, 1f, 0f);
    [SerializeField] private Vector3 animalMarkerOffset = new(0f, 1f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
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

        if (FindFirstObjectByType<ReadyMarkerManager>() != null)
        {
            return;
        }

        GameObject host = new GameObject("ReadyMarkerManager");
        host.AddComponent<ReadyMarkerManager>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public GameObject GetMarkerPrefab(ReadyMarkerKind kind)
    {
        GameObject specificPrefab = kind == ReadyMarkerKind.Crop
            ? cropReadyMarkerPrefab
            : animalReadyMarkerPrefab;

        return specificPrefab != null ? specificPrefab : readyMarkerPrefab;
    }

    public Vector3 GetMarkerOffset(ReadyMarkerKind kind)
    {
        return kind == ReadyMarkerKind.Crop ? cropMarkerOffset : animalMarkerOffset;
    }
}
