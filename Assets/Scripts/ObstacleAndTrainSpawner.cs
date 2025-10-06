/*using UnityEngine;

public class ObstacleAndTrainSpawner : MonoBehaviour
{
    [Header("Prefabs Lists")]
    public GameObject[] obstaclePrefabs;
    public GameObject[] trainPrefabs;

    [Header("Spawn Settings")]
    public float chanceToSpawnObstacle = 0.4f;
    public float chanceToSpawnTrain = 0.3f;

    public float yOffset = 0.5f;

    public void SpawnOnRoad(RoadPiece road)
    {
        // נוודא שיש רנדומליות
        float roll = Random.value;

        if (roll < chanceToSpawnObstacle && obstaclePrefabs.Length > 0)
        {
            SpawnRandom(obstaclePrefabs, road);
        }
        else if (roll < chanceToSpawnObstacle + chanceToSpawnTrain && trainPrefabs.Length > 0)
        {
            SpawnRandom(trainPrefabs, road);
        }
    }

    private void SpawnRandom(GameObject[] prefabs, RoadPiece road)
    {
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        Vector3 pos = road.transform.position + road.transform.forward * Random.Range(2f, road.Length - 3f);
        pos.y += yOffset;

        GameObject obj = Instantiate(prefab, pos, road.transform.rotation);
        obj.transform.SetParent(road.transform);
    }
}*/

using System.Collections.Generic;
using UnityEngine;

public class ObstacleAndTrainSpawner : MonoBehaviour
{
    public static ObstacleAndTrainSpawner I { get; private set; }

    [Header("Prefabs Lists")]
    public GameObject[] obstaclePrefabs;   // blocker_* prefabs
    public GameObject[] trainPrefabs;      // train_* prefabs

    [Header("Default random spawn (fallback)")]
    [Range(0f, 1f)] public float chanceToSpawnObstacle = 0.4f;
    [Range(0f, 1f)] public float chanceToSpawnTrain   = 0.3f;
    public float yOffset = 0.5f;

    [Header("Placement")]
    public string socketsRootName = "Sockets";        // optional: LaneL/LaneM/LaneR under this
    public float[] lanesLocalX = new float[] { -2f, 0f, 2f }; // used if no sockets found

    [Header("Overlap filter")]
    public LayerMask overlapMask;  // set to Train/Obstacle layers
    public float overlapRadius = 0.6f;

    // ----- Requests sent by coins: place obstacle/train ahead on a lane -----
    [System.Serializable]
    public class Request
    {
        public float worldZ;
        public int laneIndex;
        public string kind; // "obstacle" or "train"
    }
    private readonly List<Request> _requests = new List<Request>();

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    /// Called by coins: queue an obstacle/train ahead on the given lane at worldZ
    public void Enqueue(float worldZ, int laneIndex, string kind = "obstacle")
    {
        _requests.Add(new Request { worldZ = worldZ, laneIndex = laneIndex, kind = kind });
    }

    /// Call this when a new RoadPiece becomes active (both at initial build and during recycling)
    public void SpawnOnRoad(RoadPiece road)
    {
        if (!road) return;

        // 1) First consume queued requests that fall inside this tile's Z range
        ConsumeQueuedForRoad(road);

        // 2) Optional: also do a random single spawn on this tile (fallback flavor)
        TryRandomSpawnOnRoad(road);
    }

    // ---------- Internals ----------

