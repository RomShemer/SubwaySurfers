using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CoinManager : MonoBehaviour
{
    public static CoinManager I { get; private set; }

    public int TotalCoins => _totalCoins;

    public int RunCoins => _runCoins;

    public event Action<int, int> OnChanged;

    [Header("Settings")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("Debug (optional)")]
    [SerializeField] private bool logEvents = false;

    private int _totalCoins = 0;
    private int _runCoins = 0;

    private void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        FireChanged();
    }

    private void OnDestroy()
    {
        if (I == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            I = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetRun();
    }

    public void Add(int amount)
    {
        if (amount == 0) return;

        _runCoins   += amount;
        _totalCoins += amount;
        FireChanged();
    }

    public void ResetRun()
    {
        _runCoins = 0;
        FireChanged();
    }

    public void SetTotal(int newTotal)
    {
        _totalCoins = Mathf.Max(0, newTotal);
        FireChanged();
    }

    public void AddToTotalOnly(int delta)
    {
        if (delta == 0) return;
        _totalCoins = Mathf.Max(0, _totalCoins + delta);
        FireChanged();
    }

    private void FireChanged()
    {
        if (logEvents) Debug.Log($"[CoinManager] OnChanged -> Run={_runCoins}, Total={_totalCoins}");
        OnChanged?.Invoke(_runCoins, _totalCoins);
    }
}