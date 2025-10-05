using System.Collections.Generic;
using UnityEngine;

public class CoinLaneSpawner : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;           // ה-Player (Transform)
    public GameObject coinPrefab;      // Prefab של מטבע (עם Renderer + Collider isTrigger)

    [Header("Lanes (X) & Forward (Z)")]
    [Tooltip("מיקומי המסלולים בציר X (לדוגמה: -1, 0, 1)")]
    public float[] lanesX = new float[] { -1.0f, 0f, 1.0f };

    [Tooltip("מאיפה להתחיל לייצר ביחס לשחקן (ב-Z קדימה)")]
    public float startAheadZ = 15f;

    [Tooltip("עד כמה קדימה לייצר ביחס לשחקן (ב-Z)")]
    public float distanceAheadZ = 60f;

    [Tooltip("מרווח בין טורים לאורך Z (כיוון הריצה)")]
    public float spacingZ = 3.5f;

    [Tooltip("רנדומיזציה קטנה לאורך Z בכל טור")]
    public float jitterZ = 0.2f;

    [Header("Height Placement (Raycast Down)")]
    [Tooltip("גובה ברירת מחדל אם אין פגיעה ב-Ray")]
    public float defaultY = 1.2f;

    [Tooltip("גובה עליון ממנו יירו Ray למטה (גבוה מכל האובייקטים)")]
    public float rayTopY = 50f;

    [Tooltip("מרחק מקסימלי ל-Ray למטה")]
    public float rayMaxDistance = 200f;

    [Tooltip("היסט מעל נקודת הפגיעה (שישב מעל רכבת/קרקע)")]
    public float aboveHitOffset = 1.0f;

    [Tooltip("שכבות להיט רלוונטיות (Ground/Trains/Environment)")]
    public LayerMask obstacleMask = ~0; // ברירת מחדל: הכל

    private float nextSpawnZ;
    private HashSet<Vector3> placed = new HashSet<Vector3>();

    void Start()
    {
        if (!player) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        nextSpawnZ = (player ? player.position.z : 0f) + startAheadZ;
    }

    void Update()
    {
        if (!player || !coinPrefab) return;

        float targetZ = player.position.z + distanceAheadZ;

        while (nextSpawnZ <= targetZ)
        {
            // בחר מסלול X אקראי לטור הזה
            int laneIndex = Random.Range(0, lanesX.Length);
            float x = lanesX[laneIndex];

            // מיקום קדימה לאורך Z
            float z = nextSpawnZ + Random.Range(-jitterZ, jitterZ);

            // קבע גובה לפי Raycast מלמעלה-למטה
            float y = ResolveYWithRaycast(x, z);

            Vector3 pos = new Vector3(x, y, z);

            // מניעת כפילויות (קירוב)
            Vector3 key = RoundV3(pos, 2);
            if (!placed.Contains(key))
            {
                Instantiate(coinPrefab, pos, Quaternion.identity);
                placed.Add(key);
            }

            nextSpawnZ += spacingZ;
        }
    }

    float ResolveYWithRaycast(float x, float z)
    {
        Vector3 origin = new Vector3(x, rayTopY, z);
        Ray ray = new Ray(origin, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, rayMaxDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y + aboveHitOffset; // שים קצת מעל מה שפגענו בו
        }
        return defaultY; // גיבוי אם לא הייתה פגיעה
    }

    Vector3 RoundV3(Vector3 v, int digits)
    {
        float m = Mathf.Pow(10f, digits);
        return new Vector3(Mathf.Round(v.x * m) / m, Mathf.Round(v.y * m) / m, Mathf.Round(v.z * m) / m);
    }

    // כלי עזר לראות היכן נטיל מטבעות (בחר את האובייקט בסצנה)
    void OnDrawGizmosSelected()
    {
        if (lanesX == null) return;
        Gizmos.color = Color.yellow;
        float baseZ = player ? player.position.z : 0f;
        for (int i = 0; i < lanesX.Length; i++)
        {
            Vector3 a = new Vector3(lanesX[i], defaultY, baseZ + 2f);
            Vector3 b = new Vector3(lanesX[i], defaultY, baseZ + 2f + 40f);
            Gizmos.DrawLine(a, b);
        }
    }
}
