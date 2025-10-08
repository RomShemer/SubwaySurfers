using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum Side { Left = -2, Middle = 0, Right = 2 }

public class PlayerController : MonoBehaviour
{
    private Transform myTransform;
    private Animator myAnimator;
    private CharacterController characterController;
    private PlayerCollision playerCollision;
    public GameManager gameManager;

    public CharacterController MyCharacterController { get => characterController; set => characterController = value; }
    public Transform MyTransform { get => myTransform; set => myTransform = value; }

    public int IdStumbleLow { get => _IdStumbleLow; set => _IdStumbleLow = value; }
    public int IdDeathLower { get => _IdDeathLower; set => _IdDeathLower = value; }
    public int IdDeathBounce { get => _IdDeathBounce; set => _IdDeathBounce = value; }
    public int IdDeathMovingTrain { get => _IdDeathMovingTrain; set => _IdDeathMovingTrain = value; }
    public int IdDeathUpper { get => _IdDeathUpper; set => _IdDeathUpper = value; }
    public int IdStumbleCornerRight { get => _IdStumbleCornerRight; set => _IdStumbleCornerRight = value; }
    public int IdStumbleCornerLeft { get => _IdStumbleCornerLeft; set => _IdStumbleCornerLeft = value; }
    public int IdStumbleFall { get => _IdStumbleFall; set => _IdStumbleFall = value; }
    public int IdStumbleOffLeft { get => _IdStumbleOffLeft; set => _IdStumbleOffLeft = value; }
    public int IdStumbleOffRight { get => _IdStumbleOffRight; set => _IdStumbleOffRight = value; }
    public int IdStumbleSideLeft { get => _IdStumbleSideLeft; set => _IdStumbleSideLeft = value; }
    public int IdStumbleSideRight { get => _IdStumbleSideRight; set => _IdStumbleSideRight = value; }

    public Side PreviousXPos { get => _previousXPos; set => _previousXPos = value; }
    public bool IsStumbleTransitionComplete { get => isStumbleTransitionComplete; set => isStumbleTransitionComplete = value; }

    [Header("Action windows")]
    [SerializeField] private float actionGrace = 0.15f;
    private float rollGraceTimer;
    private float jumpGraceTimer;

    // flags יציבים לקוליז'ן
    public bool IsRollingNow => isRolling || rollGraceTimer > 0f;
    public bool IsAirborneOrJumping =>
        !characterController.isGrounded ||
        Mathf.Abs(characterController.velocity.y) > 0.05f ||
        jumpGraceTimer > 0f;

    private Side position;
    private Side _previousXPos;
    private Vector3 motionVector;

    [Header("Player Controller")]
    [SerializeField] private float forwardSpeed = 8f;
    [SerializeField] private float jumpPower = 6f;
    [SerializeField] private float dodgeSpeed = 6f;

    private float rollTimer;
    private float newXPosition;
    private float xPosition, yPosition;

    private int IdDodgeLeft  = Animator.StringToHash("DodgeLeft");
    private int IdDodgeRight = Animator.StringToHash("DodgeRight");
    private int IdJump       = Animator.StringToHash("Jump");
    private int IdFall       = Animator.StringToHash("Fall");
    private int IdLanding    = Animator.StringToHash("Landing");
    private int IdRoll       = Animator.StringToHash("Roll");

    private int _IdStumbleLow         = Animator.StringToHash("StumbleLow");
    private int _IdStumbleCornerRight = Animator.StringToHash("StumbleCornerRight");
    private int _IdStumbleCornerLeft  = Animator.StringToHash("StumbleCornerLeft");
    private int _IdStumbleFall        = Animator.StringToHash("StumbleFall");
    private int _IdStumbleOffLeft     = Animator.StringToHash("StumbleOffLeft");
    private int _IdStumbleOffRight    = Animator.StringToHash("StumbleOffRight");
    private int _IdStumbleSideLeft    = Animator.StringToHash("StumbleSideLeft");
    private int _IdStumbleSideRight   = Animator.StringToHash("StumbleSideRight");
    private int _IdDeathBounce        = Animator.StringToHash("DeathBounce");
    private int _IdDeathLower         = Animator.StringToHash("DeathLower");
    private int _IdDeathMovingTrain   = Animator.StringToHash("DeathMovingTrain");
    private int _IdDeathUpper         = Animator.StringToHash("DeathUpper");

