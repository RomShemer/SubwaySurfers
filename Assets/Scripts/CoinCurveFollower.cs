using UnityEngine;

public class CoinCurveFollower : MonoBehaviour
{
   [Header("Raycast")]
    public float rayTopY = 50f;       // נקודת התחלה גבוהה
    public float rayMaxDist = 200f;   // טווח למטה
    public LayerMask groundMask = ~0; // שכבות Ground/Trains/Environment

    [Header("Placement")]
    public float aboveOffset = 0.5f;  // כמה מעל השטח לשבת
    public float fallbackY = 1.2f;    // אם לא פגענו בכלום

    [Header("Smoothing (optional)")]
    public bool smooth = true;
    public float smoothTime = 0.08f;
    float velY; // פנימי ל-SmoothDamp

    void LateUpdate()
    {
        Vector3 p = transform.position;

        // Ray מלמעלה-למטה על לפי X,Z הנוכחיים
        Vector3 origin = new Vector3(p.x, rayTopY, p.z);
        float targetY;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayMaxDist, groundMask, QueryTriggerInteraction.Collide))
            targetY = hit.point.y + aboveOffset;   // תמיד בדיוק מעל הרכבת/קרקע
        else
            targetY = fallbackY;                   // גיבוי אם אין פגיעה

        p.y = smooth ? Mathf.SmoothDamp(p.y, targetY, ref velY, smoothTime) : targetY;
        transform.position = p;
    }
}
