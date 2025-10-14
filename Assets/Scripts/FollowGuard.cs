using System.Collections;
using UnityEngine;

public class FollowGuard : MonoBehaviour
{
    [Header("Animators & Transforms")]
    public Animator guardAnimator;
    public Animator dogAnimator;
    public Transform guardTransform;
    public Transform dogTransform;

    [Header("Follow")]
    [Tooltip("מרחק מהרץ (בציר Z חיובי)")]
    public float curDistance = 0.6f;

    // כשמפעילים תפיסה, נרצה להשאיר אנימטורים דלוקים גם אם המשחק הסתיים
    private bool _forceAnimOn = false;

    void Awake()
    {
        // ניסיונות השמה אוטומטית אם לא שובץ באינספקטור
        if (!guardTransform) guardTransform = this.transform;
        if (!dogTransform && guardTransform) dogTransform = guardTransform;

        // אם האנימטורים לא שובצו ידנית, ננסה למצוא בילדים
        if (!guardAnimator) guardAnimator = guardTransform ? guardTransform.GetComponent<Animator>() : GetComponent<Animator>();
        if (!dogAnimator && dogTransform) dogAnimator = dogTransform.GetComponent<Animator>();
    }

    void Start()
    {
        // בתחילת המשחק – לכבות אנימציות כדי שלא "יזוזו במקום"
        SetAnimatorsEnabled(false);
    }

    void Update()
    {
        var gm = GameManager.Instance;

        // האם אמורים לרוץ עכשיו (משחק התחיל ולא חסום)?
        bool shouldRun = gm && gm.CanMove && !gm.IsInputDisabled;

        if (shouldRun)
        {
            // ודא שהם דלוקים כשמותר לשחק
            if (!_forceAnimOn) SetAnimatorsEnabled(true);
        }
        else
        {
            // לפני התחלה / או אם הקלט מנוטרל – לא לנגן אנימציות.
            // אבל אם הפעלנו catch (force on), אל תכבה.
            if (!_forceAnimOn) SetAnimatorsEnabled(false);
        }
    }

    // =======================
    //  API לקריאות קיימות
    // =======================

    public void Jump()
    {
        StartCoroutine(PlayAnim("Guard_jump", "Dog_jump"));
    }

    public void LeftDodge()
    {
        StartCoroutine(PlayAnim("Guard_dodgeLeft", "Dog_Run"));
    }

    public void RightDodge()
    {
        StartCoroutine(PlayAnim("Guard_dodgeRight", "Dog_Run"));
    }

    public void Stumble()
    {
        StopAllCoroutines();
        StartCoroutine(PlayAnim("Guard_grap after", "Dog_Fast Run"));
    }

    public void CaughtPlayer()
    {
        // בזמן תפיסה – להכריח אנימטורים להיות דלוקים, גם אם המשחק הסתיים
        _forceAnimOn = true;
        SetAnimatorsEnabled(true);

        if (guardAnimator) guardAnimator.Play("catch_1", 0, 0f);
        if (dogAnimator)   dogAnimator.Play("catch",   0, 0f);
    }

    /// <summary>
    /// קריאה מבחוץ אם רוצים "לשחרר" את האנימטורים אחרי restart
    /// (בד״כ אין צורך – אתחול סצנה/Run חדש כבר מכבה אותם ב-Start)
    /// </summary>
    public void ResetGuardVisuals()
    {
        _forceAnimOn = false;
        SetAnimatorsEnabled(false);
    }

    private IEnumerator PlayAnim(string guardAnim, string dogAnim)
    {
        // אם עוד לא התחיל המשחק – נחכה שיתאפשר (כדי לא לנגן בזמן Pause)
        yield return new WaitUntil(() => GameManager.Instance && GameManager.Instance.CanMove && !GameManager.Instance.IsInputDisabled);

        // דלי את האנימטורים ליתר ביטחון
        SetAnimatorsEnabled(true);

        // השהיה תלויה מרחק – כמו אצלך
        float d = Mathf.Max(curDistance, 0.1f);
        yield return new WaitForSeconds(d / 5f);

        if (guardAnimator && !string.IsNullOrEmpty(guardAnim))
            guardAnimator.Play(guardAnim, 0, 0f);

        if (dogAnimator && !string.IsNullOrEmpty(dogAnim))
            dogAnimator.Play(dogAnim, 0, 0f);
    }

    // נקראת מה-PlayerController בפריימים שונים
    public void Folllow(Vector3 playerPos, float speed)
    {
        if (!guardTransform) return;

        float d = Mathf.Max(curDistance, 0.1f);
        Vector3 targetPos = playerPos - Vector3.forward * d;

        guardTransform.position = Vector3.Lerp(
            guardTransform.position,
            targetPos,
            Time.deltaTime * (speed / d)
        );

        if (dogTransform)
            dogTransform.position = guardTransform.position;
    }

    // =======================
    //  Utils
    // =======================

    private void SetAnimatorsEnabled(bool enabled)
    {
        if (guardAnimator && guardAnimator.enabled != enabled) guardAnimator.enabled = enabled;
        if (dogAnimator && dogAnimator.enabled != enabled)     dogAnimator.enabled   = enabled;
    }
} 