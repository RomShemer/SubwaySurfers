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
    float _timer;
    public bool Active => _timer > 0f;

    public enum LateralAxis { PlayerRight, WorldX }
    [Header("Pull band (who is eligible)")]
    [Tooltip("PlayerRight: רוחב לפי ימין של השחקן; WorldX: לפי ציר X עולמי")]
    public LateralAxis lateralAxis = LateralAxis.WorldX;
    [Tooltip("חצי-רוחב רצועה. כדי למשוך את כל 3 המסילות (±1.2), שימי 1.3–2.5")]
    public float lateralRange = 2.0f;
    [Tooltip("האם לבדוק גם טווח קדימה/אחורה? אם לא – מפעיל רוחב בלבד")]
    public bool useZBand = false;
    public float forwardRange = 20f;
    public float backRange = 2f;

    [Header("Pull dynamics")]
    public float pullSpeed = 14f;
    public float snapDistance = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    
    public System.Action OnActivated;
    public System.Action OnExpired;

    private bool _wasActive;

    private void Start()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>(); 
    }

  void Awake()
    {
        I = this;
        if (!target) target = transform;
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
    void Update()
    {
        bool was = Active;
        if (_timer > 0f) _timer -= Time.deltaTime;
        
        if (was && !Active)
        {
            OnExpired?.Invoke();
            if (audioSource && audioSource.isPlaying) audioSource.Stop();
        }

    }

    /// נקודת היעד (Anchor) למשיכה
    Vector3 AimPosition()
    {
        if (aimAnchor) return aimAnchor.position;
        return (target ? target.position : Vector3.zero) + Vector3.up * aimUpOffset;
    }

    /// האם מטבע צריך להישאב, ולהחזיר את נקודת היעד
    public bool ShouldAttract(Vector3 coinWorldPos, out Vector3 aimWorldPos)
    {
        aimWorldPos = AimPosition();
        if (!Active || !target) return false;

        // מדידת רוחב
        float dx;
        if (lateralAxis == LateralAxis.WorldX)
            dx = Mathf.Abs(coinWorldPos.x - aimWorldPos.x);           // רצועה לפי X עולמי
        else
        {
            Vector3 toCoin = coinWorldPos - aimWorldPos;
            dx = Mathf.Abs(Vector3.Dot(toCoin, target.right));        // רצועה לפי ימין של השחקן
        }

        bool inBand = dx <= lateralRange;
        if (!useZBand) return inBand;

        // טווח קדימה/אחורה אופציונלי
        Vector3 fwd = target.forward;
        float dz = Vector3.Dot(coinWorldPos - aimWorldPos, fwd);
        return inBand && (dz > -backRange) && (dz <= forwardRange);
    }

    public void stopMagnet()
    {
        bool was = Active;

        if (was && !Active)
        {
            OnExpired?.Invoke();
            if (audioSource && audioSource.isPlaying) audioSource.Stop();
        }
    }
}
