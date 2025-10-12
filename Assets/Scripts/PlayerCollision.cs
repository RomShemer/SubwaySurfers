using System;
using UnityEngine;

public enum CollisionX { None, Left, Middle, Right }
public enum CollisionY { None, Up, Middle, Down, LowDown }
public enum CollisionZ { None, Forward, Middle, Backward }

[RequireComponent(typeof(CharacterController))]
public class PlayerCollision : MonoBehaviour
{
    private PlayerController playerController;

    private CollisionX _collisionX;
    private CollisionY _collisionY;
    private CollisionZ _collisionZ;

    public CollisionX CollisionX { get => _collisionX; set => _collisionX = value; }
    public CollisionY CollisionY { get => _collisionY; set => _collisionY = value; }
    public CollisionZ CollisionZ { get => _collisionZ; set => _collisionZ = value; }

    [Header("Obstacle Tags")]
    [SerializeField] private string tagRollOnly     = "rollObstacle";
    [SerializeField] private string tagJumpOnly     = "jumpObstacle";
    [SerializeField] private string tagRollOrJump   = "rollAndJumpObstacle";
    [SerializeField] private string tagRamp         = "Ramp";
    [SerializeField] private string tagMovingTrain  = "TrainOn";
    [SerializeField] private string tagWallObstacle = "WallObstacle"; // קיר/מחסום מלא שמפיל תמיד

    [Header("Layer filter (solid colliders)")]
    [Tooltip("Only colliders on these layers will be considered obstacles/trains.")]
    [SerializeField] private LayerMask relevantLayers = ~0;

    [Header("Hit debounce")]
    [SerializeField] private float hitCooldown = 0.12f; // מניעת טיפול כפול על אותו קוליידר/פריים
    private int   _lastHitId = 0;
    private float _lastHitTime = -999f;

    [Header("Side Stumble Grace")]
    [Tooltip("חלון זמן קצר אחרי Stumble צד כדי לא להיתפס מיד שוב")]
    [SerializeField] private float sideStumbleGrace = 0.25f;
    private float _lastSideStumbleTime = -999f;

    [Header("Catch on repeated non-fatal stumble")]
    [Tooltip("אם השומר קרוב מהמרחק הזה ובתוך חלון הזמן יש עוד סטאמבל לא קטלני → תפיסה")]
    [SerializeField] private float guardCatchDistance = 1.25f;
    [SerializeField] private float stumbleCatchWindow = 0.75f;
    private float _lastNonFatalStumbleTime = -999f;

