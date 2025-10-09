using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Countdown")]
    [SerializeField] private float countdownTime = 3.0f;
    private float countdown;
    private bool isCountdownInProgress = false; // מתחילים ב-false
    [SerializeField] private float fadeOpacity = 0.2f;

    [Header("Refs (שייך באינספקטור אם אפשר)")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private ShaderController shaderController;
    [SerializeField] private GameUIManager uiManager;

    [Header("State")]
    [SerializeField] private bool canMove = false;
    [SerializeField] private bool _isInputDisabled = false;
    public bool IsGameStarted { get; private set; } = false;
    public bool IsGameEnded { get; private set; } = false;
    public static GameManager Instance { get; private set; }

    public bool CanMove { get => canMove; private set => canMove = value; }
    public bool IsInputDisabled { get => _isInputDisabled; private set => _isInputDisabled = value; }
    public bool IsCountdownInProgress => isCountdownInProgress;
    public float CurrentCountdown => countdown;

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
            return;
        }

        // נסה להשלים רפרנסים אם לא שובצו ידנית
        if (!playerController)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO)
            {
                playerController = playerGO.GetComponent<PlayerController>();
                if (!playerController)
                    Debug.LogError("[GameManager] Player found but missing PlayerController component.", playerGO);
            }
            else
            {
                Debug.LogError("[GameManager] No GameObject with Tag=Player found in scene.", this);
            }
        }

        if (!playerAnimator && playerController)
        {
            playerAnimator = playerController.GetComponent<Animator>();
            if (!playerAnimator)
                Debug.LogError("[GameManager] PlayerController found but missing Animator component.", playerController);
        }

        // אם רוצים, אפשר להחזיר את ShaderController
        // if (!shaderController)
        // {
        //     var curve = GameObject.Find("CurveLevel");
        //     if (curve)
        //     {
        //         shaderController = curve.GetComponent<ShaderController>();
        //         if (!shaderController)
        //             Debug.LogError("[GameManager] 'CurveLevel' found but missing ShaderController component.", curve);
        //     }
        //     else
        //     {
        //         Debug.LogError("[GameManager] No GameObject named 'CurveLevel' found in scene.", this);
        //     }
        // }

        if (!uiManager)
        {
            uiManager = FindObjectOfType<GameUIManager>();
            if (!uiManager)
                Debug.LogWarning("[GameManager] No GameUIManager found in scene.");
        }
    }

    private void Start()
    {
        // כאן אנחנו לא מפעילים את הספירה - מחכים ללחיצה על Start
        countdown = countdownTime;

        canMove = false;
        IsGameStarted = false;
        IsGameEnded = false;
        isCountdownInProgress = false;
    }

    private void Update()
    {
        if (!playerAnimator)
            return;

        if (isCountdownInProgress)
        {
            HandleCountdown();
        }
    }

    private void HandleCountdown()
    {
        if (!CanMove && countdown > 0f)
        {
            countdown -= Time.deltaTime;

            // ודא שהאנימטור כבוי בזמן הספירה
            if (playerAnimator)
                playerAnimator.enabled = false;

            if (countdown <= 0f)
            {
                // הספירה הסתיימה, אבל עדיין לא מפעילים תנועה
                IsGameStarted = true;
                isCountdownInProgress = false;
                countdown = 0f;

                // נשאיר את האנימטור כבוי עד ה-"GO!" מה-UI
                if (playerAnimator)
                    playerAnimator.enabled = false;

                canMove = false;
            }
        }
    }

    public void StartCountdown()
    {
        // נקרא רק מה-UI כשהשחקן לוחץ START
        countdown = countdownTime;
        canMove = false;
        IsGameStarted = false;
        IsGameEnded = false;
        isCountdownInProgress = true;

        // מכבים את האנימטור עד הסוף
        if (playerAnimator)
            playerAnimator.enabled = false;
    }

    public void EnableMovement()
    {
        // נקרא ע"י GameUIManager אחרי שה-"GO!" נגמר
        canMove = true;
        if (playerAnimator)
            playerAnimator.enabled = true;
    }

    public void EndGame()
    {
        if (shaderController)
            shaderController.enabled = false;

        _isInputDisabled = true;
        IsGameEnded = true;

        if (playerAnimator)
            playerAnimator.SetBool("isInputDisabled", true);

        if (uiManager)
            uiManager.ShowGameOverUI();
    }

    public void RestartGame()
    {
        ObstacleAndTrainSpawner.I?.OnGameRestart();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }
}
