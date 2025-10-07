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
    public float yOffset = 0.5f;

    [Header("Placement")]
    [Tooltip("Parent under each RoadPiece containing lane sockets (optional).")]
    public string socketsRootName = "Sockets";
    [Tooltip("Fallback lane X positions (local), used if no sockets exist.")]
    public float[] lanesLocalX = new float[] { -2f, 0f, 2f };

    [Header("Start/Visibility gating")]
    [Tooltip("Reference to the player (found by Tag \"Player\" if empty).")]
    public Transform player;
    [Tooltip("No spawns before this time passes (seconds).")]
    public float startNoSpawnSeconds = 2.0f;
    [Tooltip("No spawns until player has reached at least this Z distance from origin.")]
    public float startNoSpawnDistance = 20f;
    [Tooltip("Every placement must be at least this many meters ahead of the player.")]
    public float minAheadDistance = 25f;
    [Tooltip("Padding from both Z edges of the road tile.")]
    public float edgePaddingZ = 2.0f;

    [Header("Overlap filter (physics)")]
    [Tooltip("Layers to check when testing for spawn overlap.")]
    public LayerMask overlapMask = ~0;
    public float overlapRadius = 0.6f;

    [Header("Duplicate filter (per tile logical slots)")]
    [Tooltip("Size of a Z bin in local units used to deduplicate placements on the same tile.")]
    public float zBinSize = 0.5f;

    [Header("Global spawn gate")]
    [Tooltip("Auto-enable spawning after time/distance conditions")]
    public bool autoEnableSpawning = true;

    [Tooltip("Require BOTH time AND distance to enable (true) or EITHER (false)")]
    public bool requireBothTimeAndDistance = true;

    public bool spawningEnabled = false;

    // ----- Requests sent by coins: place obstacle/train ahead on a lane -----
    [System.Serializable]
    public class Request
    {
        public float worldZ;
        public int laneIndex;
        public string kind; // "obstacle" or "train"
    }
    private readonly List<Request> _requests = new List<Request>();

    // per-road occupancy map: which (lane, zBin) are already used on this tile
    private readonly Dictionary<RoadPiece, HashSet<Slot>> _occupiedByRoad = new();

    private struct Slot
    {
        public int lane;
        public int zBin;
        public Slot(int lane, int zBin) { this.lane = lane; this.zBin = zBin; }
    }

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
        if (!autoEnableSpawning) return;        // אם את רוצה שליטה ידנית – כבי באינספקטור
        if (spawningEnabled) return;            // כבר דלוק

        bool timeOk = (Time.time - _startTime) >= startNoSpawnSeconds;
        bool distOk = player && player.position.z >= startNoSpawnDistance;

        spawningEnabled = requireBothTimeAndDistance ? (timeOk && distOk) : (timeOk || distOk);
        // טיפ: אם תרצי לראות שזה נדלק בפועל:
        // if (spawningEnabled) Debug.Log("[Spawner] Spawning enabled");
    }



    // ---------- Public API ----------

    /// Coins call this to request an obstacle/train ahead at a specific world Z and lane
    public void Enqueue(float worldZ, int laneIndex, string kind = "obstacle")
    {
        _requests.Add(new Request { worldZ = worldZ, laneIndex = laneIndex, kind = kind });
    }

    /// Call this whenever a road tile becomes active (initial build and on recycle)
    public void SpawnOnRoad(RoadPiece road)
    {
        if (!road) return;
        
        if (!spawningEnabled) return; 

        if (!_occupiedByRoad.ContainsKey(road))
            _occupiedByRoad[road] = new HashSet<Slot>();

        // Global/start gating + ensure the whole tile starts far enough ahead
        if (!SpawnsGloballyAllowed() || !RoadFarEnoughAhead(road))
            return;

        // 1) consume queued coin-requests that fall inside this tile
        ConsumeQueuedForRoad(road);

        // 2) optional single random spawn on this tile
        TryRandomSpawnOnRoad(road);
    }

    // Optional: clear occupancy when a tile is recycled (call from your pool return)
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
        // מותר אחרי זמן או אחרי מרחק – החליפי ל && אם תרצי גם וגם
        return timeOk || distOk;
    }

    // Ensure the tile begins at least minAheadDistance in front of the player
    private bool RoadFarEnoughAhead(RoadPiece road)
    {
        if (!TryGetTileZRangeWorld(road, out float zMinW, out float zMaxW)) return false;
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

            // Leave request in queue if too close to the player yet
            if ((r.worldZ - PlayerZ()) < minAheadDistance) continue;

            if (!TryGetLaneWorldX(road, r.laneIndex, out float laneWorldX, out Quaternion laneRot)) continue;

            // Clamp to edge padding
            float rzMin = zMinW + edgePaddingZ;
            float rzMax = zMaxW - edgePaddingZ;
            float clampedZ = Mathf.Clamp(r.worldZ, rzMin, rzMax);

            Vector3 worldPointOnZ = new Vector3(laneWorldX, road.transform.position.y, clampedZ);
            Vector3 local = road.transform.InverseTransformPoint(worldPointOnZ);
            local.y = GetRoadCenterY(road);

            if (TryPlace(road, r.laneIndex, local, laneRot, r.kind))
                _requests.RemoveAt(i);
        }
    }

    private void TryRandomSpawnOnRoad(RoadPiece road)
    {
        if (obstaclePrefabs.Length == 0 && trainPrefabs.Length == 0) return;
        if (!TryGetTileZRangeWorld(road, out float zMinW, out float zMaxW)) return;

        // Do not place if the tile isn't far enough
        if ((zMinW - PlayerZ()) < minAheadDistance) return;

        float roll = Random.value;
        string kind = null;
        if (roll < chanceToSpawnObstacle && obstaclePrefabs.Length > 0) kind = "obstacle";
        else if (roll < chanceToSpawnObstacle + chanceToSpawnTrain && trainPrefabs.Length > 0) kind = "train";
        if (kind == null) return;

        int laneIdx = Random.Range(0, GetLaneCount(road));
        if (!TryGetLaneWorldX(road, laneIdx, out float laneWorldX, out Quaternion laneRot)) return;

        // Constrain Z to be within the tile, away from edges, and far enough ahead of the player
        float zFrom = Mathf.Max(zMinW + edgePaddingZ, PlayerZ() + minAheadDistance);
        float zTo   = zMaxW - edgePaddingZ;
        if (zTo <= zFrom) return;

        float worldZ = Random.Range(zFrom, zTo);

        Vector3 worldPointOnZ = new Vector3(laneWorldX, road.transform.position.y, worldZ);
        Vector3 local = road.transform.InverseTransformPoint(worldPointOnZ);
        local.y = GetRoadCenterY(road);

        TryPlace(road, laneIdx, local, laneRot, kind);
    }

    /// Centralized placement with both duplicate & physics overlap checks.
    private bool TryPlace(RoadPiece road, int laneIndex, Vector3 localPos, Quaternion laneRot, string kind)
    {
        // duplicate filter (logical slot per tile)
        int zBin = Mathf.RoundToInt(localPos.z / Mathf.Max(0.01f, zBinSize));
        var slot = new Slot(laneIndex, zBin);
        var set = _occupiedByRoad[road];
        if (set.Contains(slot)) return false;

        // compute final world pos (child of tile), then physics overlap check
        Vector3 world = road.transform.TransformPoint(localPos) + Vector3.up * yOffset;

        if (Physics.CheckSphere(world, overlapRadius, overlapMask, QueryTriggerInteraction.Collide))
            return false;

        // instantiate as child → keep local to avoid sliding with world transforms
        var prefab = PickPrefab(kind);
        if (!prefab) return false;

        var obj = Instantiate(prefab, road.transform, false);
        obj.transform.localPosition = localPos + Vector3.up * yOffset;
        obj.transform.localRotation = laneRot;
        obj.transform.localScale = Vector3.one;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }

        set.Add(slot); // mark cell taken
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

    private bool TryGetTileZRangeWorld(RoadPiece road, out float zMinW, out float zMaxW)
    {
        zMinW = zMaxW = 0f;

        // Prefer BoxCollider to include full mesh length
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

        // Fallback to renderer bounds
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

        // fallback: constant local X per lane
        int li = Mathf.Clamp(laneIndex, 0, lanesLocalX.Length - 1);
        Vector3 local = new Vector3(lanesLocalX[li], GetRoadCenterY(road), 0f);
        Vector3 world = road.transform.TransformPoint(local);
        worldX = world.x;
        rot = road.transform.rotation;
        return true;
    }

    private float GetRoadCenterY(RoadPiece road)
    {
        var bc = road.GetComponent<BoxCollider>();
        return bc ? bc.center.y : 0f;
    }

    private int GetLaneCount(RoadPiece road)
    {
        var sockets = road.transform.Find(socketsRootName);
        return (sockets && sockets.childCount > 0) ? sockets.childCount : lanesLocalX.Length;
    }
    
    private void ResetGateAndState()
    {
        _startTime = Time.time;
        spawningEnabled = false;        // נועל ספאונים בתחילת כל סצנה
        _requests.Clear();              // לא לשאת בקשות מהראן הקודם
        _occupiedByRoad.Clear();        // איפוס תפוסות של אריחים
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

// --- NEW: חיבור לאירוע טעינת סצנה ---
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetGateAndState(); // כל פעם שטוענים סצנה (כולל Restart) נאפס שער ומצב
    }

    public void OnGameRestart()
    {
        ResetGateAndState();
    }
    
    public void RegisterPlayer(Transform t)
    {
        player = t;
    }

}
