using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Countdown")]
    [SerializeField] private float countdownTime = 3.0f;
    private float countdown;
    private bool isCountdownInProgress = true;
    [SerializeField] private float fadeOpacity = 0.2f;

    [Header("Refs (שייך באינספקטור אם אפשר)")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private ShaderController shaderController;
    [SerializeField] private CoinManager coinManager;
    [SerializeField] private InfiniteRoad infiniteRoad;   // ← הוספתי

    [Header("State")]
    [SerializeField] private bool canMove = false;
    [SerializeField] private bool _isInputDisabled = false;

    
    [Header("Audio")]
    [SerializeField] private AudioSource backgroundMusic;
    
    public bool CanMove { get => canMove; set => canMove = value; }
    public bool IsInputDisabled { get => _isInputDisabled; set => _isInputDisabled = value; }

    // --- התחברות לאירוע טעינת סצנה כדי לבצע Rebind ---
    private void OnEnable()  { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        RebindSceneRefs();     // למצוא מחדש את האובייקטים של הסצנה
        ResetStateForRun();    // להחזיר למצב ההתחלתי שלך
    }

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); 
        
        // השארתי את האוטו-חיפוש המקורי שלך, אבל נעשה אותו גם ב-RebindSceneRefs
        RebindSceneRefs();
    }

    void Start()
    {
        ResetStateForRun();
    }

    private void ResetStateForRun()
    {
        countdown = countdownTime;
        isCountdownInProgress = true;
        canMove = false;
        _isInputDisabled = false;

        if (playerAnimator) playerAnimator.enabled = false;

        // רישום שחקן אצל הספונר (אם יש סינגלטון)
        if (playerController) ObstacleAndTrainSpawner.I?.RegisterPlayer(playerController.transform);

        if(backgroundMusic && backgroundMusic.isPlaying) backgroundMusic.Stop(); 
        // אם חשוב לך לבנות כביש מחדש גם בלי טעינה:
        // if (infiniteRoad) infiniteRoad.RebuildInitialRoad();
    }

    void Update()
    {
        // נסה להשלים רפרנסים אם משהו חסר (במקום return;)
        if (!playerController || !playerAnimator || !infiniteRoad || !coinManager)
            RebindSceneRefs();

        if (!playerAnimator)
        {
            // עדיין חסר? נחכה לפריים הבא, אבל לא נצא מוקדם בלי ניסיון
            return;
        }

        if (!canMove && countdown > 0f)
        {
            countdown -= Time.deltaTime;

            if (playerAnimator) playerAnimator.enabled = false;

            if (countdown <= 0f)
            {
                canMove = true;
                if (playerAnimator) playerAnimator.enabled = true;
                if(backgroundMusic && !backgroundMusic.isPlaying) backgroundMusic.Play(); 
                countdown = countdownTime;
            }
        }
        else
        {
            isCountdownInProgress = false;
        }
    }
    
    /*     void OnGUI()
       {
           /*
           // ה-UI הישן שלך כפי שהיה
           GUIStyle countdownStyle = new GUIStyle(GUI.skin.GetStyle("label"));
           countdownStyle.fontSize = 50;
           countdownStyle.normal.textColor = Color.white; 

           if (!canMove)
           {
               GUI.Label(new Rect(Screen.width / 2, Screen.height / 2, 200, 500),
                         Mathf.Round(countdown).ToString(), countdownStyle);
           }* /

           GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 30 };

           Rect buttonRect = _isInputDisabled
               ? new Rect((Screen.width - 200) / 2, (Screen.height - 100) / 2, 200, 100)
               : new Rect(Screen.width - 110, 10, 100, 50);

           if (!_isInputDisabled)
           {
               buttonStyle.fontSize = 15;
               buttonStyle.normal.background = Texture2D.linearGrayTexture;
           }
           
           //if(GUI.Button(buttonRect, "RESTART", buttonStyle))
           Button restartButton = GameUIManager.Instance.getRestartButton();
           if (!restartButton )
           {
               // חשוב: איפוס לפני טעינה
               ObstacleAndTrainSpawner.I?.OnGameRestart();
               CoinManager.I?.ResetRun();

               SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
               // OnSceneLoaded יעשה Rebind + ResetStateForRun
           }

           if (isCountdownInProgress)
           {
               GUI.color = new Color(0f, 0f, 0f, fadeOpacity);
               GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
           }
       } */

    public void EndGame()
    {
        if (shaderController) shaderController.enabled = false;
        _isInputDisabled = true;
        if (playerAnimator)
        {
            playerAnimator.SetBool("isInputDisabled", true);
            //check if guard animation ended
            GameUIManager.Instance.ShowGameOverUI();
        }
        
        if(backgroundMusic && backgroundMusic.isPlaying) backgroundMusic.Stop(); 

    }

    // --------- כאן אנחנו מוצאים מחדש רפרנסים אחרי טעינה / או אם נעלמו ---------
    private void RebindSceneRefs()
    {
        if (!playerController)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO) playerController = playerGO.GetComponent<PlayerController>();
        }

        if (!playerAnimator && playerController)
            playerAnimator = playerController.GetComponent<Animator>();

        if (!shaderController)
        {
            var curve = GameObject.Find("CurveLevel");
            if (curve) shaderController = curve.GetComponent<ShaderController>();
        }

        if (!coinManager)
        {
            var coin = GameObject.Find("CoinManager");
            if (coin) coinManager = coin.GetComponent<CoinManager>();
        }

        if (!infiniteRoad)
            infiniteRoad = FindObjectOfType<InfiniteRoad>(includeInactive: true);

        if (!backgroundMusic)
        {
            // חפשי אובייקט בשם "BackgroundMusic" עם AudioSource
            var go = GameObject.Find("BackgroundMusic");
            if (go) backgroundMusic = go.GetComponent<AudioSource>();
        }

        // ואם יש ספונר – לרשום את השחקן שוב (קריטי לתנועה/מסילות וכו')
        if (playerController) ObstacleAndTrainSpawner.I?.RegisterPlayer(playerController.transform);
    }
    
    public void RestartScene()
    {
        // מה שעשית ב-OnGUI – פשוט עטוף בפונקציה
        ObstacleAndTrainSpawner.I?.OnGameRestart();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        CoinManager.I?.ResetRun();
    }
    
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}