using System.Collections.Generic;
using UnityEngine;

public class CoinLaneSpawner : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;        // השחקן
    public GameObject coinPrefab;   // Prefab עם Renderer (CurveWorld) + Collider isTrigger

    [Header("Lanes (X) & Forward (Z)")]
    public float[] lanesX = new float[] { -1.2f, 0f, 1.2f }; // X של המסילות
    public float startAheadZ = 15f;     // התחלה קדימה מהשחקן
    public float distanceAheadZ = 60f;  // עד כמה קדימה לייצר
    public float spacingZ = 6f;         // רווח בין שורות לאורך Z
    public float jitterZ = 0f;          // 0 לקווים נקיים

    [Header("Y (לא קריטי עם CurveWorld)")]
    public float baseY = 0f;            // אפשר להשאיר 0

    private float nextSpawnZ;
    private readonly HashSet<Vector3> placed = new HashSet<Vector3>();

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
            float z = nextSpawnZ + (jitterZ == 0 ? 0 : Random.Range(-jitterZ, jitterZ));

            // שורה: מטבע בכל מסילה באותו Z
            for (int i = 0; i < lanesX.Length; i++)
            {
                float x = lanesX[i];
                Vector3 pos = new Vector3(x, baseY, z);

                // מניעת כפילויות בקירוב
                Vector3 key = RoundV3(pos, 2);
                if (!placed.Contains(key))
                {
                    Instantiate(coinPrefab, pos, Quaternion.identity);
                    placed.Add(key);
                }
            }

            nextSpawnZ += spacingZ;
        }
    }

    Vector3 RoundV3(Vector3 v, int digits)
    {
        float m = Mathf.Pow(10f, digits);
        return new Vector3(
            Mathf.Round(v.x * m) / m,
            Mathf.Round(v.y * m) / m,
            Mathf.Round(v.z * m) / m
        );
    }

    void OnDrawGizmosSelected()
    {
        if (lanesX == null) return;
        Gizmos.color = Color.yellow;
        float baseZ = player ? player.position.z : 0f;
        for (int i = 0; i < lanesX.Length; i++)
        {
            Gizmos.DrawLine(
                new Vector3(lanesX[i], baseY, baseZ + 2f),
                new Vector3(lanesX[i], baseY, baseZ + 42f)
            );
        }
    }
}
