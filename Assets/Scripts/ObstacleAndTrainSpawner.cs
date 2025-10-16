using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObstacleAndTrainSpawner : MonoBehaviour
{
    // -------- Singleton --------
    public static ObstacleAndTrainSpawner I { get; private set; }

    [Header("Prefabs Lists")]
    public GameObject[] obstaclePrefabs;
    public GameObject[] trainPrefabs;

    [Header("Default random spawn (fallback)")]
    [Range(0f, 1f)] public float chanceToSpawnObstacle = 0.4f;
    [Range(0f, 1f)] public float chanceToSpawnTrain   = 0.3f;

    [Tooltip("היסט Y קטן מעל הרצפה/רואד. אם הפיבוט של המכשול הוא בתחתית – השאירי 0.")]
    public float yOffset = 0.0f;

    [Header("Placement")]
    [Tooltip("שם ה-Transform בכל RoadPiece שמכיל child-ים של מסלולים (אופציונלי).")]
    public string socketsRootName = "Sockets";

    [Tooltip("X לוקאלי למסלולים כשאין Sockets. לדוגמה: שלוש מסילות -2,0,+2")]
    public float[] lanesLocalX = new float[] { -2f, 0f, 2f };

    [Header("Start/Visibility gating")]
    [Tooltip("הרפרנס לשחקן (אם ריק – ימולא אוטומטית לפי Tag=Player)")]
    public Transform player;

    [Tooltip("כמה שניות לא להניח מכשולים בתחילת המשחק")]
    public float startNoSpawnSeconds = 2.0f;

    [Tooltip("לא להניח עד שהשחקן הגיע למרחק Z זה מהראשית")]
    public float startNoSpawnDistance = 20f;

    [Tooltip("להניח רק אם תחילת ה-Road לפחות במרחק זה מהשחקן (ב-Z קדימה)")]
    public float minAheadDistance = 25f;

    [Tooltip("שוליים בזי מכל קצה של האריח")]
    public float edgePaddingZ = 2.0f;

    [Header("Overlap filters (physics)")]
    [Tooltip("ליירים שמבדקים חפיפה סביב נקודת הנחה (כדי לא לדרוס עצמים קיימים)")]
    public LayerMask overlapMask = ~0;

    [Tooltip("רדיוס הבדיקה לחפיפה (בשיטה הישנה – נשאר, ואנחנו גם בודקים קפסולה לאורך כולו)")]
    public float overlapRadius = 0.6f;

    [Header("Grounding (optional)")]
    [Tooltip("TRUE: עושים Raycast לקרקע מתחת לנקודת ההנחה; FALSE: אין Raycast ומניחים בגובה ה-Road")]
    public bool snapToGround = false;

    [Tooltip("אם snapToGround=TRUE: ליירים שנחשבים 'קרקע/מסילה' ל-Raycast")]
    public LayerMask groundMask = ~0;

    [Tooltip("אם TRUE: בודקים אזור אסור סביב נקודת ההנחה (למנוע הנחה על גגות מנהרות וכו')")]
    public bool checkForbiddenZones = false;

    [Tooltip("ליירים שנחשבים 'אסורים' (למשל NoSpawnerZone)")]
    public LayerMask forbiddenMask = 0;

    [Tooltip("גובה שממנו נעשה את ה-Raycast כלפי מטה (יחסי ל-Y של ה-Road)")]
    public float raycastHeight = 10f;

    [Header("Duplicate filter (per tile logical slots)")]
    [Tooltip("רזולוציית סלוטים לאורך ה-Road, למניעת כפילויות על אותו אריח")]
    public float zBinSize = 0.5f;

    [Header("Global spawn gate")]
    [Tooltip("הדלקה אוטומטית של ספאונים לאחר עמידה בתנאי זמן/מרחק")]
    public bool autoEnableSpawning = true;

    [Tooltip("אם TRUE: דורש גם זמן וגם מרחק; אם FALSE: מספיק אחד מהם")]
    public bool requireBothTimeAndDistance = true;

    [Tooltip("סטטוס נוכחי – האם מותר להניח כעת")]
    public bool spawningEnabled = false;

    // ----- ריווח מינימלי באותה מסילה -----
    [Header("Spacing rules (per lane)")]
    [Tooltip("מרחק מינימלי בין שני מכשולים שאינם רכבות (באותה מסילה, בזי)")]
    public float minGapObstacleToObstacle = 10f;

    [Tooltip("מרחק מינימלי מרכבת/רמפה למכשול שאינו רכבת (באותה מסילה, בזי)")]
    public float minGapTrainToObstacle = 12f;

    // ----- בקשות חכמות (למשל ממטבעות) -----
    [System.Serializable]
    public class Request
    {
        public float worldZ;
        public int laneIndex;
        public string kind; // "obstacle" or "train"
    }
    private readonly List<Request> _requests = new();

    // מפת תפוסות לכל אריח (למניעת כפילות סלוטים)
    private readonly Dictionary<RoadPiece, HashSet<Slot>> _occupiedByRoad = new();

    private struct Slot
    {
        public int lane;
        public int zBin;
        public Slot(int lane, int zBin) { this.lane = lane; this.zBin = zBin; }
    }

    // "הנחה אחרונה" לכל מסילה (world Z)
    private readonly Dictionary<int, float> _lastNonTrainZByLane = new();
    private readonly Dictionary<int, float> _lastTrainZByLane    = new();

    private float _startTime;

    // -------- מודעות אורך + אזורים אסורים לאורך מלא --------
    [Header("Size-aware placement")]
    [Tooltip("התחשבות במידות הפריפאב בעת ההצבה ובבדיקות קפסולה")]
    [SerializeField] bool usePrefabLength = true;

    [Tooltip("אורך ברירת מחדל (במטרים) אם לא ניתן למדוד מהפריפאב")]
    [SerializeField] float defaultPrefabLengthZ = 5f;

    [Tooltip("מרווח נוסף לרדיוס הקפסולה בבדיקות (מטר)")]
    [SerializeField] float capsuleExtraRadius = 0.1f;

    // per RoadPiece → per lane → רשימת קטעים [zStart,zEnd] בעולם שכבר תפוסים (לוגית)
    private readonly Dictionary<RoadPiece, Dictionary<int, List<Vector2>>> _rangesByRoadLane = new();

    // -------- מעקב אחרי קטעי רכבות לכל מסילה --------
    [Header("Train lanes rule")]
    [Tooltip("מונע מצב שבו כל המסילות מכוסות ע\"י רכבות באותו מקטע Z")]
    public bool preventAllLanesCoveredByTrains = true;

    [Tooltip("תגיות שנחשבות 'דמויות־רכבת' (רכבות, רמפות וכו')")]
    public string[] trainLikeTags = new[] { "Train", "Ramp" };

    [Tooltip("תגיות מכשולים שחייבות לשמור מרחק מקטעי רכבת/רמפה")]
    public string[] obstacleTagsRequireTrainGap = new[] { "rollAndJumpObstacle", "rollObstacle", "jumpObstacle" };

    // per RoadPiece → per lane → רשימת קטעי רכבת [zStart,zEnd]
    private readonly Dictionary<RoadPiece, Dictionary<int, List<Vector2>>> _trainRangesByRoadLane = new();

    // Cache למידות פריפאבים
    private readonly Dictionary<GameObject, float> _prefabLenCache = new();
    private readonly Dictionary<GameObject, Vector3> _prefabBoundsCache = new();

    // =====================================================================
    private void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        _startTime = Time.time;

        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    private void Update()
    {
        if (!autoEnableSpawning || spawningEnabled) return;

        bool timeOk = (Time.time - _startTime) >= startNoSpawnSeconds;
        bool distOk = player && player.position.z >= startNoSpawnDistance;
        spawningEnabled = requireBothTimeAndDistance ? (timeOk && distOk) : (timeOk || distOk);
    }

    // ---------- Public API ----------
    public void Enqueue(float worldZ, int laneIndex, string kind = "obstacle")
    {
        _requests.Add(new Request { worldZ = worldZ, laneIndex = laneIndex, kind = kind });
    }

    public void SpawnOnRoad(RoadPiece road)
    {
        if (!road || !spawningEnabled) return;

        if (!_occupiedByRoad.ContainsKey(road))
            _occupiedByRoad[road] = new HashSet<Slot>();

        if (!SpawnsGloballyAllowed() || !RoadFarEnoughAhead(road))
            return;

        ConsumeQueuedForRoad(road);
        TryRandomSpawnOnRoad(road);
    }

    public void ClearRoadOccupancy(RoadPiece road)
    {
        if (road && _occupiedByRoad.ContainsKey(road))
            _occupiedByRoad[road].Clear();

        if (road != null && _rangesByRoadLane.ContainsKey(road))
            _rangesByRoadLane[road].Clear();

        if (road != null && _trainRangesByRoadLane.ContainsKey(road))
            _trainRangesByRoadLane[road].Clear();
    }

    // ---------- Internals ----------
    private float PlayerZ() => player ? player.position.z : float.NegativeInfinity;

    private bool SpawnsGloballyAllowed()
    {
        bool timeOk = (Time.time - _startTime) >= startNoSpawnSeconds;
        bool distOk = PlayerZ() >= startNoSpawnDistance;
        return timeOk || distOk;
    }

    private bool RoadFarEnoughAhead(RoadPiece road)
    {
        if (!TryGetTileZRangeWorld(road, out float zMinW, out _)) return false;
        return (zMinW - PlayerZ()) >= minAheadDistance;
    }

    private void ConsumeQueuedForRoad(RoadPiece road)
    {
        if (_requests.Count == 0) return;
        if (!TryGetTileZRangeWorld(road, out float zMinW, out float zMaxW)) return;

        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            var r = _requests[i];
            if (r.worldZ < zMinW || r.worldZ > zMaxW) continue;
            if ((r.worldZ - PlayerZ()) < minAheadDistance) continue;

            if (!TryGetLaneWorldX(road, r.laneIndex, out float laneWorldX, out Quaternion laneRot)) continue;

            float rzMin = zMinW + edgePaddingZ;
            float rzMax = zMaxW - edgePaddingZ;
            float clampedZ = Mathf.Clamp(r.worldZ, rzMin, rzMax);

            if (TryPlace(road, r.laneIndex, laneWorldX, clampedZ, laneRot, r.kind))
                _requests.RemoveAt(i);
        }
    }

    private void TryRandomSpawnOnRoad(RoadPiece road)
    {
        if (obstaclePrefabs.Length == 0 && trainPrefabs.Length == 0) return;
        if (!TryGetTileZRangeWorld(road, out float zMinW, out float zMaxW)) return;
        if ((zMinW - PlayerZ()) < minAheadDistance) return;

        float roll = Random.value;
        string kind = null;
        if (roll < chanceToSpawnObstacle && obstaclePrefabs.Length > 0) kind = "obstacle";
        else if (roll < chanceToSpawnObstacle + chanceToSpawnTrain && trainPrefabs.Length > 0) kind = "train";
        if (kind == null) return;

        int laneIdx = Random.Range(0, GetLaneCount(road));
        if (!TryGetLaneWorldX(road, laneIdx, out float laneWorldX, out Quaternion laneRot)) return;

        float zFrom = Mathf.Max(zMinW + edgePaddingZ, PlayerZ() + minAheadDistance);
        float zTo   = zMaxW - edgePaddingZ;
        if (zTo <= zFrom) return;

        float worldZ = Random.Range(zFrom, zTo);
        TryPlace(road, laneIdx, laneWorldX, worldZ, laneRot, kind);
    }

    // ---- spacing rules per lane ----
    private bool SpacingAllows(string kind, int laneIndex, float worldZ)
    {
        if (kind == "train") return true; // רכבות יכולות להיות צמודות

        if (_lastNonTrainZByLane.TryGetValue(laneIndex, out float lastObsZ))
            if (worldZ - lastObsZ < minGapObstacleToObstacle) return false;

        if (_lastTrainZByLane.TryGetValue(laneIndex, out float lastTrainZ))
            if (worldZ - lastTrainZ < minGapTrainToObstacle) return false;

        return true;
    }

    private void RememberPlacement(string kind, int laneIndex, float worldZ)
    {
        if (kind == "train") _lastTrainZByLane[laneIndex] = worldZ;
        else                 _lastNonTrainZByLane[laneIndex] = worldZ;
    }

    // =====================================================================
    //  TryPlace – מציב בפועל + כללי מרחק מרכבות לרמפות עבור מכשולים מסוימים
    // =====================================================================
    private bool TryPlace(RoadPiece road, int laneIndex, float laneWorldX, float worldZ, Quaternion laneRot, string kind)
    {
        if (!SpacingAllows(kind, laneIndex, worldZ)) return false;
        if (!WorldZToLocalZ(road, worldZ, out float localZ)) return false;

        int zBin = Mathf.RoundToInt(localZ / Mathf.Max(0.01f, zBinSize));
        var slot = new Slot(laneIndex, zBin);
        var set  = _occupiedByRoad[road];
        if (set.Contains(slot)) return false;

        // בוחרים פריפאב כדי למדוד מימדים
        var prefab = PickPrefab(kind);
        if (!prefab) return false;

        float lenZ    = GetPrefabLengthZ(prefab);            // אורך Z
        float halfLen = lenZ * 0.5f;
        var boundsXYZ = GetPrefabBoundsSize(prefab);         // רוחב/גובה נומינליים
        float halfWidthX = Mathf.Max(boundsXYZ.x * 0.5f, overlapRadius);
        float heightY    = Mathf.Max(boundsXYZ.y, 1.0f);

        // גבולות האריח בעולם
        if (!TryGetTileZRangeWorld(road, out float zMinW, out float zMaxW)) return false;

        // רק מרכזים שנכנסים עם האורך והריפוד
        float minCenter = zMinW + edgePaddingZ + halfLen;
        float maxCenter = zMaxW - edgePaddingZ - halfLen;
        if (maxCenter < minCenter) return false;

        float clampedCenterZ = Mathf.Clamp(worldZ, minCenter, maxCenter);

        // הטווח שהאובייקט יתפוס במסילה
        Vector2 seg = new Vector2(clampedCenterZ - halfLen, clampedCenterZ + halfLen);

        // בדיקת חיתוך לוגי מול קטעים קיימים במסילה (באותו RoadPiece)
        var ranges = GetRangesList(road, laneIndex);
        for (int i = 0; i < ranges.Count; i++)
            if (Intersects(seg, ranges[i])) return false;

        // סיווגים
        bool isTrainLike = IsTrainLikePrefab(prefab) || kind == "train";
        bool isObstacleThatNeedsTrainGap = NeedsTrainGap(prefab);

        // ---- כלל: מכשולים מסוימים אסור להציב בתוך/קרוב לרכבת/רמפה ----
        if (isObstacleThatNeedsTrainGap)
        {
            var trainRanges = GetTrainRangesList(road, laneIndex);
            if (!TrainGapAllows(seg, trainRanges, minGapTrainToObstacle))
                return false;
        }

        // ---- כלל קיים: לא לאפשר שכל המסילות מכוסות ע"י רכבות באותו מקטע Z ----
        if (preventAllLanesCoveredByTrains && isTrainLike)
        {
            int lanesCount = GetLaneCount(road);
            if (WouldFillAllTrainLanes(road, seg, laneIndex, lanesCount))
                return false;
        }

        // נקודות קפסולה לאורך האורך (קצה-לקצה) + גובה
        float yA = GetProbeYAt(road, laneWorldX, seg.x, heightY);
        float yB = GetProbeYAt(road, laneWorldX, seg.y, heightY);
        Vector3 pA = new Vector3(laneWorldX, yA, seg.x);
        Vector3 pB = new Vector3(laneWorldX, yB, seg.y);
        float capsuleRad = halfWidthX + capsuleExtraRadius;

        // --- אזור אסור לאורך מלא ---
        if (checkForbiddenZones && forbiddenMask != 0)
            if (Physics.CheckCapsule(pA, pB, capsuleRad, forbiddenMask, QueryTriggerInteraction.Collide))
                return false;

        // נקודת העמדה (מרכז) + גובה להצבה בפועל
        Vector3 worldPoint;
        if (snapToGround)
        {
            if (!GetGroundPointWorld(road, laneWorldX, clampedCenterZ, out worldPoint))
                return false;
        }
        else
        {
            float y = GetRoadBaseY(road);
            worldPoint = new Vector3(laneWorldX, y, clampedCenterZ);
        }

        // --- חפיפה פיזית לאורך מלא מול עצמים קיימים ---
        if (Physics.CheckCapsule(pA, pB, capsuleRad, overlapMask, QueryTriggerInteraction.Collide))
            return false;

        // נשאיר גם בדיקת מרכז לשמירה לאחוריות
        if (Physics.CheckSphere(worldPoint + Vector3.up * 0.05f, overlapRadius, overlapMask, QueryTriggerInteraction.Collide))
            return false;

        // יצירה בפועל
        var obj = Instantiate(prefab, road.transform, false);

        // world->local (כי ממוקמים כילד של ה-Road)
        Vector3 localPos = road.transform.InverseTransformPoint(worldPoint);
        obj.transform.localPosition = localPos + Vector3.up * yOffset;
        obj.transform.localRotation = laneRot;
        obj.transform.localScale = Vector3.one;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }

        // ספרי תפוסה לוגית + קטע אורך למסילה
        set.Add(slot);
        InsertRangeSorted(ranges, seg);

        // אם זו רכבת/רמפה — נשמור גם במפת הרכבות למסילה
        if (isTrainLike)
        {
            var trainRanges = GetTrainRangesList(road, laneIndex);
            InsertRangeSorted(trainRanges, seg);
        }

        // ריווח לפי הסוג (קיים מהעבר)
        RememberPlacement(kind, laneIndex, clampedCenterZ);
        return true;
    }

    // ===== עזרי סיווג =====
    private bool IsTrainLikePrefab(GameObject prefab)
    {
        if (!prefab) return false;
        foreach (var tag in trainLikeTags)
        {
            if (!string.IsNullOrEmpty(tag) && prefab.CompareTag(tag)) return true;
        }
        return false;
    }

    private bool NeedsTrainGap(GameObject prefab)
    {
        if (!prefab || obstacleTagsRequireTrainGap == null) return false;
        foreach (var tag in obstacleTagsRequireTrainGap)
        {
            if (!string.IsNullOrEmpty(tag) && prefab.CompareTag(tag)) return true;
        }
        return false;
    }

    // דרישת מרחק ממקטעי רכבת/רמפה קיימים: segObstacle מול segTrain מורחב בגודל המרחק
    private bool TrainGapAllows(Vector2 segObstacle, List<Vector2> trainRanges, float gap)
    {
        if (trainRanges == null || trainRanges.Count == 0) return true;

        // נוודא: מכשול צריך להיות לפני תחילת הרכבת - gap, או אחרי סוף הרכבת + gap
        // נשתמש בהרחבת מקטע הרכבת לשני הצדדים ומניעת חיתוך.
        for (int i = 0; i < trainRanges.Count; i++)
        {
            Vector2 t = trainRanges[i];
            Vector2 tExpanded = new Vector2(t.x - gap, t.y + gap);
            if (Intersects(segObstacle, tExpanded))
                return false;
        }
        return true;
    }

    private bool WorldZToLocalZ(RoadPiece road, float worldZ, out float localZ)
    {
        Vector3 worldPoint = new Vector3(road.transform.position.x, road.transform.position.y, worldZ);
        Vector3 local = road.transform.InverseTransformPoint(worldPoint);
        localZ = local.z;
        return true;
    }

    private GameObject PickPrefab(string kind)
    {
        if (kind == "train" && trainPrefabs.Length > 0)
            return trainPrefabs[Random.Range(0, trainPrefabs.Length)];
        if (obstaclePrefabs.Length > 0)
            return obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
        return null;
    }

    // ---- tile extents helpers ----
    private bool TryGetTileZRangeWorld(RoadPiece road, out float zMinW, out float zMaxW)
    {
        zMinW = zMaxW = 0f;

        var bc = road.GetComponent<BoxCollider>();
        if (bc)
        {
            float lenZ = bc.size.z;
            Vector3 a = road.transform.TransformPoint(new Vector3(0, 0, bc.center.z - lenZ * 0.5f));
            Vector3 b2 = road.transform.TransformPoint(new Vector3(0, 0, bc.center.z + lenZ * 0.5f));
            zMinW = Mathf.Min(a.z, b2.z);
            zMaxW = Mathf.Max(a.z, b2.z);
            return true;
        }

        var r = road.GetComponentInChildren<Renderer>();
        if (!r) return false;
        var b = r.bounds;
        zMinW = b.min.z; zMaxW = b.max.z;
        return true;
    }

    private bool TryGetLaneWorldX(RoadPiece road, int laneIndex, out float worldX, out Quaternion rot)
    {
        var sockets = road.transform.Find(socketsRootName);
        if (sockets && sockets.childCount > 0)
        {
            int idx = Mathf.Clamp(laneIndex, 0, sockets.childCount - 1);
            var t = sockets.GetChild(idx);
            worldX = t.position.x;
            rot = t.rotation;
            return true;
        }

        int li = Mathf.Clamp(laneIndex, 0, lanesLocalX.Length - 1);
        Vector3 local = new Vector3(lanesLocalX[li], 0f, 0f);
        Vector3 world = road.transform.TransformPoint(local);
        worldX = world.x;
        rot = road.transform.rotation;
        return true;
    }

    // ---- Grounding helpers ----
    private float GetRoadBaseY(RoadPiece road)
    {
        var bc = road.GetComponent<BoxCollider>();
        if (bc)
            return road.transform.TransformPoint(new Vector3(0f, bc.center.y, 0f)).y;
        return road.transform.position.y;
    }

    private bool GetGroundPointWorld(RoadPiece road, float worldX, float worldZ, out Vector3 groundWorld)
    {
        float baseY = road.transform.position.y + raycastHeight;
        Vector3 origin = new Vector3(worldX, baseY, worldZ);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundWorld = hit.point;
            return true;
        }

        // fallback — שמים בגובה הרואד גם אם הרייקאסט לא פגע
        float y = GetRoadBaseY(road);
        groundWorld = new Vector3(worldX, y, worldZ);
        return true;
    }

    private bool IsInForbiddenArea(Vector3 worldPoint)
    {
        if (!checkForbiddenZones || forbiddenMask == 0) return false;
        const float probeRadius = 0.35f;
        return Physics.CheckSphere(worldPoint, probeRadius, forbiddenMask, QueryTriggerInteraction.Collide);
    }

    private int GetLaneCount(RoadPiece road)
    {
        var sockets = road.transform.Find(socketsRootName);
        return (sockets && sockets.childCount > 0) ? sockets.childCount : lanesLocalX.Length;
    }

    // --- Scene hooks ---
    private void OnEnable()   { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable()  { SceneManager.sceneLoaded -= OnSceneLoaded; }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ResetGateAndState(); }
    public  void OnGameRestart() { ResetGateAndState(); }

    private void ResetGateAndState()
    {
        _startTime = Time.time;
        spawningEnabled = false;
        _requests.Clear();
        _occupiedByRoad.Clear();
        _lastNonTrainZByLane.Clear();
        _lastTrainZByLane.Clear();

        _rangesByRoadLane.Clear();
        _trainRangesByRoadLane.Clear();

        RebindPlayer();
    }

    private void RebindPlayer()
    {
        if (!player || !player.gameObject.activeInHierarchy)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    public void RegisterPlayer(Transform t) { player = t; }

    // --------- Helpers for length/size-aware placement ---------
    private float GetPrefabLengthZ(GameObject prefab)
    {
        if (!usePrefabLength || !prefab) return defaultPrefabLengthZ;
        if (_prefabLenCache.TryGetValue(prefab, out var len)) return len;

        float best = 0f;

        // Colliders bounds
        var cols = prefab.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols) best = Mathf.Max(best, c.bounds.size.z);

        // Renderers bounds
        if (best <= 0f)
        {
            var rends = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends) best = Mathf.Max(best, r.bounds.size.z);
        }

        // Mesh bounds (local) – כמוצא אחרון
        if (best <= 0f)
        {
            var mfs = prefab.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in mfs) if (mf.sharedMesh) best = Mathf.Max(best, mf.sharedMesh.bounds.size.z);
        }

        if (best <= 0f) best = defaultPrefabLengthZ;
        _prefabLenCache[prefab] = best;
        return best;
    }

    private Vector3 GetPrefabBoundsSize(GameObject prefab)
    {
        if (_prefabBoundsCache.TryGetValue(prefab, out var sz)) return sz;

        Vector3 best = Vector3.zero;

        var cols = prefab.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            var b = c.bounds.size;
            best = new Vector3(Mathf.Max(best.x, b.x), Mathf.Max(best.y, b.y), Mathf.Max(best.z, b.z));
        }

        if (best == Vector3.zero)
        {
            var rends = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                var b = r.bounds.size;
                best = new Vector3(Mathf.Max(best.x, b.x), Mathf.Max(best.y, b.y), Mathf.Max(best.z, b.z));
            }
        }

        if (best == Vector3.zero)
        {
            var mfs = prefab.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in mfs)
            {
                if (!mf.sharedMesh) continue;
                var b = mf.sharedMesh.bounds.size;
                best = new Vector3(Mathf.Max(best.x, b.x), Mathf.Max(best.y, b.y), Mathf.Max(best.z, b.z));
            }
        }

        if (best == Vector3.zero) best = new Vector3(overlapRadius * 2f, 1.0f, defaultPrefabLengthZ);

        _prefabBoundsCache[prefab] = best;
        return best;
    }

    private Dictionary<int, List<Vector2>> GetLaneRangesMap(RoadPiece road)
    {
        if (!_rangesByRoadLane.TryGetValue(road, out var lanes))
        {
            lanes = new Dictionary<int, List<Vector2>>();
            _rangesByRoadLane[road] = lanes;
        }
        return lanes;
    }

    private List<Vector2> GetRangesList(RoadPiece road, int laneIndex)
    {
        var lanes = GetLaneRangesMap(road);
        if (!lanes.TryGetValue(laneIndex, out var list))
        {
            list = new List<Vector2>(4);
            lanes[laneIndex] = list;
        }
        return list;
    }

    // --- Train ranges helpers ---
    private Dictionary<int, List<Vector2>> GetTrainLaneRangesMap(RoadPiece road)
    {
        if (!_trainRangesByRoadLane.TryGetValue(road, out var lanes))
        {
            lanes = new Dictionary<int, List<Vector2>>();
            _trainRangesByRoadLane[road] = lanes;
        }
        return lanes;
    }

    private List<Vector2> GetTrainRangesList(RoadPiece road, int laneIndex)
    {
        var lanes = GetTrainLaneRangesMap(road);
        if (!lanes.TryGetValue(laneIndex, out var list))
        {
            list = new List<Vector2>(4);
            lanes[laneIndex] = list;
        }
        return list;
    }

    private bool WouldFillAllTrainLanes(RoadPiece road, Vector2 newTrainSeg, int placingLane, int laneCount)
    {
        // סופרים כמה מסילות יהיו מכוסות ע"י רכבת במקטע הזה אם נציב גם את החדשה
        int covered = 0;
        for (int lane = 0; lane < laneCount; lane++)
        {
            bool hasTrainHere = false;

            // אם זו המסילה הנוכחית – נספור כאילו כבר הונחה
            if (lane == placingLane)
            {
                hasTrainHere = true;
            }
            else
            {
                var trenRanges = GetTrainRangesList(road, lane);
                for (int i = 0; i < trenRanges.Count; i++)
                {
                    if (Intersects(newTrainSeg, trenRanges[i]))
                    {
                        hasTrainHere = true;
                        break;
                    }
                }
            }

            if (hasTrainHere) covered++;
        }

        // אם כל המסילות מכוסות – אסור להציב
        return covered >= laneCount && laneCount > 0;
    }

    private static bool Intersects(Vector2 a, Vector2 b)
    {
        // true אם יש חיתוך/נגיעה בין הקטעים [a.x,a.y] ו-[b.x,b.y]
        return !(a.y <= b.x || b.y <= a.x);
    }

    private static void InsertRangeSorted(List<Vector2> list, Vector2 seg)
    {
        int i = 0;
        for (; i < list.Count; i++)
            if (seg.x < list[i].x) break;
        list.Insert(i, seg);
    }

    private float GetProbeYAt(RoadPiece road, float worldX, float worldZ, float objHeight)
    {
        float baseY;
        if (snapToGround)
        {
            Vector3 dummy;
            if (GetGroundPointWorld(road, worldX, worldZ, out dummy))
                baseY = dummy.y;
            else
                baseY = GetRoadBaseY(road);
        }
        else
        {
            baseY = GetRoadBaseY(road);
        }
        // מרכז קפסולה: חצי גובה האובייקט + yOffset קטן כדי לכלול נפח
        return baseY + (objHeight * 0.5f) + Mathf.Max(0f, yOffset);
    }
}