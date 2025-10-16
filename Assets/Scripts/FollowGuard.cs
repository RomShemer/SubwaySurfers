using System.Collections;
using UnityEngine;

public class FollowGuard : MonoBehaviour
{
    [Header("Animators & Transforms")]
    public Animator guardAnimator;
    public Animator dogAnimator;
    public Transform guardTransform;
    public Transform dogTransform;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip startRunSound;

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
        
        if (!audioSource) audioSource = GetComponent<AudioSource>();
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

    /*public void CaughtPlayer()
    {
        Debug.Log("==== [FollowGuard] CaughtPlayer() called ====");

        // Force animators on even if the game is over
        _forceAnimOn = true;
        SetAnimatorsEnabled(true);

        // --- GUARD ---
        if (guardAnimator)
        {
            Debug.Log("[Guard] Animator is ENABLED.");

            var guardState = guardAnimator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"[Guard] Current state: {guardState.fullPathHash} / normalizedTime={guardState.normalizedTime:F2}");

            // Check if it's currently playing the run animation
            if (guardState.IsName("Guard_Run"))
            {
                Debug.Log("[Guard] Currently in Guard_Run → switching to catch_1");
                guardAnimator.CrossFadeInFixedTime("catch_1", 0.1f);
            }
            else
            {
                Debug.Log($"[Guard] Not in Guard_Run (maybe in {guardState.shortNameHash}), forcing catch_1 anyway");
                guardAnimator.CrossFadeInFixedTime("catch_1", 0.1f);
            }
        }
        else
        {
            Debug.LogWarning("[Guard] Animator is NULL or DISABLED!");
        }

        // --- DOG ---
        if (dogAnimator)
        {
            Debug.Log("[Dog] Animator is ENABLED.");

            var dogState = dogAnimator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"[Dog] Current state: {dogState.fullPathHash} / normalizedTime={dogState.normalizedTime:F2}");

            if (dogState.IsName("Dog_Run"))
            {
                Debug.Log("[Dog] Currently in Dog_Run → switching to catch");
                dogAnimator.CrossFadeInFixedTime("catch", 0.1f);
            }
            else
            {
                Debug.Log($"[Dog] Not in Dog_Run (maybe in {dogState.shortNameHash}), forcing catch anyway");
                dogAnimator.CrossFadeInFixedTime("catch", 0.1f);
            }
        }
        else
        {
            Debug.LogWarning("[Dog] Animator is NULL or DISABLED!");
        }

        Debug.Log("==== [FollowGuard] CaughtPlayer() finished ====");
    } */
    
    public void CaughtPlayer() 
    {
        Debug.Log("==== [FollowGuard] CaughtPlayer() called ====");
        _forceAnimOn = true;

        // 1) אל תתני לקורוטינות אחרות להחזיר ל-Run
        StopAllCoroutines();

        // 2) ודאי שהאנימטורים דלוקים
        SetAnimatorsEnabled(true);

        if (guardAnimator)
        {
            guardAnimator.updateMode = AnimatorUpdateMode.UnscaledTime; // שלא ייפגע מ-timeScale
            guardAnimator.speed = 1f;

            // 3) נטרול תנאים שמחזירים ל-Run
            guardAnimator.ResetTrigger("Run");
            TrySetBool(guardAnimator, "isRunning", false);
            TrySetFloat(guardAnimator, "speed", 0f);

            // 4) הכרחת מעבר + שמירה עליו כמה פריימים
            StartCoroutine(ForceCatchState(guardAnimator, "catch_1"));
        }

        if (dogAnimator)
        {
            dogAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            dogAnimator.speed = 1f;
            StartCoroutine(ForceCatchState(dogAnimator, "catch"));
        }
    }

    private IEnumerator ForceCatchState(Animator anim, string stateName)
    {
        // דחיפה ראשונית
        anim.CrossFadeInFixedTime(stateName, 0.05f, 0, 0f);
        Debug.Log($"[FollowGuard] CrossFade → {stateName}");

        // למשך ~0.25ש׳ תשגיח שכלום לא מחזיר ל-Run
        float t = 0f;
        while (t < 0.25f)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"[FollowGuard] now in = {st.shortNameHash}, normalized={st.normalizedTime:F2}");
            if (!st.IsName(stateName))
            {
                Debug.LogWarning($"[FollowGuard] NOT in {stateName} → re-apply CrossFade");
                anim.CrossFadeInFixedTime(stateName, 0.05f, 0, 0f);
            }
            // אם יש פרמטרים שמחזירים ל-Run, ננטרל כל לולאה
            TrySetBool(anim, "isRunning", false);
            TrySetFloat(anim, "speed", 0f);

            yield return null;
            t += Time.unscaledDeltaTime; // ריאלי
        }
    }

// helpers
private void TrySetBool(Animator a, string name, bool v)
{
    foreach (var p in a.parameters) if (p.type == AnimatorControllerParameterType.Bool && p.name == name) { a.SetBool(name, v); return; }
}
private void TrySetFloat(Animator a, string name, float v)
{
    foreach (var p in a.parameters) if (p.type == AnimatorControllerParameterType.Float && p.name == name) { a.SetFloat(name, v); return; }
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

    public void playStartGuardSound()
    {
        if(audioSource) audioSource.PlayOneShot(startRunSound);
    }
} 