using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CoinManager : MonoBehaviour
{
    public static CoinManager I { get; private set; }

    /// סה״כ מטבעות (נשמרים בין ריצות – לפי איך שתנהלי)
    public int TotalCoins => _totalCoins;

    /// מטבעות בריצה הנוכחית (אפס בכל Reload של סצנה)
    public int RunCoins => _runCoins;

    /// (run, total)
    public event Action<int, int> OnChanged;

    [Header("Settings")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("Debug (optional)")]
    [SerializeField] private bool logEvents = false;

    private int _totalCoins = 0;
    private int _runCoins = 0;

    private void Awake()
    {
        // Singleton
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

        // נאפס ריצת מטבעות בכל טעינת סצנה
        SceneManager.sceneLoaded += OnSceneLoaded;

        // חשוב: לירות אירוע גם באתחול כדי שה-UI יראה ערכים התחלתיים
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
        // בכל טעינת סצנה – מתחילים ריצה חדשה
        ResetRun();
    }

    /// הוספת מטבעות לריצה הנוכחית ולסך-הכל
    public void Add(int amount)
    {
        if (amount == 0) return;

        _runCoins   += amount;
        _totalCoins += amount;
        FireChanged();
    }

    /// איפוס מונה הריצה (לא נוגע ב-Total)
    public void ResetRun()
    {
        _runCoins = 0;
        FireChanged();
    }

    /// אם את טוענת/שומרת סה״כ ממקור חיצוני
    public void SetTotal(int newTotal)
    {
        _totalCoins = Mathf.Max(0, newTotal);
        FireChanged();
    }

    /// במידה ואת רוצה להפחית/להוסיף רק לסה״כ ללא השפעה על Run
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