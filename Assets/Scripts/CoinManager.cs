using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CoinManager : MonoBehaviour
{
    public static CoinManager I { get; private set; }

    public int TotalCoins => _totalCoins;
    public int RunCoins   => _runCoins;  // נספרים לראן הנוכחי

    public event Action<int,int> OnChanged; // (run, total)

    [SerializeField] private bool persistAcrossScenes = true;

    private int _totalCoins = 0;
    private int _runCoins   = 0;

    private void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += (_, __) => ResetRun(); // איפוס לראן חדש
    }

    public void Add(int amount)
    {
        _runCoins   += amount;
        _totalCoins += amount;
        OnChanged?.Invoke(_runCoins, _totalCoins);
    }

    public void ResetRun()
    {
        _runCoins = 0;
        OnChanged?.Invoke(_runCoins, _totalCoins);
    }
}