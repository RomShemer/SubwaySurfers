using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoadPowerupSpawner : MonoBehaviour
{
    [System.Serializable]
    public class PowerupDef
    {
        public string id = "magnet";
        public GameObject prefab;
        [Range(0f, 1f)] public float weight = 1f; // משקל יחסי בהגרלה
        public float clearRadius = 0.6f;          // רדיוס בדיקת-חפיפה עבור הסוג הזה
    }

    [Header("What to spawn (weighted)")]
    public List<PowerupDef> powerups = new List<PowerupDef>();

    [Header("Spawn policy per tile")]
    [Tooltip("סיכוי שבכלל יהיו פאווראפ באריח הזה")]
    [Range(0f,1f)] public float chanceThisTile = 0.45f;
    [Tooltip("מס’ מקסימלי של פאווראפים באריח (לרוב 1)")]
    [Range(0,3)] public int maxPerTile = 1;

    [Header("Lanes / sockets")]
    public string socketsRootName = "Sockets";     // אם יש סוקטים
    public float[] lanesLocalX = new float[] { -1.2f, 0f, 1.2f }; // fallback אם אין
    public float yLocalOffset = 0.5f;

    [Header("Z placement on tile (LOCAL)")]
    public float edgePaddingZ = 1.0f;
    public float slotStepZ = 2.8f;                 // גריד נוח לבדיקת זמינות

    [Header("Avoid overlap with existing stuff")]
    public LayerMask blockMask;                    // שכבות של רכבות/מכשולים/מטבעות
    public float extraClearRadius = 0.1f;          // שוליים קטנים מעבר ל-clearRadius הספציפי

    [Header("Gating / Start")]
    public int skipTilesFromStart = 4;             // לא להניח באריחים הראשונים
    public Transform player;
    public float minAheadDistance = 18f;           // אל תניחו אריח אם תחילת האריח קרובה מדי לשחקן

    private BoxCollider _col;

    void Awake()
    {
        _col = GetComponent<BoxCollider>();
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    // קוראים לזה מתוך InfiniteRoad כשמפעילים אריח, עם אינדקס האריח הכולל
    public void SpawnOnThisTile(int spawnedTileIndex)
    {
        if (spawnedTileIndex <= skipTilesFromStart) return;
        if (powerups.Count == 0 || PowerupPool.I == null) return;
        if (Random.value > chanceThisTile) return;

        // בדיקת מרחק מינימלי מהשחקן: נוודא שתחילת האריח רחוקה מספיק
        if (!TryGetTileZRangeWorld(out float zMinW, out float zMaxW)) return;
        if (player && (zMinW - player.position.z) < minAheadDistance) return;

        // בונים רשימת "סלוטים" אפשריים (ליין × Z), נערבב ונבחר עד maxPerTile
        var candidates = BuildCandidatesLocal();
        Shuffle(candidates);

        int placed = 0;
        foreach (var c in candidates)
        {
            // בוחרים סוג פאווראפ בהגרלה משוקללת
            var def = PickPowerupWeighted();
            if (def == null || !def.prefab) continue;

            Vector3 local = new Vector3(c.xLocal, _col.center.y + yLocalOffset, c.zLocal);
            Vector3 world = transform.TransformPoint(local);

            float radius = Mathf.Max(0.05f, def.clearRadius + extraClearRadius);
            if (Physics.CheckSphere(world, radius, blockMask, QueryTriggerInteraction.Collide))
                continue; // עמוס/מתנגש → נוותר וננסה סלוט אחר

            // ספאון מהפול
            var inst = PowerupPool.I.Spawn(def.prefab, transform, world, transform.rotation);

            // אופציונלי: "יישור" לאוריינטציית הליין (אם יש "Sockets")
            if (TryGetLaneRot(c.laneIndex, out var laneRot)) inst.transform.rotation = laneRot;

            placed++;
            if (placed >= Mathf.Max(1, maxPerTile)) break;
        }
    }

    // ---------- עזרים ----------

    private struct Slot { public int laneIndex; public float xLocal, zLocal; public Slot(int li,float xl,float zl){ laneIndex=li; xLocal=xl; zLocal=zl; } }

    private List<Slot> BuildCandidatesLocal()
    {
        var list = new List<Slot>();

        // X של הליינים: מסוקטים אם יש, אחרת מערך fallback
        var xs = GetLaneLocalXs(out int laneCount);

        float lenZ = _col.size.z;
        float zMin = _col.center.z - (lenZ * 0.5f) + edgePaddingZ;
        float zMax = _col.center.z + (lenZ * 0.5f) - edgePaddingZ;

        for (int li = 0; li < laneCount; li++)
        {
            float x = xs[li];
            for (float z = zMin; z <= zMax; z += Mathf.Max(0.1f, slotStepZ))
                list.Add(new Slot(li, x, z));
        }
        return list;
    }

    private float[] GetLaneLocalXs(out int count)
    {
        var sockets = transform.Find(socketsRootName);
        if (sockets && sockets.childCount > 0)
        {
            count = sockets.childCount;
            var arr = new float[count];
            for (int i = 0; i < count; i++)
                arr[i] = transform.InverseTransformPoint(sockets.GetChild(i).position).x;
            return arr;
        }
        count = lanesLocalX.Length;
        return lanesLocalX;
    }

    private bool TryGetTileZRangeWorld(out float zMinW, out float zMaxW)
    {
        zMinW = zMaxW = 0f;
        if (!_col) return false;
        float lenZ = _col.size.z;
        Vector3 a = transform.TransformPoint(new Vector3(0, 0, _col.center.z - lenZ * 0.5f));
        Vector3 b = transform.TransformPoint(new Vector3(0, 0, _col.center.z + lenZ * 0.5f));
        zMinW = Mathf.Min(a.z, b.z);
        zMaxW = Mathf.Max(a.z, b.z);
        return true;
    }

    private bool TryGetLaneRot(int laneIndex, out Quaternion rot)
    {
        rot = transform.rotation;
        var sockets = transform.Find(socketsRootName);
        if (sockets && sockets.childCount > 0)
        {
            int idx = Mathf.Clamp(laneIndex, 0, sockets.childCount - 1);
            rot = sockets.GetChild(idx).rotation;
            return true;
        }
        return false;
    }

    private PowerupDef PickPowerupWeighted()
    {
        float sum = 0f;
        foreach (var p in powerups) sum += Mathf.Max(0f, p.weight);
        if (sum <= 0f) return null;

        float r = Random.value * sum, cum = 0f;
        foreach (var p in powerups)
        {
            cum += Mathf.Max(0f, p.weight);
            if (r <= cum) return p;
        }
        return powerups[powerups.Count - 1];
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
