using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SharedBankAccount : MonoBehaviour
{
    // ==================================================
    // SINGLETON
    // ==================================================

    public static SharedBankAccount Instance { get; private set; }

    // ==================================================
    // BANK BALANCE
    // ==================================================

    [Header("Shared Bank Balance")]
    [SerializeField, Min(0)]
    private int currentBalance;

    // ==================================================
    // SETTINGS
    // ==================================================

    [Header("Persistence")]
    [Tooltip(
        "Aktivieren, wenn dieses Objekt beim Szenenwechsel " +
        "bestehen bleiben soll."
    )]
    [SerializeField]
    private bool persistBetweenScenes = true;

    // ==================================================
    // PUBLIC VALUES
    // ==================================================

    public int Balance => currentBalance;

    public bool HasMoney =>
        currentBalance > 0;

    // ==================================================
    // EVENTS
    // ==================================================

    public event Action<int> BalanceChanged;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        currentBalance =
            Mathf.Max(0, currentBalance);

        if (persistBetweenScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        currentBalance =
            Mathf.Max(0, currentBalance);
    }

    // ==================================================
    // ADD MONEY
    // ==================================================

    public bool AddFunds(int amount)
    {
        if (amount <= 0)
            return false;

        currentBalance += amount;

        BalanceChanged?.Invoke(
            currentBalance
        );

        return true;
    }

    public bool AddFunds(float amount)
    {
        int roundedAmount =
            Mathf.RoundToInt(amount);

        return AddFunds(roundedAmount);
    }

    // ==================================================
    // SPEND MONEY
    // ==================================================

    public bool CanAfford(int amount)
    {
        if (amount < 0)
            return false;

        return currentBalance >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
            return false;

        if (!CanAfford(amount))
            return false;

        currentBalance -= amount;

        BalanceChanged?.Invoke(
            currentBalance
        );

        return true;
    }

    // ==================================================
    // SET BALANCE
    // ==================================================

    public void SetBalance(int amount)
    {
        int newBalance =
            Mathf.Max(0, amount);

        if (newBalance == currentBalance)
            return;

        currentBalance = newBalance;

        BalanceChanged?.Invoke(
            currentBalance
        );
    }

    public void ClearBalance()
    {
        SetBalance(0);
    }

    // ==================================================
    // SAVE / LOAD SUPPORT
    // ==================================================

    /*
     * Diese Methoden sind für den späteren SaveManager
     * und das Multiplayer-System gedacht.
     *
     * SharedBankAccount speichert absichtlich noch nicht
     * selbst mit PlayerPrefs.
     */

    public int GetBalanceForSave()
    {
        return currentBalance;
    }

    public void LoadBalance(int savedBalance)
    {
        SetBalance(savedBalance);
    }
}