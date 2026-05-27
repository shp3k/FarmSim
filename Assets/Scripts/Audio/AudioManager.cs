using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("SFX Clips")]
    [SerializeField] private AudioClip[] clickSounds;
    [SerializeField] private AudioClip[] farmSFXTracks;

    [Header("Music Clips")]
    [SerializeField] private AudioClip[] backgroundMusicTracks;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;

    [Header("Runtime Sources (Optional)")]
    [Tooltip("Optional. Leave empty unless you want to provide a custom AudioSource.")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("Optional. Leave empty unless you want to provide a custom AudioSource.")]
    [SerializeField] private AudioSource sfxSource;

    private const float ButtonScanInterval = 0.5f;
    private const float AudioRetryDelay = 0.25f;
    private const string MusicVolumeKey = "farmsim_music_volume";
    private const string SFXVolumeKey = "farmsim_sfx_volume";
    private AudioListener fallbackAudioListener;
    private Coroutine buttonScanRoutine;
    private Coroutine musicPlaybackRoutine;
    private Coroutine farmSFXPlaybackRoutine;
    private AudioClip lastMusicTrack;
    private AudioClip lastFarmSFXTrack;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null || FindFirstObjectByType<AudioManager>() != null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>("AudioManager");
        if (prefab != null && prefab.GetComponent<AudioManager>() != null)
        {
            Instantiate(prefab);
            return;
        }

        GameObject managerObject = new GameObject("AudioManager");
        managerObject.AddComponent<AudioManager>();
        Debug.LogWarning("AudioManager prefab was not found in Resources. Audio clips must be assigned in a scene AudioManager.");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A scene prefab can provide Inspector settings without replacing the persistent manager.
            Instance.ApplySettingsFrom(this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSources();
        EnsureAudioListener();
        LoadSavedVolumes();
        ApplyVolumes();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (buttonScanRoutine == null)
        {
            buttonScanRoutine = StartCoroutine(ScanButtonsRoutine());
        }
    }

    private void Start()
    {
        RegisterSceneButtons();
        PlayMusic();
        PlayFarmSFX();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (buttonScanRoutine != null)
        {
            StopCoroutine(buttonScanRoutine);
            buttonScanRoutine = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateAudioListenerState();
        RegisterSceneButtons();
        PlayMusic();
        PlayFarmSFX();
    }

    public void PlayClick()
    {
        EnsureAudioSources();

        AudioClip clickSound = GetRandomTrack(clickSounds, null);

        if (clickSound == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clickSound);
    }

    public void PlayMusic()
    {
        EnsureAudioSources();

        if (musicSource == null)
        {
            return;
        }

        if (HasTracks(backgroundMusicTracks))
        {
            if (musicPlaybackRoutine == null)
            {
                musicPlaybackRoutine = StartCoroutine(PlayRandomMusicRoutine());
            }

            return;
        }

        StopMusicRoutine();
        musicSource.Stop();
    }

    public void PlayFarmSFX()
    {
        EnsureAudioSources();

        if (sfxSource == null)
        {
            return;
        }

        if (HasTracks(farmSFXTracks))
        {
            if (farmSFXPlaybackRoutine == null)
            {
                farmSFXPlaybackRoutine = StartCoroutine(PlayRandomFarmSFXRoutine());
            }

            return;
        }

        StopFarmSFXRoutine();
        sfxSource.Stop();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);

        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);

        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }

    }

    private void ApplySettingsFrom(AudioManager source)
    {
        if (source == null)
        {
            return;
        }

        if (source.clickSounds != null && source.clickSounds.Length > 0)
        {
            clickSounds = source.clickSounds;
        }

        if (source.backgroundMusicTracks != null && source.backgroundMusicTracks.Length > 0)
        {
            backgroundMusicTracks = source.backgroundMusicTracks;
        }

        if (source.farmSFXTracks != null && source.farmSFXTracks.Length > 0)
        {
            farmSFXTracks = source.farmSFXTracks;
        }

        musicVolume = source.musicVolume;
        sfxVolume = source.sfxVolume;
        LoadSavedVolumes();
        ApplyVolumes();
        PlayMusic();
        PlayFarmSFX();
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = CreateAudioSource("MusicSource");
            musicSource.loop = true;
        }

        if (sfxSource == null)
        {
            sfxSource = CreateAudioSource("SFXSource");
            sfxSource.loop = false;
        }

    }

    private void EnsureAudioListener()
    {
        fallbackAudioListener = GetComponentInChildren<AudioListener>(true);
        if (fallbackAudioListener == null)
        {
            GameObject listenerObject = new GameObject("FallbackAudioListener");
            listenerObject.transform.SetParent(transform, false);
            fallbackAudioListener = listenerObject.AddComponent<AudioListener>();
        }

        UpdateAudioListenerState();
    }

    private void UpdateAudioListenerState()
    {
        if (fallbackAudioListener == null)
        {
            return;
        }

        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        bool hasSceneListener = false;

        foreach (AudioListener listener in listeners)
        {
            if (listener != null && listener != fallbackAudioListener && listener.enabled && listener.gameObject.activeInHierarchy)
            {
                hasSceneListener = true;
                break;
            }
        }

        fallbackAudioListener.enabled = !hasSceneListener;
    }

    private AudioSource CreateAudioSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void ApplyVolumes()
    {
        SetMusicVolume(musicVolume);
        SetSFXVolume(sfxVolume);
    }

    private void LoadSavedVolumes()
    {
        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, sfxVolume);
    }

    private IEnumerator PlayRandomMusicRoutine()
    {
        while (HasTracks(backgroundMusicTracks))
        {
            AudioClip nextTrack = GetRandomTrack(backgroundMusicTracks, lastMusicTrack);

            if (nextTrack == null)
            {
                yield return null;
                continue;
            }

            lastMusicTrack = nextTrack;
            musicSource.clip = nextTrack;
            musicSource.loop = false;
            musicSource.volume = musicVolume;
            musicSource.Play();
            yield return null;

            if (musicSource == null || musicSource.clip != nextTrack || !musicSource.isPlaying)
            {
                yield return new WaitForSeconds(AudioRetryDelay);
                continue;
            }

            while (musicSource != null && musicSource.clip == nextTrack && musicSource.isPlaying)
            {
                yield return null;
            }
        }

        musicPlaybackRoutine = null;
        PlayMusic();
    }

    private IEnumerator PlayRandomFarmSFXRoutine()
    {
        while (HasTracks(farmSFXTracks))
        {
            AudioClip nextTrack = GetRandomTrack(farmSFXTracks, lastFarmSFXTrack);

            if (nextTrack == null)
            {
                yield return null;
                continue;
            }

            lastFarmSFXTrack = nextTrack;
            sfxSource.clip = nextTrack;
            sfxSource.loop = false;
            sfxSource.volume = sfxVolume;
            sfxSource.Play();
            yield return null;

            if (sfxSource == null || sfxSource.clip != nextTrack || !sfxSource.isPlaying)
            {
                yield return new WaitForSeconds(AudioRetryDelay);
                continue;
            }

            while (sfxSource != null && sfxSource.clip == nextTrack && sfxSource.isPlaying)
            {
                yield return null;
            }
        }

        farmSFXPlaybackRoutine = null;
        PlayFarmSFX();
    }

    private AudioClip GetRandomTrack(AudioClip[] tracks, AudioClip previousTrack)
    {
        int validTrackCount = CountValidTracks(tracks);

        if (validTrackCount == 0)
        {
            return null;
        }

        if (validTrackCount == 1)
        {
            return GetValidTrackAt(tracks, 0);
        }

        AudioClip selectedTrack;

        do
        {
            selectedTrack = GetValidTrackAt(tracks, Random.Range(0, validTrackCount));
        }
        while (selectedTrack == previousTrack);

        return selectedTrack;
    }

    private bool HasTracks(AudioClip[] tracks)
    {
        return CountValidTracks(tracks) > 0;
    }

    private int CountValidTracks(AudioClip[] tracks)
    {
        if (tracks == null)
        {
            return 0;
        }

        int count = 0;

        foreach (AudioClip track in tracks)
        {
            if (track != null)
            {
                count++;
            }
        }

        return count;
    }

    private AudioClip GetValidTrackAt(AudioClip[] tracks, int index)
    {
        if (tracks == null)
        {
            return null;
        }

        int currentIndex = 0;

        foreach (AudioClip track in tracks)
        {
            if (track == null)
            {
                continue;
            }

            if (currentIndex == index)
            {
                return track;
            }

            currentIndex++;
        }

        return null;
    }

    private void StopMusicRoutine()
    {
        if (musicPlaybackRoutine == null)
        {
            return;
        }

        StopCoroutine(musicPlaybackRoutine);
        musicPlaybackRoutine = null;
    }

    private void StopFarmSFXRoutine()
    {
        if (farmSFXPlaybackRoutine == null)
        {
            return;
        }

        StopCoroutine(farmSFXPlaybackRoutine);
        farmSFXPlaybackRoutine = null;
    }

    private IEnumerator ScanButtonsRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(ButtonScanInterval);

        while (true)
        {
            RegisterSceneButtons();
            yield return wait;
        }
    }

    private void RegisterSceneButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button == null || !button.gameObject.scene.IsValid())
            {
                continue;
            }

            if (button.GetComponent<UIButtonSound>() == null)
            {
                // This keeps existing Button.onClick listeners untouched.
                button.gameObject.AddComponent<UIButtonSound>();
            }
        }
    }
}
