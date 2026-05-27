using System;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    [Header("Time Settings")]
    [Tooltip("How long one in-game day lasts, in minutes.")]
    [SerializeField] [Min(0.1f)] private float dayDurationMinutes = 5f;

    private int currentDay = 1;
    private float dayProgressSeconds;
    private long lastClockSampleUnixSeconds;
    private bool hasClockSample;

    public int CurrentDay => currentDay;
    public float DayDurationMinutes => dayDurationMinutes;
    public float DayProgressSeconds => dayProgressSeconds;

    public static event Action<int> OnNewDay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (FindFirstObjectByType<TimeManager>() != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("TimeManager");
        managerObject.AddComponent<TimeManager>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SampleClock(resetSampleOnly: true);
    }

    private void Update()
    {
        AdvanceByRealTime();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            AdvanceByRealTime();
            return;
        }

        SampleClock(resetSampleOnly: true);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            AdvanceByRealTime();
        }
    }

    public void SetDayFromSave(int loadedDay, float loadedDayProgressSeconds = 0f)
    {
        float fullDaySeconds = GetDayDurationSeconds();
        currentDay = Mathf.Max(1, loadedDay);
        dayProgressSeconds = Mathf.Clamp(loadedDayProgressSeconds, 0f, fullDaySeconds);
        SampleClock(resetSampleOnly: true);
        Debug.Log($"Day loaded: {currentDay}");
    }

    public void ResetRunTime()
    {
        currentDay = 1;
        dayProgressSeconds = 0f;
        SampleClock(resetSampleOnly: true);
        Debug.Log("Run time reset.");
    }

    private void AdvanceByRealTime()
    {
        long nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!hasClockSample)
        {
            lastClockSampleUnixSeconds = nowUnixSeconds;
            hasClockSample = true;
            return;
        }

        long deltaSeconds = nowUnixSeconds - lastClockSampleUnixSeconds;
        if (deltaSeconds <= 0)
        {
            return;
        }

        lastClockSampleUnixSeconds = nowUnixSeconds;
        dayProgressSeconds += deltaSeconds;

        float fullDaySeconds = GetDayDurationSeconds();
        while (dayProgressSeconds >= fullDaySeconds)
        {
            dayProgressSeconds -= fullDaySeconds;
            currentDay++;
            OnNewDay?.Invoke(currentDay);
        }
    }

    private void SampleClock(bool resetSampleOnly)
    {
        if (!resetSampleOnly)
        {
            AdvanceByRealTime();
        }

        lastClockSampleUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        hasClockSample = true;
    }

    private float GetDayDurationSeconds()
    {
        return Mathf.Max(1f, dayDurationMinutes * 60f);
    }
}
