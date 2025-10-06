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
