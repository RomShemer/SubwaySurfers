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
    [SerializeField] private string tagRollOnly    = "rollObstacle";
    [SerializeField] private string tagJumpOnly    = "jumpObstacle";
    [SerializeField] private string tagRollOrJump  = "rollAndJumpObstacle";
    [SerializeField] private string tagRamp        = "Ramp";
    [SerializeField] private string tagMovingTrain = "TrainOn";

    [Header("Layer filter (solid colliders)")]
    [Tooltip("Only colliders on these layers will be considered obstacles/trains.")]
    [SerializeField] private LayerMask relevantLayers = ~0;

    [Header("Hit debounce")]
    [SerializeField] private float hitCooldown = 0.12f; // מונע טיפול כפול על אותו קוליידר/פריים
    private int   _lastHitId = 0;
    private float _lastHitTime = -999f;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    // === נתיבי התנגשות מוצקה (CharacterController) ===
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null) return;
        OnCharacterCollision(hit.collider); // אותו צינור טיפול
    }

    // === API ציבורי למסלולים אחרים (למשל OnCollisionEnter מגוף עזר) ===
    public void OnCharacterCollision(Collider collider, bool bypassFilters = false)
    {
        if (!collider) return;

        // מתעלמים מטריגרים – כאן עובדים רק עם מוצק
        if (collider.isTrigger) return;

        // דיבאונס בסיסי למניעת טיפול כפול באותו אובייקט
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

        // חישוב "איפה פגענו" ביחס למכשול
        _collisionX = GetCollisionX(collider);
        _collisionY = GetCollisionY(collider);
        _collisionZ = GetCollisionZ(collider);

        // קודם כל – טאגים (חוקים ברורים), ואז נפילה ללוגיקה כללית
        if (IsRelevantTag(collider.tag))
        {
            if (HandleByTags(collider)) return; // טופל (עבר/נכשל)
            SetAnimatorByCollision(collider);    // לא הוחלט לפי טאג → טיפול כללי
            return;
        }

        // ללא טאג → נחשב כמכשול מוצק רגיל
        FailByCollision(collider);
    }

    // חוקים לפי טאג. מחזיר true אם טופל (הצלחה/כישלון), אחרת false.
    private bool HandleByTags(Collider col)
    {
        string t = col.tag;

        // רמפה – מתעלמים (מומלץ שהקוליידר שלה יהיה Trigger, או בלייר שלא מתנגש עם השחקן)
        if (t == tagRamp) return true;

        bool rollingNow        = GetRollingFlag();   // כולל חלון חסד
        bool airborneOrJumping = GetJumpFlag();      // כולל חלון חסד

        if (t == tagRollOnly)
        {
            if (rollingNow) return true; // עבר כהלכה
            FailByCollision(col); return true;
        }

        if (t == tagJumpOnly)
        {
            if (airborneOrJumping) return true; // עבר כהלכה
            FailByCollision(col); return true;
        }

        if (t == tagRollOrJump)
        {
            if (rollingNow || airborneOrJumping) return true; // אחד מספיק
            FailByCollision(col); return true;
        }

        if (t == tagMovingTrain)
        {
            playerController.SetPlayerAnimator(playerController.IdDeathMovingTrain, false);
            playerController.gameManager.EndGame();
            return true;
        }

        return false;
    }

    // כישלון "גנרי" – בוחר אנימציה מתאימה לפי מיקום הפגיעה (כמו שהשתמשת עד עכשיו)
    private void FailByCollision(Collider collider)
    {
        if (playerController == null) return;

        if (_collisionZ == CollisionZ.Backward && _collisionX == CollisionX.Middle && _collisionY == CollisionY.LowDown)
        {
            collider.enabled = false; // למניעת טריגר כפול
            playerController.SetPlayerAnimator(playerController.IdStumbleLow, false);
            return;
        }

        if (_collisionY == CollisionY.Down || _collisionY == CollisionY.LowDown)
        {
            playerController.SetPlayerAnimator(playerController.IdDeathLower, false);
        }
        else if (_collisionY == CollisionY.Up && !GetRollingFlag())
        {
            // מוות עליון רק אם באמת "באוויר"/קפיצה (אחרת זה נראה כמו באמפר)
            if (GetJumpFlag())
                playerController.SetPlayerAnimator(playerController.IdDeathUpper, false);
            else
                playerController.SetPlayerAnimator(playerController.IdDeathBounce, false);
        }
        else
        {
            playerController.SetPlayerAnimator(playerController.IdDeathBounce, false);
        }

        playerController.gameManager.EndGame();
    }

    // לוגיקת ברירת מחדל (אם טאג לא הכריע)
    private void SetAnimatorByCollision(Collider collider)
    {
        if (playerController == null) return;

        if (_collisionZ == CollisionZ.Backward && _collisionX == CollisionX.Middle)
        {
            if (_collisionY == CollisionY.LowDown)
            {
                collider.enabled = false;
                playerController.SetPlayerAnimator(playerController.IdStumbleLow, false);
            }
            else if (_collisionY == CollisionY.Down)
            {
                playerController.SetPlayerAnimator(playerController.IdDeathLower, false);
                playerController.gameManager.EndGame();
            }
            else if (_collisionY == CollisionY.Middle)
            {
                if (collider.CompareTag(tagMovingTrain))
                {
                    playerController.SetPlayerAnimator(playerController.IdDeathMovingTrain, false);
                    playerController.gameManager.EndGame();
                }
                else if (!collider.CompareTag(tagRamp))
                {
                    playerController.SetPlayerAnimator(playerController.IdDeathBounce, false);
                    playerController.gameManager.EndGame();
                }
            }
            else if (_collisionY == CollisionY.Up && !GetRollingFlag())
            {
                if (GetJumpFlag())
                {
                    playerController.SetPlayerAnimator(playerController.IdDeathUpper, false);
                    playerController.gameManager.EndGame();
                }
                else
                {
                    playerController.SetPlayerAnimator(playerController.IdDeathBounce, false);
                    playerController.gameManager.EndGame();
                }
            }
        }
        else if (_collisionZ == CollisionZ.Middle)
        {
            if (_collisionX == CollisionX.Right)
                playerController.SetPlayerAnimator(playerController.IdStumbleSideRight, false);
            else if (_collisionX == CollisionX.Left)
                playerController.SetPlayerAnimator(playerController.IdStumbleSideLeft, false);
        }
        else
        {
            if (_collisionX == CollisionX.Right)
                playerController.SetPlayerAnimatorWithLayer(playerController.IdStumbleCornerRight);
            else if (_collisionX == CollisionX.Left)
                playerController.SetPlayerAnimatorWithLayer(playerController.IdStumbleCornerLeft);
        }
    }

    // ===== עזרים =====

    private bool IsRelevantTag(string t) =>
        t == tagRollOnly || t == tagJumpOnly || t == tagRollOrJump || t == tagRamp || t == tagMovingTrain;

    private bool IsOnRelevantLayer(Collider c) =>
        ((1 << c.gameObject.layer) & relevantLayers) != 0;

    private bool GetRollingFlag()
    {
        // משתמש בדגל ה"יציב" מה־PlayerController (כולל חלון חסד)
        try { return playerController.IsRollingNow; } catch { return false; }
    }

    private bool GetJumpFlag()
    {
        // משתמש בדגל ה"יציב" מה־PlayerController (כולל חלון חסד)
        try { return playerController.IsAirborneOrJumping; } catch { }

        // נפילה לגיבוי – זיהוי לפי CharacterController
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
}
