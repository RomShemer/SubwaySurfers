using System;
using UnityEngine;

public class MagnetCollector : MonoBehaviour
{
    public static MagnetCollector I { get; private set; }

    [Header("Who to pull toward")]
    public Transform target;                // בד"כ ה-Transform של השחקן
    [Tooltip("אם הוגדר, המטבעות יימשכו לנקודה הזו (מומלץ: ילד על החזה)")]
    public Transform aimAnchor;
    [Tooltip("אם אין Anchor, נשתמש בהיסט מעלה יחסית לשחקן")]
    public float aimUpOffset = 0.35f;

    [Header("Timing")]
    public float defaultDuration = 7f;
    private float _timer;
    public bool Active => _timer > 0f;

    public enum LateralAxis { PlayerRight, WorldX }
    [Header("Pull band (who is eligible)")]
    public LateralAxis lateralAxis = LateralAxis.WorldX;
    [Tooltip("חצי-רוחב רצועה. כדי למשוך את כל 3 המסילות (±1.2), שים 1.3–2.5")]
    public float lateralRange = 2.0f;
    [Tooltip("האם לבדוק גם טווח קדימה/אחורה? אם לא – מפעיל רוחב בלבד")]
    public bool useZBand = true;
    public float forwardRange = 12f;
    public float backRange = 1.5f;

    [Header("Extra limits")]
    [Tooltip("האם להגביל רדיוס כללי")]
    public bool limitRadius = true;
    public float maxRadius = 15f;

    [Header("Pull dynamics")]
    public float pullSpeed = 14f;
    public float snapDistance = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    
    public Action OnActivated;
    public Action OnExpired;

    private void Awake()
    {
        I = this;
        if (!target) target = transform;
    }

    private void Start()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
    }

    public void Activate(float seconds)
    {
        bool was = Active;
        _timer = Mathf.Max(_timer, seconds > 0 ? seconds : defaultDuration);
        if (!was && Active)
        {
            OnActivated?.Invoke();
            if (audioSource && !audioSource.isPlaying) audioSource.Play();
        }
    }

    private void Update()
    {
        bool was = Active;
        if (_timer > 0f) _timer -= Time.deltaTime;
        
        if (was && !Active)
        {
            ExpireNow();
        }
    }

    // --- כיבוי מיידי (Game Over, מוות וכו') ---
    public void Deactivate()
    {
        if (!Active) { StopAudio(); return; }
        _timer = 0f;
        ExpireNow();
    }

    // תאימות לשם הישן שלך
    public void stopMagnet() => Deactivate();

    private void ExpireNow()
    {
        OnExpired?.Invoke();
        StopAudio();
    }

    private void StopAudio()
    {
        if (audioSource && audioSource.isPlaying)
            audioSource.Stop();
    }

    private void OnDisable()
    {
        StopAudio();
        _timer = 0f;
    }

    // ---- לוגיקת משיכה ----
    private Vector3 AimPosition()
    {
        if (aimAnchor) return aimAnchor.position;
        return (target ? target.position : Vector3.zero) + Vector3.up * aimUpOffset;
    }

    public bool ShouldAttract(Vector3 coinWorldPos, out Vector3 aimWorldPos)
    {
        aimWorldPos = AimPosition();
        if (!Active || !target) return false;

        // הגבלת רדיוס כללי
        if (limitRadius)
        {
            float sq = (coinWorldPos - aimWorldPos).sqrMagnitude;
            if (sq > maxRadius * maxRadius) return false;
        }

        // מדידת רוחב
        float dx;
        if (lateralAxis == LateralAxis.WorldX)
            dx = Mathf.Abs(coinWorldPos.x - aimWorldPos.x);
        else
        {
            Vector3 toCoin = coinWorldPos - aimWorldPos;
            dx = Mathf.Abs(Vector3.Dot(toCoin, target.right));
        }

        bool inBand = dx <= lateralRange;
        if (!useZBand) return inBand;

        // טווח קדימה/אחורה
        Vector3 fwd = target.forward;
        float dz = Vector3.Dot(coinWorldPos - aimWorldPos, fwd);
        return inBand && (dz > -backRange) && (dz <= forwardRange);
    }
}
