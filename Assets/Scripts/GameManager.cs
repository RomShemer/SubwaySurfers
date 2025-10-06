using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Countdown")]
    [SerializeField] private float countdownTime = 3.0f;
    private float countdown;
    private bool isCountdownInProgress = true;
    [SerializeField] private float fadeOpacity = 0.2f;

    [Header("Refs (שייך באינספקטור אם אפשר)")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private ShaderController shaderController;

    [Header("State")]
    [SerializeField] private bool canMove = false;
    [SerializeField] private bool _isInputDisabled = false;

    public bool CanMove { get => canMove; set => canMove = value; }
    public bool IsInputDisabled { get => _isInputDisabled; set => _isInputDisabled = value; }

    private void Awake()
    {
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

        if (!shaderController)
        {
            var curve = GameObject.Find("CurveLevel");
            if (curve)
            {
                shaderController = curve.GetComponent<ShaderController>();
                if (!shaderController)
                    Debug.LogError("[GameManager] 'CurveLevel' found but missing ShaderController component.", curve);
            }
            else
            {
                Debug.LogError("[GameManager] No GameObject named 'CurveLevel' found in scene.", this);
            }
        }
    }

    void Start()
    {
        countdown = countdownTime;
    }

    void Update()
    {
        // אם חסר רפרנס קריטי – לא נתקדם
        if (!playerAnimator)
        {
            // הודעה חד־פעמית מספיקה; השארנו לוגים ב-Awake.
            return;
        }

        if (!canMove && countdown > 0f)
        {
            countdown -= Time.deltaTime;

            // נבטיח שלא נזרוק NRE גם אם animator לא קיים
            if (playerAnimator) playerAnimator.enabled = false;

            if (countdown <= 0f)
            {
                canMove = true;
                if (playerAnimator) playerAnimator.enabled = true;
                countdown = countdownTime;
            }
        }
        else
        {
            isCountdownInProgress = false;
        }
    }

    void OnGUI()
    {
        // UI ישן – שומר כמו שהיה
        GUIStyle countdownStyle = new GUIStyle(GUI.skin.GetStyle("label"));
        countdownStyle.fontSize = 50;
        countdownStyle.normal.textColor = Color.white;

        if (!canMove)
        {
            GUI.Label(new Rect(Screen.width / 2, Screen.height / 2, 200, 500), Mathf.Round(countdown).ToString(), countdownStyle);
        }

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 30;

        Rect buttonRect = _isInputDisabled ? new Rect((Screen.width - 200) / 2, (Screen.height - 100) / 2, 200, 100)
                                           : new Rect(Screen.width - 110, 10, 100, 50);

        if (!_isInputDisabled)
        {
            buttonStyle.fontSize = 15;
            buttonStyle.normal.background = Texture2D.linearGrayTexture;
        }

        if (GUI.Button(buttonRect, "RESTART", buttonStyle))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        if (isCountdownInProgress)
        {
            GUI.color = new Color(0f, 0f, 0f, fadeOpacity);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
    }

    public void EndGame()
    {
        if (shaderController) shaderController.enabled = false;
        _isInputDisabled = true;
        if (playerAnimator) playerAnimator.SetBool("isInputDisabled", true);
    }
}
