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
    public float curDistance = 0.6f;

    private bool _forceAnimOn = false;

    void Awake()
    {
        if (!guardTransform) guardTransform = this.transform;
        if (!dogTransform && guardTransform) dogTransform = guardTransform;

        if (!guardAnimator) guardAnimator = guardTransform ? guardTransform.GetComponent<Animator>() : GetComponent<Animator>();
        if (!dogAnimator && dogTransform) dogAnimator = dogTransform.GetComponent<Animator>();
    }

    void Start()
    {
        SetAnimatorsEnabled(false);
        
        if (!audioSource) audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        var gm = GameManager.Instance;

        bool shouldRun = gm && gm.CanMove && !gm.IsInputDisabled;

        if (shouldRun)
        {
            if (!_forceAnimOn) SetAnimatorsEnabled(true);
        }
        else
        {
            if (!_forceAnimOn) SetAnimatorsEnabled(false);
        }
    }

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
        _forceAnimOn = true;

        StopAllCoroutines();

        SetAnimatorsEnabled(true);

        if (guardAnimator)
        {
            guardAnimator.updateMode = AnimatorUpdateMode.UnscaledTime; 
            guardAnimator.speed = 1f;

            guardAnimator.ResetTrigger("Run");
            TrySetBool(guardAnimator, "isRunning", false);
            TrySetFloat(guardAnimator, "speed", 0f);

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
        anim.CrossFadeInFixedTime(stateName, 0.05f, 0, 0f);

        float t = 0f;
        while (t < 0.25f)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (!st.IsName(stateName))
            {
                anim.CrossFadeInFixedTime(stateName, 0.05f, 0, 0f);
            }

            TrySetBool(anim, "isRunning", false);
            TrySetFloat(anim, "speed", 0f);

            yield return null;
            t += Time.unscaledDeltaTime; 
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
    
    public void ResetGuardVisuals()
    {
        _forceAnimOn = false;
        SetAnimatorsEnabled(false);
    }

    private IEnumerator PlayAnim(string guardAnim, string dogAnim)
    {
        yield return new WaitUntil(() => GameManager.Instance && GameManager.Instance.CanMove && !GameManager.Instance.IsInputDisabled);

        SetAnimatorsEnabled(true);

        float d = Mathf.Max(curDistance, 0.1f);
        yield return new WaitForSeconds(d / 5f);

        if (guardAnimator && !string.IsNullOrEmpty(guardAnim))
            guardAnimator.Play(guardAnim, 0, 0f);

        if (dogAnimator && !string.IsNullOrEmpty(dogAnim))
            dogAnimator.Play(dogAnim, 0, 0f);
    }

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