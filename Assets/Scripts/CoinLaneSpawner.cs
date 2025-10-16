using System.Collections.Generic;
using UnityEngine;

public class CoinLaneSpawner : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;       
    public GameObject coinPrefab;   
    [Header("Lanes (X) & Forward (Z)")]
    public float[] lanesX = new float[] { -1.2f, 0f, 1.2f }; 
    public float startAheadZ = 15f;     
    public float distanceAheadZ = 60f; 
    public float spacingZ = 6f;        
    public float jitterZ = 0f;         

    [Header("Y CurveWorld")]
    public float baseY = 0f;           

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

            for (int i = 0; i < lanesX.Length; i++)
            {
                float x = lanesX[i];
                Vector3 pos = new Vector3(x, baseY, z);

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
