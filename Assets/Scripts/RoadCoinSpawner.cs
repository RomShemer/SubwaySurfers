/*using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoadCoinSpawner : MonoBehaviour
{
    [Header("Coin prefab")]
    public GameObject coinPrefab;

    [Header("Lanes (LOCAL X, left→right)")]
    public float[] lanesLocalX = new float[] { -1.2f, 0f, 1.2f };

    [Header("Column placement (LOCAL)")]
    public float yLocalOffset = 0.5f;
    public float spacingLocalZ = 2.8f;      // distance between coins along Z in a column
    public float marginStartZ = 0.8f;       // keep headroom at start of tile
    public float marginEndZ = 0.8f;         // keep headroom at end of tile

    [Header("Per-tile spawn chance")]
    [Range(0f,1f)] public float tileHasCoinsChance = 0.85f; // sometimes a tile has no coins at all

    [Header("Column pattern weights")]
    public float weightSingle  = 1.0f;  // one lane
    public float weightDouble  = 1.2f;  // two lanes
    public float weightTriple  = 0.6f;  // three lanes

    [Header("Avoid placing over trains/obstacles")]
    public LayerMask obstacleMask;      // include Train/Obstacle layers
    public float coinRadius = 0.28f;

    [Header("Link: coins → obstacle/train ahead (per chosen column)")]
    [Range(0f,1f)] public float obstacleAfterColumnChance = 0.5f; // chance per selected column
    public float aheadDistanceWorld = 6f;                          // how far ahead from column start
    public string aheadKind = "obstacle";                          // "obstacle" or "train"

    [Header("Auto-spawn on Start")]
    public bool spawnOnStart = true;

    private BoxCollider col;

    void Awake()
    {
        col = GetComponent<BoxCollider>();
    }

    void Start()
    {
        if (spawnOnStart) SpawnCoinsOnThisTile();
    }

    public void SpawnCoinsOnThisTile()
    {
        if (!coinPrefab || lanesLocalX == null || lanesLocalX.Length == 0) return;
        if (Random.value > tileHasCoinsChance) return;

        float lenZ = col.size.z;
        float zMin = col.center.z - (lenZ * 0.5f) + marginStartZ;
        float zMax = col.center.z + (lenZ * 0.5f) - marginEndZ;

        // pick a single pattern per tile
        int[] laneSet = PickLaneSet();

        // for each chosen lane, lay a full column along Z
        foreach (int li in laneSet)
        {
            if (li < 0 || li >= lanesLocalX.Length) continue;

            // compute column start world Z (we'll also use it for the "ahead" request)
            Vector3 colStartLocal = new Vector3(lanesLocalX[li], col.center.y + yLocalOffset, zMin);
            Vector3 colStartWorld = transform.TransformPoint(colStartLocal);

            for (float z = zMin; z <= zMax; z += spacingLocalZ)
            {
                Vector3 local = new Vector3(lanesLocalX[li], col.center.y + yLocalOffset, z);
                Vector3 world = transform.TransformPoint(local);

                if (!CanPlace(world)) continue;

                Instantiate(coinPrefab, world, Quaternion.identity, transform);
            }

            // optionally request an obstacle/train ahead for this column
            if (ObstacleAndTrainSpawner.I && Random.value < obstacleAfterColumnChance)
            {
                float targetWorldZ = colStartWorld.z + aheadDistanceWorld;
                ObstacleAndTrainSpawner.I.Enqueue(targetWorldZ, li, aheadKind);
            }
        }
    }

    // choose lanes for this tile: single / one of the double pairs / triple
    private int[] PickLaneSet()
    {
        float s = Mathf.Max(0.0001f, weightSingle);
        float d = Mathf.Max(0.0001f, weightDouble);
        float t = Mathf.Max(0.0001f, weightTriple);
        float r = Random.value * (s + d + t);

        if (r <= s || lanesLocalX.Length == 1)
        {
            // single: random lane
            return new int[] { Random.Range(0, lanesLocalX.Length) };
        }
        else if (r <= s + d || lanesLocalX.Length == 2)
        {
            // double: pick one of (L+M), (M+R), (L+R) depending on lane count
            if (lanesLocalX.Length >= 3)
            {
                int pick = Random.Range(0, 3);
                if (pick == 0) return new int[] { 0, 1 };         // L + M
                if (pick == 1) return new int[] { 1, 2 };         // M + R
                return new int[] { 0, 2 };                         // L + R
            }
            else
            {
                return new int[] { 0, 1 };
            }
        }
        else
        {
            // triple: all lanes
            int n = lanesLocalX.Length;
            int[] all = new int[n];
            for (int i = 0; i < n; i++) all[i] = i;
            return all;
        }
    }

    private bool CanPlace(Vector3 worldPos)
    {
        return !Physics.CheckSphere(worldPos, coinRadius, obstacleMask, QueryTriggerInteraction.Ignore);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!col) col = GetComponent<BoxCollider>();
        if (!col) return;

        Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);

        float lenZ = col.size.z;
        float zMin = col.center.z - (lenZ * 0.5f) + marginStartZ;
        float zMax = col.center.z + (lenZ * 0.5f) - marginEndZ;

        // visualize columns on all lanes (for debugging)
        foreach (float x in lanesLocalX)
        {
            for (float z = zMin; z <= zMax; z += spacingLocalZ)
            {
                Vector3 local = new Vector3(x, col.center.y + yLocalOffset, z);
                Vector3 world = transform.TransformPoint(local);
                Gizmos.DrawWireSphere(world, coinRadius);
            }
        }
    }
#endif
}
*/

