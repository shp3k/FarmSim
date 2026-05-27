using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public static event Action<int> OnMoneyChanged;

    [Header("Economy")]
    public int money = 100;
    public int totalRunEarnings;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyStartingMoneyIfNewRun();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void AddMoney(int amount)
    {
        AddMoney(amount, true);
    }

    public void AddMoney(int amount, bool countAsRunEarnings)
    {
        money += amount;
        if (countAsRunEarnings && amount > 0)
        {
            totalRunEarnings += amount;
        }

        Debug.Log("Money: " + money);
        NotifyMoneyChanged();
        SaveManager.Instance?.SaveGame();
    }

    public void SpendMoney(int amount)
    {
        money -= amount;
        Debug.Log("Money: " + money);
        NotifyMoneyChanged();
        SaveManager.Instance?.SaveGame();
    }

    public void SetMoneyFromSave(int loadedMoney)
    {
        SetMoneyFromSave(loadedMoney, totalRunEarnings);
    }

    public void SetMoneyFromSave(int loadedMoney, int loadedTotalRunEarnings)
    {
        money = loadedMoney;
        totalRunEarnings = Mathf.Max(0, loadedTotalRunEarnings);
        Debug.Log("Money loaded: " + money);
        NotifyMoneyChanged();
    }

    public void ResetForNewRun(int startingMoney)
    {
        money = Mathf.Max(0, startingMoney);
        totalRunEarnings = 0;
        Debug.Log("Run reset. Money: " + money);
        NotifyMoneyChanged();
    }

    private void ApplyStartingMoneyIfNewRun()
    {
        if (totalRunEarnings > 0)
        {
            return;
        }

        PrestigeManager prestigeManager = PrestigeManager.Instance ?? FindFirstObjectByType<PrestigeManager>();
        if (prestigeManager != null)
        {
            money = Mathf.Max(money, prestigeManager.GetStartingMoney());
        }
    }

    private void NotifyMoneyChanged()
    {
        OnMoneyChanged?.Invoke(money);
    }
}
