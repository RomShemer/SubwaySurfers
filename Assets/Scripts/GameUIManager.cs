using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI hiScoreText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button startButton;
    [SerializeField] private CanvasGroup fadeOverlay;

    [Header("Score Settings")]
    [SerializeField] private float scoreRate = 10f;
    private int currentScore = 0;
    private int highScore = 0;
    private float scoreCounter = 0f;
    private bool isCountingScore = false;
    private Coroutine countdownRoutine;

    private void Start()
    {
        if (startButton)
            startButton.onClick.AddListener(OnStartButtonClicked);

        if (statusText)
            statusText.text = "";

        if (fadeOverlay)
            fadeOverlay.alpha = 0f;

        if (scoreText)
            scoreText.text = currentScore.ToString("D6");

        highScore = PlayerPrefs.GetInt("HighScore", 0);
        if (hiScoreText)
            hiScoreText.text = $"HI {highScore:D6}";
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // ✅ start counting score only when movement is allowed
        if (GameManager.Instance.CanMove && !GameManager.Instance.IsGameEnded)
        {
            isCountingScore = true;
            UpdateScore();
        }
        else
        {
            isCountingScore = false;
        }

        if (GameManager.Instance.IsGameEnded)
        {
            ShowGameOverUI();
        }
    }

    private void UpdateScore()
    {
        scoreCounter += Time.deltaTime * scoreRate;
        currentScore = (int)scoreCounter;

        if (scoreText)
            scoreText.text = currentScore.ToString("D6");

        if (currentScore > highScore)
        {
            highScore = currentScore;
            PlayerPrefs.SetInt("HighScore", highScore);
            PlayerPrefs.Save();

            if (hiScoreText)
                hiScoreText.text = $"HI {highScore:D6}";
        }
    }

    private void OnStartButtonClicked()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.IsGameEnded)
        {
            gm.RestartGame();
        }
        else if (!gm.IsGameStarted)
        {
            gm.StartCountdown();
            startButton.gameObject.SetActive(false);

            if (countdownRoutine != null)
                StopCoroutine(countdownRoutine);

            countdownRoutine = StartCoroutine(PlayCountdownAnimation(gm.CurrentCountdown));
        }
    }

    private IEnumerator PlayCountdownAnimation(float startTime)
    {
        var gm = GameManager.Instance;
        if (gm == null) yield break;

        if (fadeOverlay)
            StartCoroutine(FadeCanvas(fadeOverlay, fadeOverlay.alpha, 0.6f, 0.5f));

        // READY
        if (statusText)
        {
            statusText.text = "READY";
            statusText.alpha = 0f;
            yield return StartCoroutine(FadeText(statusText, 0f, 1f, 0.5f));
            yield return new WaitForSeconds(0.7f);
            yield return StartCoroutine(FadeText(statusText, 1f, 0f, 0.5f));
        }

        // COUNTDOWN 3, 2, 1
        float timer = startTime;
        while (timer > 0f && gm.IsCountdownInProgress)
        {
            if (statusText)
            {
                statusText.text = Mathf.CeilToInt(timer).ToString();
                statusText.alpha = 0f;
                yield return StartCoroutine(FadeText(statusText, 0f, 1f, 0.3f));
                yield return new WaitForSeconds(0.5f);
                yield return StartCoroutine(FadeText(statusText, 1f, 0f, 0.3f));
            }
            timer -= 1f;
        }

        // GO!
        if (statusText)
        {
            statusText.text = "GO!";
            statusText.alpha = 0f;
            yield return StartCoroutine(FadeText(statusText, 0f, 1f, 0.3f));
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(FadeText(statusText, 1f, 0f, 0.6f));
        }

        // Fade out overlay and clear text
        if (fadeOverlay)
            StartCoroutine(FadeCanvas(fadeOverlay, fadeOverlay.alpha, 0f, 0.8f));

        if (statusText)
            statusText.text = "";

        // ✅ Only now enable player movement
        gm.EnableMovement();
    }

    private IEnumerator FadeText(TMP_Text text, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (text)
                text.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        if (text)
            text.alpha = to;
    }

    private IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (cg)
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        if (cg)
            cg.alpha = to;
    }

    public void ShowGameOverUI()
    {
        if (!startButton.gameObject.activeSelf)
        {
            bool gotNewHighScore = currentScore >= PlayerPrefs.GetInt("HighScore", 0);

            if (statusText)
            {
                if (gotNewHighScore)
                    statusText.text = $"GAME OVER!\nNEW HIGH SCORE: {currentScore:D6}";
                else
                    statusText.text = $"GAME OVER!\nSCORE: {currentScore:D6}";
            }

            startButton.gameObject.SetActive(true);
            var buttonText = startButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText)
                buttonText.text = "RESTART";
        }
    }

    private void OnDestroy()
    {
        if (startButton)
            startButton.onClick.RemoveListener(OnStartButtonClicked);
    }
}
