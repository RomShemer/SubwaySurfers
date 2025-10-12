using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI hiScoreText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private CanvasGroup fadeOverlay;

    [Header("Countdown (visual only)")]
    [SerializeField] private float countdownSeconds = 3f;

    [Header("Scoring")]
    [SerializeField] private float scoreRate = 10f;

    private int currentScore = 0;
    private int highScore = 0;
    private float scoreCounter = 0f;
    private Coroutine countdownRoutine;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // אם את רוצה שה-UI ישרוד בין סצנות:
        // DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        EnsureSingleEventSystemOnce();

        if (startButton)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartClicked);
        }
        if (exitButton)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitClicked);
        }

        // עוצרים זמן עד START כדי שהקאונטר של ה-GameManager לא יתקדם
        Time.timeScale = 0f;

        PrepareForNewRun();
    }

    private void Update()
    {
        if (Time.timeScale > 0.0f)
        {
            scoreCounter += Time.deltaTime * scoreRate;
            currentScore = (int)scoreCounter;

            if (scoreText) scoreText.text = currentScore.ToString("D6");

            if (currentScore > highScore)
            {
                highScore = currentScore;
                PlayerPrefs.SetInt("HighScore", highScore);
                PlayerPrefs.Save();
                if (hiScoreText) hiScoreText.text = $"HI {highScore:D6}";
            }
        }
    }

    // -------- Start / Restart --------

    public void OnStartClicked()
    {
        if (IsInRestartMode())
        {
            // ✅ את הריסטארט עושה ה-GameManager
            Time.timeScale = 1f; // ליתר ביטחון
            GameManager.Instance?.RestartScene();
        }
        else
        {
            if (startButton) startButton.gameObject.SetActive(false);
            if (countdownRoutine != null) StopCoroutine(countdownRoutine);
            countdownRoutine = StartCoroutine(PlayCountdownAnimation(countdownSeconds));
        }
    }

    public void OnExitClicked()
    {
       /* if (IsInRestartMode())
        {
            // ✅ את הריסטארט עושה ה-GameManager
            Time.timeScale = 1f; // ליתר ביטחון
            GameManager.Instance?.RestartScene();
        }
        else
        {
            if (exitButton) startButton.gameObject.SetActive(false);
            if (countdownRoutine != null) StopCoroutine(countdownRoutine);
            countdownRoutine = StartCoroutine(PlayCountdownAnimation(countdownSeconds));
        } */

       if (exitButton)
       {
           startButton.gameObject.SetActive(true);
           exitButton.gameObject.SetActive(false);
       }
       
        Time.timeScale = 0f;
        GameManager.Instance?.RestartScene();
    }

    private IEnumerator PlayCountdownAnimation(float seconds)
    {
        if (fadeOverlay) StartCoroutine(FadeCanvas(fadeOverlay, fadeOverlay.alpha, 0.6f, 0.25f));

        // READY
        if (statusText)
        {
            statusText.text = "READY";
            statusText.alpha = 0f;
            yield return StartCoroutine(FadeText(statusText, 0f, 1f, 0.35f));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return StartCoroutine(FadeText(statusText, 1f, 0f, 0.25f));
        }

        // משחרר זמן – עכשיו ה-GameManager יתחיל את הספירה שלו ויפתח תנועה כשהיא תסתיים
        Time.timeScale = 1f;

        // 3..2..1 על זמן אמיתי
        float t = seconds;
        while (t > 0f)
        {
            if (statusText)
            {
                statusText.text = Mathf.CeilToInt(t).ToString();
                statusText.alpha = 0f;
                yield return StartCoroutine(FadeText(statusText, 0f, 1f, 0.2f));
                yield return new WaitForSecondsRealtime(0.6f);
                yield return StartCoroutine(FadeText(statusText, 1f, 0f, 0.2f));
            }
            t -= 1f;
        }

        // GO!
        if (statusText)
        {
            statusText.text = "GO!";
            statusText.alpha = 0f;
            yield return StartCoroutine(FadeText(statusText, 0f, 1f, 0.2f));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return StartCoroutine(FadeText(statusText, 1f, 0f, 0.3f));
            statusText.text = "";
        }

        if (fadeOverlay) StartCoroutine(FadeCanvas(fadeOverlay, fadeOverlay.alpha, 0f, 0.5f));
        countdownRoutine = null;
    }
    

    // -------- Game Over --------

    public void ShowGameOverUI()
    {
        bool gotNewHighScore = currentScore >= PlayerPrefs.GetInt("HighScore", 0);
        if (statusText)
        {
            statusText.text = gotNewHighScore
                ? $"GAME OVER!\nNEW HIGH SCORE: {currentScore:D6}"
                : $"GAME OVER!\nSCORE: {currentScore:D6}";
            statusText.alpha = 1f;
        }

        if (startButton)
        {
            //SetStartButtonLabel("RESTART");
            //startButton.gameObject.SetActive(true);
            startButton.gameObject.SetActive(false);
        }
        
        if(exitButton) 
            exitButton.gameObject.SetActive(true);


        if (fadeOverlay) StartCoroutine(FadeCanvas(fadeOverlay, fadeOverlay.alpha, 0.35f, 0.3f));
    }

    // -------- Prepare New Run --------

    public void PrepareForNewRun()
    {
        currentScore = 0;
        scoreCounter = 0f;

        if (scoreText) scoreText.text = "000000";
        highScore = PlayerPrefs.GetInt("HighScore", 0);
        if (hiScoreText) hiScoreText.text = $"HI {highScore:D6}";

        if (statusText) { statusText.text = ""; statusText.alpha = 1f; }
        if (fadeOverlay) fadeOverlay.alpha = 0f;

        if (startButton)
        {
            SetStartButtonLabel("START");
            startButton.gameObject.SetActive(true);
            startButton.interactable = true;
        }
    }

    // -------- Helpers --------

    private bool IsInRestartMode()
    {
        if (!startButton) return false;
        var tmp = startButton.GetComponentInChildren<TextMeshProUGUI>();
        return tmp && tmp.text.Trim().ToUpper().Contains("RESTART");
    }

    private void SetStartButtonLabel(string text)
    {
        if (!startButton) return;
        var tmp = startButton.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp) tmp.text = text;
    }

    private IEnumerator FadeText(TMP_Text text, float from, float to, float duration)
    {
        float e = 0f;
        while (e < duration)
        {
            e += Time.unscaledDeltaTime;
            if (text) text.alpha = Mathf.Lerp(from, to, e / duration);
            yield return null;
        }
        if (text) text.alpha = to;
    }

    private IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float duration)
    {
        float e = 0f;
        while (e < duration)
        {
            e += Time.unscaledDeltaTime;
            if (cg) cg.alpha = Mathf.Lerp(from, to, e / duration);
            yield return null;
        }
        if (cg) cg.alpha = to;
    }

    private bool _ensuredEventSystem = false;
    private void EnsureSingleEventSystemOnce()
    {
        if (_ensuredEventSystem) return;
        _ensuredEventSystem = true;

        var all = FindObjectsOfType<UnityEngine.EventSystems.EventSystem>(includeInactive: true);
        if (all.Length <= 1) return;
        for (int i = 1; i < all.Length; i++)
            Destroy(all[i].gameObject);

        Debug.LogWarning($"[GameUIManager] Removed extra EventSystem(s). Kept one, destroyed {all.Length - 1}.");
    }
}