    private bool swipeLeft, swipeRight, swipeUp, swipeDown;

    [Header("Runtime Flags")]
    [SerializeField] private bool isRolling;
    [SerializeField] private bool isJumping;
    [SerializeField] private bool isGrounded;
    [SerializeField, FormerlySerializedAs("_isStumbleTransitionComplete")]
    private bool isStumbleTransitionComplete = false;

    // Guard
    public FollowGuard guard;
    private float _curDistance = 0.6f;

    // Death Flow
    [Header("Death Flow")]
    public bool dead = false;
    public bool canInput = true;
    public string caughtAnimName = "caught1";

    private bool playedCaughtOnce = false;

    void Start()
    {
        position = Side.Middle;
        myTransform = GetComponent<Transform>();
        myAnimator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        playerCollision = GetComponent<PlayerCollision>();
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        yPosition = -7f;
    }

    void Update()
    {
        if (dead)
        {
            canInput = false;

            // מקרב את השומר
            _curDistance = Mathf.MoveTowards(_curDistance, 0f, Time.deltaTime * 5f);
            if (guard != null)
            {
                guard.curDistance = _curDistance;
                guard.Folllow(myTransform.position, forwardSpeed);

                if (!playedCaughtOnce)
                {
                    // אנימציית תפיסה של השחקן פעם אחת
                    SafePlayAnimation(caughtAnimName);
                    // אנימציית תפיסה של השומר/כלב (אם מימשת ב-FollowGuard)
                    if (guard != null) guard.CaughtPlayer();

                    playedCaughtOnce = true;
                    // אם תרצה: Invoke("FinishAfterCatch", 1f); // לסגור משחק אחרי שנייה
                    if (gameManager != null)
                    gameManager.EndGame();
                }
            }

            return; // לא מריצים לוגיקה רגילה
        }

        if (!gameManager.CanMove || gameManager.IsInputDisabled) return;

        if (rollGraceTimer > 0f) rollGraceTimer -= Time.deltaTime;
        if (jumpGraceTimer > 0f) jumpGraceTimer -= Time.deltaTime;

        GetSwipe();
        SetPlayerPosition();
        MovePlayer();
        Jump();
        Roll();

        _curDistance = Mathf.MoveTowards(_curDistance, 5f, Time.deltaTime * 0.5f);
        if (guard != null)
        {
            guard.curDistance = _curDistance;
            guard.Folllow(myTransform.position, forwardSpeed);
        }

        isGrounded = characterController.isGrounded;
        SetStumblePosition();
    }

    // אם תרצה לסגור משחק אחרי תפיסה:
    // private void FinishAfterCatch() { if (gameManager != null) gameManager.EndGame(); }

    public void DeathPlayer(string anim)
    {
        if (dead) return;
        dead = true;
        canInput = false;

        myAnimator.SetLayerWeight(1, 0f);
        SafePlayAnimation(anim); // אצלך זה "caught1"
    }

    public void PlayAnimation(string anim)
    {
        if (dead) return;
        SafePlayAnimation(anim);
    }

    private void SafePlayAnimation(string animName)
    {
        if (string.IsNullOrEmpty(animName) || myAnimator == null) return;
        myAnimator.Play(animName, 0, 0f);
    }

    private void SetStumblePosition()
    {
        if ((myAnimator.GetCurrentAnimatorStateInfo(0).IsName("StumbleSideRight") ||
             myAnimator.GetCurrentAnimatorStateInfo(0).IsName("StumbleSideLeft")) &&
            isStumbleTransitionComplete)
        {
            _curDistance = 0.6f;
            UpdatePlayerXPosition(_previousXPos);
            isStumbleTransitionComplete = false;
        }
    }

