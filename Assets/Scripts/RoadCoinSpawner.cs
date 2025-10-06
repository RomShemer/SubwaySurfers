using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoadCoinSpawner : MonoBehaviour
{
    public GameObject coinPrefab;

    [Header("Lane positions (LOCAL X)")]
    public float[] lanesLocalX = new float[] { -1.2f, 0f, 1.2f };

    [Header("Placement inside this road tile (LOCAL)")]
    public float yLocalOffset = 0.5f;     // גובה המטבע מעל פני הכביש (לוקאל Y)
    public float spacingLocalZ = 3.5f;    // ריווח בין שורות לאורך הקטע
    public float marginStartZ = 0.8f;     // שוליים בתחילת הקטע
    public float marginEndZ = 0.8f;       // שוליים בסוף הקטע

    [Header("When to spawn")]
    public bool spawnOnStart = true;      // יצירה מיד כשנטען הקטע

    BoxCollider col;

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

        // אורך/מרכז הקופסה בלוקאל (ולא לשכוח את center של הקוליידר!)
        float lenZ = col.size.z;
        float zMin = col.center.z - lenZ * 0.5f + marginStartZ;
        float zMax = col.center.z + lenZ * 0.5f - marginEndZ;

        for (float z = zMin; z <= zMax; z += spacingLocalZ)
        {
            foreach (float x in lanesLocalX)
            {
                // נקודה לוקאלית על המסלול -> הופכים לוורלד
                Vector3 local = new Vector3(x, col.center.y + yLocalOffset, z);
                Vector3 world = transform.TransformPoint(local);

                Instantiate(coinPrefab, world, Quaternion.identity);
            }
        }
    }
}