using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoadCoinSpawner : MonoBehaviour
{
    [Header("Coin prefab")]
    public GameObject coinPrefab;

    [Header("Lanes (LOCAL X, left→right)")]
    public float[] lanesLocalX = new float[] { -1.2f, 0f, 1.2f };

    [Header("Column placement (LOCAL)")]
    public float yLocalOffset = 0.5f;
    public float spacingLocalZ = 2.8f;
    public float marginStartZ = 0.8f;
    public float marginEndZ = 0.8f;

    [Header("Per-tile spawn chance")]
    [Range(0f,1f)] public float tileHasCoinsChance = 0.85f;

    [Header("Column pattern (legacy weighted)")]
    public float weightSingle  = 1.0f;
    public float weightDouble  = 1.2f;
    public float weightTriple  = 0.6f;

    [Header("Random subset mode (NEW)")]
    public bool useRandomSubset = true;           // NEW: הפעלת מצב תתי־קבוצה
    [Range(0f,1f)] public float perLaneChance = 0.6f; // NEW: הסתברות לכל נתיב
    public int minColumns = 1;                    // NEW: מינימום טורים
    public int maxColumns = 2;                    // NEW: מקסימום טורים
    public bool shuffleLanes = true;              // NEW: ערבוב סדר הנתיבים
    public bool skipBlockedLanes = true;          // NEW: דלג על נתיב אם חסום ע"י רכבת/מכשול
    public float laneBlockSampleStep = 3f;        // NEW: צעדי דגימה לאורך הנתיב

    [Header("Avoid placing over trains/obstacles")]
    public LayerMask obstacleMask;
    public float coinRadius = 0.28f;

    [Header("Link: coins → obstacle/train ahead")]
    [Range(0f,1f)] public float obstacleAfterColumnChance = 0.5f;
    public float aheadDistanceWorld = 6f;
    public string aheadKind = "obstacle";

    [Header("Auto-spawn on Start")]
    public bool spawnOnStart = false;             // חשוב: עכשיו נריץ אחרי המכשולים

    private BoxCollider col;

    void Awake()
    {
        col = GetComponent<BoxCollider>();
    }

    void Start()
    {
        if (spawnOnStart) SpawnCoinsOnThisTile();
    }

    // קראי לזה אחרי שהמכשולים לרוד הזה כבר הונחו
    public void SpawnCoinsOnThisTile()
    {
        if (!coinPrefab || lanesLocalX == null || lanesLocalX.Length == 0) return;
        if (Random.value > tileHasCoinsChance) return;

        float lenZ = col.size.z;
        float zMin = col.center.z - (lenZ * 0.5f) + marginStartZ;
        float zMax = col.center.z + (lenZ * 0.5f) - marginEndZ;

        int[] laneSet = useRandomSubset ? PickLaneSubset(zMin, zMax) : PickLaneSetWeighted();

        // אם יצא ריק — לא מציבים כלום בטייל הזה
        if (laneSet == null || laneSet.Length == 0) return;

        foreach (int li in laneSet)
        {
            if (li < 0 || li >= lanesLocalX.Length) continue;

            // רנדומיזציה קטנה לפאזה – שלא כל העמודות יתחילו מאותו Z
            float phase = Random.Range(0f, spacingLocalZ * 0.8f);

            Vector3 colStartLocal = new Vector3(lanesLocalX[li], col.center.y + yLocalOffset, zMin + phase);
            Vector3 colStartWorld = transform.TransformPoint(colStartLocal);

            for (float z = zMin + phase; z <= zMax; z += spacingLocalZ)
            {
                Vector3 local = new Vector3(lanesLocalX[li], col.center.y + yLocalOffset, z);
                Vector3 world = transform.TransformPoint(local);
                if (!CanPlace(world)) continue;

                    //Instantiate(coinPrefab, world, Quaternion.identity, transform);
                if (CoinPool.I)
                    CoinPool.I.Spawn(transform, world, Quaternion.identity);
                else
                    Instantiate(coinPrefab, world, Quaternion.identity, transform); // fallback אם אין פול
            }

            // בקשה למכשול/רכבת קדימה מאותה עמודה (כמו שהיה)
            if (ObstacleAndTrainSpawner.I && Random.value < obstacleAfterColumnChance)
            {
                float targetWorldZ = colStartWorld.z + aheadDistanceWorld;
                ObstacleAndTrainSpawner.I.Enqueue(targetWorldZ, li, aheadKind);
            }
        }
    }

    // ---- Weighted legacy (שמירה על התנהגות קודמת) ----
    private int[] PickLaneSetWeighted()
    {
        float s = Mathf.Max(0.0001f, weightSingle);
        float d = Mathf.Max(0.0001f, weightDouble);
        float t = Mathf.Max(0.0001f, weightTriple);
        float r = Random.value * (s + d + t);

        if (r <= s || lanesLocalX.Length == 1)
        {
            return new int[] { Random.Range(0, lanesLocalX.Length) };
        }
        else if (r <= s + d || lanesLocalX.Length == 2)
        {
            if (lanesLocalX.Length >= 3)
            {
                int pick = Random.Range(0, 3);
                if (pick == 0) return new int[] { 0, 1 };
                if (pick == 1) return new int[] { 1, 2 };
                return new int[] { 0, 2 };
            }
            else
            {
                return new int[] { 0, 1 };
            }
        }
        else
        {
            int n = lanesLocalX.Length;
            int[] all = new int[n];
            for (int i = 0; i < n; i++) all[i] = i;
            return all;
        }
    }

    // ---- NEW: בחירת תת־קבוצה אקראית של נתיבים, עם דילוג על נתיבים חסומים ----
    private int[] PickLaneSubset(float zMin, float zMax)
    {
        int n = lanesLocalX.Length;
        if (n == 0) return null;

        // בניית רשימת אינדקסים
        System.Collections.Generic.List<int> lanes = new System.Collections.Generic.List<int>(n);
        for (int i = 0; i < n; i++) lanes.Add(i);

        if (shuffleLanes)
            Shuffle(lanes);

        // סינון נתיבים חסומים (אופציונלי)
        if (skipBlockedLanes)
        {
            for (int i = lanes.Count - 1; i >= 0; i--)
            {
                if (LaneLooksBlocked(lanes[i], zMin, zMax))
                    lanes.RemoveAt(i);
            }
        }

        if (lanes.Count == 0) return new int[0];

        // בחירה הסתברותית + אילוץ מינימום/מקסימום
        System.Collections.Generic.List<int> chosen = new System.Collections.Generic.List<int>(lanes.Count);
        foreach (var li in lanes)
            if (Random.value <= perLaneChance)
                chosen.Add(li);

        // אילוצים
        if (chosen.Count < minColumns)
        {
            // הוסף עוד אקראית עד המינימום
            for (int i = 0; i < lanes.Count && chosen.Count < minColumns; i++)
                if (!chosen.Contains(lanes[i])) chosen.Add(lanes[i]);
        }
        if (chosen.Count > maxColumns)
        {
            // הורד אקראית עד המקסימום
            while (chosen.Count > maxColumns) chosen.RemoveAt(Random.Range(0, chosen.Count));
        }

        return chosen.ToArray();
    }

    private bool LaneLooksBlocked(int laneIndex, float zMin, float zMax)
    {
        // דוגמים כמה נקודות לאורך הנתיב ובודקים אם יש מכשול/רכבת
        float laneX = lanesLocalX[laneIndex];
        for (float z = zMin; z <= zMax; z += laneBlockSampleStep)
        {
            Vector3 local = new Vector3(laneX, col.center.y + yLocalOffset, z);
            Vector3 world = transform.TransformPoint(local);
            if (!CanPlace(world)) return true; // משהו חוסם כאן
        }
        return false;
    }

    private bool CanPlace(Vector3 worldPos)
    {
        return !Physics.CheckSphere(worldPos, coinRadius, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    private static void Shuffle(System.Collections.Generic.List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    
    public void ClearCoinsOnThisTile()
    {
        // אם יש Pool – נחזיר אליו את כל המטבעות שנמצאים כילדים של האריח
        if (CoinPool.I)
        {
            CoinPool.I.ReturnAllUnder(transform);
            return;
        }

        // Fallback: אם אין Pool פעיל – פשוט נמחק את המטבעות
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var ch = transform.GetChild(i);

            // מזהה מטבע לפי קומפוננטת Coin (התאימי אם השם אצלך שונה)
            if (ch.TryGetComponent<Coin>(out _))
                Destroy(ch.gameObject);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!col) col = GetComponent<BoxCollider>();
        if (!col) return;

        Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);

        float lenZ = col.size.z;
        float zMin = col.center.z - (lenZ * 0.5f) + marginStartZ;
        float zMax = col.center.z + (lenZ * 0.5f) - marginEndZ;

        foreach (float x in lanesLocalX)
        {
            for (float z = zMin; z <= zMax; z += spacingLocalZ)
            {
                Vector3 local = new Vector3(x, col.center.y + yLocalOffset, z);
                Vector3 world = transform.TransformPoint(local);
                Gizmos.DrawWireSphere(world, coinRadius);
            }
        }
    }
#endif
}
