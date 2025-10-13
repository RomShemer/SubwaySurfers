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

    [Tooltip("רדיוס הבדיקה לחפיפה")]
    public float overlapRadius = 0.6f;

    [Header("Grounding (optional)")]
    [Tooltip("TRUE: עושים Raycast לקרקע מתחת לנקודת ההנחה; FALSE: אין Raycast ומניחים בגובה ה-Road")]
    public bool snapToGround = false;

    [Tooltip("אם snapToGround=TRUE: ליירים שנחשבים 'קרקע/מסילה' ל-Raycast")]
    public LayerMask groundMask = ~0;

    [Tooltip("אם TRUE: בודקים אזור אסור סביב נקודת ההנחה (למנוע הנחה על גגות מנהרות וכו')")]
    public bool checkForbiddenZones = false;

    [Tooltip("ליירים שנחשבים 'אסורים' (אם checkForbiddenZones=TRUE)")]
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

    [Tooltip("מרחק מינימלי מרכבת למכשול שאינו רכבת אחריה (באותה מסילה, בזי)")]
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

    /// הנחה בפועל (ריווח, כפילות, חפיפה, וגובה – עם/בלי Raycast)
    private bool TryPlace(RoadPiece road, int laneIndex, float laneWorldX, float worldZ, Quaternion laneRot, string kind)
    {
        if (!SpacingAllows(kind, laneIndex, worldZ)) return false;

        if (!WorldZToLocalZ(road, worldZ, out float localZ)) return false;
        int zBin = Mathf.RoundToInt(localZ / Mathf.Max(0.01f, zBinSize));
        var slot = new Slot(laneIndex, zBin);
        var set = _occupiedByRoad[road];
        if (set.Contains(slot)) return false;

        Vector3 worldPoint;

        if (snapToGround)
        {
            if (!GetGroundPointWorld(road, laneWorldX, worldZ, out worldPoint))
                return false;

            if (checkForbiddenZones && IsInForbiddenArea(worldPoint))
                return false;
        }
        else
        {
            // בלי Raycast – מניחים על גובה ה-Road
            float y = GetRoadBaseY(road);
            worldPoint = new Vector3(laneWorldX, y, worldZ);
            // אם תרצי – אפשר לאפשר forbidden גם כאן:
            // if (checkForbiddenZones && IsInForbiddenArea(worldPoint)) return false;
        }

        // בדיקת חפיפה פיזית (מול פריטים שכבר הונחו)
        if (Physics.CheckSphere(worldPoint + Vector3.up * 0.05f, overlapRadius, overlapMask, QueryTriggerInteraction.Collide))
            return false;

        var prefab = PickPrefab(kind);
        if (!prefab) return false;

        var obj = Instantiate(prefab, road.transform, false);

        // world->local (כי ממוקמים כילד של ה-Road)
        Vector3 local = road.transform.InverseTransformPoint(worldPoint);
        obj.transform.localPosition = local + Vector3.up * yOffset;
        obj.transform.localRotation = laneRot;
        obj.transform.localScale = Vector3.one;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }

        set.Add(slot);
        RememberPlacement(kind, laneIndex, worldZ);
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
}