    [SerializeField] private bool debugLogCollisions = false;
    private Animator _anim;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        _anim = GetComponent<Animator>();
    }

    // נתיב התנגשות מוצקה (CharacterController)
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null) return;
        OnCharacterCollision(hit.collider);
    }

    // API ציבורי למסלולים אחרים
    public void OnCharacterCollision(Collider collider, bool bypassFilters = false)
    {
        if (!collider) return;
        if (collider.isTrigger) return; // אם עובדים עם טריגרים – אפשר להוסיף OnTriggerEnter

        // דיבאונס
        int id = collider.GetInstanceID();
        if (id == _lastHitId && Time.time - _lastHitTime < hitCooldown) return;
        _lastHitId = id; _lastHitTime = Time.time;

        if (!bypassFilters && !IsOnRelevantLayer(collider)) return;

        HandleCollisionInternal(collider);
    }

    private void HandleCollisionInternal(Collider collider)
    {
        if (playerController == null || playerController.MyCharacterController == null)
            return;

        // חלון חסד קצר אחרי סטאמבל צד
        if (Time.time - _lastSideStumbleTime < sideStumbleGrace)
            return;

        // חישוב "איפה פגענו"
        _collisionX = GetCollisionX(collider);
        _collisionY = GetCollisionY(collider);
        _collisionZ = GetCollisionZ(collider);

        if (debugLogCollisions)
            Debug.Log($"[HIT] name={collider.name} tag={collider.tag} layer={collider.gameObject.layer} trig={collider.isTrigger}  X={_collisionX} Y={_collisionY} Z={_collisionZ}");

        // ✅ קדימות: פגיעה צדית באמצע → תמיד Stumble Side (לא מוות)
        if (_collisionZ == CollisionZ.Middle &&
            (_collisionX == CollisionX.Left || _collisionX == CollisionX.Right))
        {
            if (_collisionX == CollisionX.Right)
                playerController.SetPlayerAnimator(playerController.IdStumbleSideRight, false);
            else
                playerController.SetPlayerAnimator(playerController.IdStumbleSideLeft, false);

            _lastSideStumbleTime = Time.time;
            MaybeCatchAfterRepeatedStumble(); // בדיקת תפיסה על סטאמבל חוזר כשהשומר קרוב
            return;
        }

        // אם לא צד – ניגש לכללי הטאגים
        if (IsRelevantTag(collider.tag))
        {
            if (HandleByTags(collider)) return;
            SetAnimatorByCollision(collider);
            return;
        }

        // ללא טאג → מכשול רגיל
        FailByCollision(collider);
    }

    // ===== תפיסה (caught1) =====
    private void TriggerCaught(string reason = "")
    {
        if (debugLogCollisions) Debug.Log($"[Collision] TriggerCaught: {reason}");
        playerController.DeathPlayer("caught1");
        if (playerController.guard != null) playerController.guard.CaughtPlayer();
        if (playerController.gameManager != null)
            playerController.gameManager.EndGame();
    }

    // ===== לוגיקת "תפיסה על סטאמבל חוזר כשהשומר קרוב" =====
    private bool IsGuardClose()
    {
        return playerController != null &&
               playerController.guard != null &&
               playerController.guard.curDistance <= guardCatchDistance;
    }

    private void MaybeCatchAfterRepeatedStumble()
    {
        if (IsGuardClose() && Time.time - _lastNonFatalStumbleTime <= stumbleCatchWindow)
        {
            // סטאמבל שני בזמן קצר כשהשוטר קרוב → תפיסה
            playerController.CaughtByGuard(); // דורש פונקציה קצרה ב-PlayerController (ראה למטה)
        }
        else
        {
            // סטאמבל ראשון או שהשוטר לא מספיק קרוב → רק נרשום זמן
            _lastNonFatalStumbleTime = Time.time;
        }
    }

    // ===== חוקים לפי טאג =====
    private bool HandleByTags(Collider col)
    {
        string t = col.tag;

        if (t == tagRamp) return true; // מתעלמים מרמפה

        // אם איכשהו הגענו לכאן עם פגיעה צדית – נטפל כסטאמבל צד
        if (_collisionZ == CollisionZ.Middle &&
            (_collisionX == CollisionX.Left || _collisionX == CollisionX.Right))
        {
            if (_collisionX == CollisionX.Right)
                playerController.SetPlayerAnimator(playerController.IdStumbleSideRight, false);
            else
                playerController.SetPlayerAnimator(playerController.IdStumbleSideLeft, false);

            _lastSideStumbleTime = Time.time;
            MaybeCatchAfterRepeatedStumble();
            return true;
        }

        // בדיקות "קשיחות" (לא חלונות חסד)
        bool rollingStrict = IsActuallyRollingNowStrict();
        bool jumpingStrict = IsActuallyJumpingNowStrict();

        if (debugLogCollisions)
            Debug.Log($"[Collision] tag={t} rollStrict={rollingStrict} jumpStrict={jumpingStrict}  looseRoll={GetRollingFlag()} looseJump={GetJumpFlag()}");

        if (t == tagRollOnly)
        {
            if (rollingStrict) return true;
            TriggerCaught("rollObstacle_fail"); return true;
        }

        if (t == tagJumpOnly)
        {
            if (jumpingStrict) return true;
            TriggerCaught("jumpObstacle_fail"); return true;
        }

        if (t == tagRollOrJump)
        {
            if (rollingStrict || jumpingStrict) return true;
            TriggerCaught("rollOrJumpObstacle_fail"); return true;
        }

        if (t == tagMovingTrain)
        {
            TriggerCaught("movingTrain"); return true;
        }

        if (!string.IsNullOrEmpty(tagWallObstacle) && t == tagWallObstacle)
        {
            TriggerCaught("wallObstacle"); return true;
        }

        return false;
    }

    // ===== כישלון כללי =====
    private void FailByCollision(Collider collider, bool isHandleTag = false)
    {
        if (playerController == null) return;

        // Stumble נמוך – אם הגיע מהגדרת טאג (כלומר נכשל בדרישה), זה תפיסה; אחרת זה סתם סטאמבל
        if (_collisionZ == CollisionZ.Backward && _collisionX == CollisionX.Middle && _collisionY == CollisionY.LowDown)
        {
            collider.enabled = false;

            if (!isHandleTag)
            {
                playerController.SetPlayerAnimator(playerController.IdStumbleLow, false);
                MaybeCatchAfterRepeatedStumble(); // גם Low Stumble נספר לרצף
            }
            else
            {
                TriggerCaught("lowDown_by_tag");
            }
            return;
        }

        // כל השאר = תפיסה
        TriggerCaught("generic_fail");
    }

    // ===== ברירת מחדל אם טאג לא הכריע =====
    private void SetAnimatorByCollision(Collider collider)
    {
        if (playerController == null) return;

        if (_collisionZ == CollisionZ.Backward && _collisionX == CollisionX.Middle)
        {
            if (_collisionY == CollisionY.LowDown)
            {
                collider.enabled = false;
                playerController.SetPlayerAnimator(playerController.IdStumbleLow, false);
                MaybeCatchAfterRepeatedStumble();
            }
            else
            {
                TriggerCaught("backward_mid_fail");
            }
        }
        else if (_collisionZ == CollisionZ.Middle)
        {
            // (לגיבוי; בפועל הקדימות כבר תפסה למעלה)
            if (_collisionX == CollisionX.Right)
                playerController.SetPlayerAnimator(playerController.IdStumbleSideRight, false);
            else if (_collisionX == CollisionX.Left)
                playerController.SetPlayerAnimator(playerController.IdStumbleSideLeft, false);

            _lastSideStumbleTime = Time.time;
            MaybeCatchAfterRepeatedStumble();
        }
        else
        {
            if (_collisionX == CollisionX.Right)
                playerController.SetPlayerAnimatorWithLayer(playerController.IdStumbleCornerRight);
            else if (_collisionX == CollisionX.Left)
                playerController.SetPlayerAnimatorWithLayer(playerController.IdStumbleCornerLeft);
            // פינתיים לרוב לא נחשבים "לא קטלני" במשחקים—אם תרצה לספור לרצף, אפשר להוסיף גם כאן:
            // _lastNonFatalStumbleTime = Time.time;
        }
    }

    // ===== עזרים =====
    private bool IsRelevantTag(string t) =>
        t == tagRollOnly || t == tagJumpOnly || t == tagRollOrJump || t == tagRamp || t == tagMovingTrain ||
        (!string.IsNullOrEmpty(tagWallObstacle) && t == tagWallObstacle);

    private bool IsOnRelevantLayer(Collider c) =>
        ((1 << c.gameObject.layer) & relevantLayers) != 0;

    private bool GetRollingFlag()
    {
        try { return playerController.IsRollingNow; } catch { return false; }
    }

    private bool GetJumpFlag()
    {
        try { return playerController.IsAirborneOrJumping; } catch { }
        var cc = playerController.MyCharacterController;
        return !cc.isGrounded || Mathf.Abs(cc.velocity.y) > 0.05f;
    }

    private CollisionZ GetCollisionZ(Collider collider)
    {
        Bounds a = playerController.MyCharacterController.bounds;
        Bounds b = collider.bounds;
        float minZ = Mathf.Max(b.min.z, a.min.z);
        float maxZ = Mathf.Min(b.max.z, a.max.z);
        float avg = (minZ + maxZ) / 2 - b.min.z;

        if (avg > b.size.z - 0.2f) return CollisionZ.Forward;
        if (avg < 0.2f)            return CollisionZ.Backward;
                                   return CollisionZ.Middle;
    }

    private CollisionY GetCollisionY(Collider collider)
    {
        Bounds a = playerController.MyCharacterController.bounds;
        Bounds b = collider.bounds;
        float minY = Mathf.Max(b.min.y, a.min.y);
        float maxY = Mathf.Min(b.max.y, a.max.y);
        float avg = (minY + maxY) / 2 - b.min.y;

        if (avg > b.size.y - 0.33f) return CollisionY.Up;
        if (avg < 0.17f)            return CollisionY.LowDown;
        if (avg < 0.33f)            return CollisionY.Down;
                                    return CollisionY.Middle;
    }

    private CollisionX GetCollisionX(Collider collider)
    {
        Bounds a = playerController.MyCharacterController.bounds;
        Bounds b = collider.bounds;
        float minX = Mathf.Max(b.min.x, a.min.x);
        float maxX = Mathf.Min(b.max.x, a.max.x);
        float avg = (minX + maxX) / 2 - b.min.x;

        if (avg > b.size.x - 0.33f) return CollisionX.Right;
        if (avg < 0.33f)            return CollisionX.Left;
                                    return CollisionX.Middle;
    }

    // בדיקות "קשיחות" לאנימציות בפועל
    private bool IsActuallyRollingNowStrict()
    {
        if (_anim)
        {
            AnimatorStateInfo st = _anim.GetCurrentAnimatorStateInfo(0);
            if (st.IsName("Roll") || st.IsTag("Roll"))
                return true;
        }
        return false;
    }

    private bool IsActuallyJumpingNowStrict()
    {
        if (_anim)
        {
            var st = _anim.GetCurrentAnimatorStateInfo(0);
            if (st.IsName("Jump") || st.IsTag("Jump"))
                return true;
        }
        var cc = playerController.MyCharacterController;
        return !cc.isGrounded && cc.velocity.y > 0.05f; // תנועה כלפי מעלה
    }
}