    void ConsumeQueuedForRoad(RoadPiece road)
    {
        if (_requests.Count == 0) return;
        if (!TryGetTileZRangeWorld(road, out float zMinW, out float zMaxW)) return;

        // iterate reversed to remove while iterating
        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            var r = _requests[i];
            if (r.worldZ < zMinW || r.worldZ > zMaxW) continue;

            // pick X by lane
            if (!TryGetLaneWorldX(road, r.laneIndex, out float laneWorldX, out Quaternion laneRot)) continue;

            // convert worldZ to local z to get exact world point again through transform (safer with rotations)
            Vector3 worldPointOnZ = new Vector3(laneWorldX, road.transform.position.y, r.worldZ);
            Vector3 local = road.transform.InverseTransformPoint(worldPointOnZ);
            local.y = GetRoadCenterY(road);
            Vector3 world = road.transform.TransformPoint(local) + Vector3.up * yOffset;

            if (Physics.CheckSphere(world, overlapRadius, overlapMask, QueryTriggerInteraction.Collide))
                continue;

            var prefab = PickPrefab(r.kind);
            if (prefab)
            {
                Instantiate(prefab, world, laneRot, road.transform);
                _requests.RemoveAt(i);
            }
        }
    }

    void TryRandomSpawnOnRoad(RoadPiece road)
    {
        if (obstaclePrefabs.Length == 0 && trainPrefabs.Length == 0) return;

        float roll = Random.value;
        string kind = null;
        if (roll < chanceToSpawnObstacle && obstaclePrefabs.Length > 0) kind = "obstacle";
        else if (roll < chanceToSpawnObstacle + chanceToSpawnTrain && trainPrefabs.Length > 0) kind = "train";

        if (kind == null) return;

        // pick a lane/socket + a Z inside the tile
        if (!TryGetLaneWorldX(road, Random.Range(0, GetLaneCount(road)), out float laneWorldX, out Quaternion laneRot)) return;
        if (!TryGetTileZRangeWorld(road, out float zMinW, out float zMaxW)) return;

        float worldZ = Random.Range(zMinW + 2f, zMaxW - 2f);
        Vector3 worldPointOnZ = new Vector3(laneWorldX, road.transform.position.y, worldZ);
        Vector3 local = road.transform.InverseTransformPoint(worldPointOnZ);
        local.y = GetRoadCenterY(road);
        Vector3 world = road.transform.TransformPoint(local) + Vector3.up * yOffset;

        if (Physics.CheckSphere(world, overlapRadius, overlapMask, QueryTriggerInteraction.Collide))
            return;

        var prefab = PickPrefab(kind);
        if (prefab) Instantiate(prefab, world, laneRot, road.transform);
    }

    GameObject PickPrefab(string kind)
    {
        if (kind == "train" && trainPrefabs.Length > 0)
            return trainPrefabs[Random.Range(0, trainPrefabs.Length)];
        if (obstaclePrefabs.Length > 0)
            return obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
        return null;
    }

    bool TryGetTileZRangeWorld(RoadPiece road, out float zMinW, out float zMaxW)
    {
        zMinW = zMaxW = 0f;
        var bc = road.GetComponent<BoxCollider>();
        if (!bc) { var r = road.GetComponentInChildren<Renderer>(); if (!r) return false; var b = r.bounds; zMinW = b.min.z; zMaxW = b.max.z; return true; }

        float lenZ = bc.size.z;
        Vector3 a = road.transform.TransformPoint(new Vector3(0, 0, bc.center.z - lenZ * 0.5f));
        Vector3 b2 = road.transform.TransformPoint(new Vector3(0, 0, bc.center.z + lenZ * 0.5f));
        zMinW = Mathf.Min(a.z, b2.z);
        zMaxW = Mathf.Max(a.z, b2.z);
        return true;
        }

    bool TryGetLaneWorldX(RoadPiece road, int laneIndex, out float worldX, out Quaternion rot)
    {
        // prefer sockets if exist
        var sockets = road.transform.Find(socketsRootName);
        if (sockets && sockets.childCount > 0)
        {
            int idx = Mathf.Clamp(laneIndex, 0, sockets.childCount - 1);
            var t = sockets.GetChild(idx);
            worldX = t.position.x;
            rot = t.rotation;
            return true;
        }
        // fallback to local X array
        int li = Mathf.Clamp(laneIndex, 0, lanesLocalX.Length - 1);
        Vector3 local = new Vector3(lanesLocalX[li], GetRoadCenterY(road), 0f);
        Vector3 world = road.transform.TransformPoint(local);
        worldX = world.x;
        rot = road.transform.rotation;
        return true;
    }

    float GetRoadCenterY(RoadPiece road)
    {
        var bc = road.GetComponent<BoxCollider>();
        if (bc) return bc.center.y;
        return 0f;
    }

    int GetLaneCount(RoadPiece road)
    {
        var sockets = road.transform.Find(socketsRootName);
        return (sockets && sockets.childCount > 0) ? sockets.childCount : lanesLocalX.Length;
    }
}
