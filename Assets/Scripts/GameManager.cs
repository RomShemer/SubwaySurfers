using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Countdown")]
    [SerializeField] private float countdownTime = 3.0f;
    private float countdown;
    private bool isCountdownInProgress = true;
    [SerializeField] private float fadeOpacity = 0.2f;

    [Header("Refs")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private ShaderController shaderController;
    [SerializeField] private CoinManager coinManager;
    [SerializeField] private InfiniteRoad infiniteRoad;

    [Header("State")]
    [SerializeField] private bool canMove = false;
    [SerializeField] private bool _isInputDisabled = false;
    
    [Header("Audio")]
    [SerializeField] private AudioSource backgroundMusic;

    public bool CanMove { get => canMove; set => canMove = value; }
    public bool IsInputDisabled { get => _isInputDisabled; set => _isInputDisabled = value; }

    private bool _prevCanMove = false;

    private void OnEnable()  { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        RebindSceneRefs();
        ResetStateForRun();
    }

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); 
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
        _prevCanMove = false;
        _isInputDisabled = false;

        if (playerAnimator) playerAnimator.enabled = false;
        if (playerController) ObstacleAndTrainSpawner.I?.RegisterPlayer(playerController.transform);

        if (backgroundMusic && backgroundMusic.isPlaying)
            backgroundMusic.Stop();
    }

    void Update()
    {
        if (!playerController || !playerAnimator || !infiniteRoad || !coinManager)
            RebindSceneRefs();

        if (!playerAnimator) return;

        if (!canMove && countdown > 0f)
        {
            countdown -= Time.deltaTime;
            if (playerAnimator) playerAnimator.enabled = false;

            if (countdown <= 0f)
            {
                BeginRun();
                countdown = countdownTime;
            }
        }
        else
        {
            isCountdownInProgress = false;
        }

        // גילוי שינוי מצב מבחוץ (למשל כפתור Start)
        if (!_prevCanMove && canMove)
        {
            OnRunStarted();
        }
        _prevCanMove = canMove;
    }

    private void BeginRun()
    {
        canMove = true;
        if (playerAnimator) playerAnimator.enabled = true;
        OnRunStarted();
    }

    private void OnRunStarted()
    {
        AudioListener.pause = false;

        if (backgroundMusic)
        {
            if (!backgroundMusic.isPlaying)
                backgroundMusic.Play();
        }
    }

    public void EndGame()
    {
        if (shaderController) shaderController.enabled = false;
        _isInputDisabled = true;

        if (playerAnimator)
        {
            playerAnimator.SetBool("isInputDisabled", true);
            GameUIManager.Instance.ShowGameOverUI();
        }

        if (backgroundMusic && backgroundMusic.isPlaying)
            backgroundMusic.Stop();
    }

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
            var goByName = GameObject.Find("BackgroundMusic");
            if (goByName) backgroundMusic = goByName.GetComponent<AudioSource>();

            if (!backgroundMusic)
            {
                var all = Resources.FindObjectsOfTypeAll<AudioSource>();
                foreach (var a in all)
                {
                    if (a != null && a.loop)
                    {
                        backgroundMusic = a;
                        break;
                    }
                }
            }
        }

        if (playerController) ObstacleAndTrainSpawner.I?.RegisterPlayer(playerController.transform);
    }

    public void RestartScene()
    {
        ObstacleAndTrainSpawner.I?.OnGameRestart();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        CoinManager.I?.ResetRun();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