    private void Roll()
    {
        rollTimer -= Time.deltaTime;

        if (rollTimer <= 0f)
        {
            isRolling = false;
            rollTimer = 0f;
            characterController.center = new Vector3(0f, 0.45f, 0f);
            characterController.height = 0.9f;
        }

        if (swipeDown && !isJumping && canInput)
        {
            rollGraceTimer = actionGrace;

            isRolling = true;
            rollTimer = 1f;
            rollGraceTimer = 0.2f;
            SetPlayerAnimator(IdRoll, true);
            characterController.center = new Vector3(0f, 0.2f, 0f);
            characterController.height = 0.4f;
        }
    }

    private void GetSwipe()
    {
        if (!canInput) { swipeLeft = swipeRight = swipeDown = swipeUp = false; return; }

        if (isGrounded)
        {
            swipeLeft  = Input.GetKeyDown(KeyCode.LeftArrow);
            swipeRight = Input.GetKeyDown(KeyCode.RightArrow);
            swipeDown  = Input.GetKeyDown(KeyCode.DownArrow);
            swipeUp    = Input.GetKeyDown(KeyCode.UpArrow);
        }
    }

    private void SetPlayerPosition()
    {
        if (swipeLeft && !isRolling)
        {
            if (position == Side.Middle)
            {
                _previousXPos = Side.Middle;
                UpdatePlayerXPosition(Side.Left);
                SetPlayerAnimator(IdDodgeLeft, false);
                if (guard != null) guard.LeftDodge();
            }
            else if (position == Side.Right)
            {
                _previousXPos = Side.Right;
                UpdatePlayerXPosition(Side.Middle);
                SetPlayerAnimator(IdDodgeLeft, false);
                if (guard != null) guard.LeftDodge();
            }
        }
        else if (swipeRight && !isRolling)
        {
            if (position == Side.Middle)
            {
                _previousXPos = Side.Middle;
                UpdatePlayerXPosition(Side.Right);
                SetPlayerAnimator(IdDodgeRight, false);
                if (guard != null) guard.RightDodge();
            }
            else if (position == Side.Left)
            {
                _previousXPos = Side.Left;
                UpdatePlayerXPosition(Side.Middle);
                SetPlayerAnimator(IdDodgeRight, false);
                if (guard != null) guard.RightDodge();
            }
        }
    }

    private void UpdatePlayerXPosition(Side plPosition)
    {
        newXPosition = (int)plPosition;
        position = plPosition;
    }

    public void SetPlayerAnimatorWithLayer(int id)
    {
        myAnimator.SetLayerWeight(1, 1);
        myAnimator.Play(id);
        ResetCollision();
    }

    public void SetPlayerAnimator(int id, bool isCrossFade, float fadeTime = 0.1f)
    {
        myAnimator.SetLayerWeight(0, 1);
        myAnimator.Play(id);
        ResetCollision();
    }

    private void ResetCollision()
    {
        if (playerCollision != null)
        {
            Debug.Log(playerCollision.CollisionX + " " + playerCollision.CollisionY + " " + playerCollision.CollisionZ);
            playerCollision.CollisionX = CollisionX.None;
            playerCollision.CollisionY = CollisionY.None;
            playerCollision.CollisionZ = CollisionZ.None;
        }
    }

    private void MovePlayer()
    {
        motionVector = new Vector3(
            xPosition - myTransform.position.x,
            yPosition * Time.deltaTime,
            forwardSpeed * Time.deltaTime
        );

        xPosition = Mathf.Lerp(xPosition, newXPosition, Time.deltaTime * dodgeSpeed);
        characterController.Move(motionVector);
    }

    private void Jump()
    {
        if (characterController.isGrounded)
        {
            isJumping = false;

            if (myAnimator.GetCurrentAnimatorStateInfo(0).IsName("Fall"))
                SetPlayerAnimator(IdLanding, false);

            if (swipeUp && !isRolling && !dead && canInput)
            {
                if (guard != null) guard.Jump();

                jumpGraceTimer = actionGrace;
                isJumping = true;
                yPosition = jumpPower;
                SetPlayerAnimator(IdJump, true, 1f);
            }
        }
        else
        {
            yPosition -= jumpPower * 2f * Time.deltaTime;

            if (characterController.velocity.y <= 0f)
                SetPlayerAnimator(IdFall, false);
        }
    }
}
