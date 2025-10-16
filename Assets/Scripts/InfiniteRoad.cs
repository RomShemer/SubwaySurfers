using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoadVariant
{
    [Tooltip("מזהה לוגי – נוח לכללים/דיבאג")]
    public string id = "Straight";

    [Tooltip("הפריפאב של המקטע. על ה-Root חייב להיות RoadPiece עם Start/End Anchors")]
    public RoadPiece prefab;

    [Tooltip("משקל לבחירה יחסית")]
    public float weight = 1f;

    [Tooltip("כמה ברצף מותר מאותו סוג")]
    public int maxConsecutive = 2;

    [Tooltip("כמה מקטעים צריך להמתין עד שמותר לחזור על הסוג")]
    public int minSpacing = 0;

    [Tooltip("סוגים שאסור לבוא מיד אחרי ה-Variant הזה")]
    public List<string> disallowNextIds = new List<string>();

    [Tooltip("כמה אינסטנסים להכין מראש ב-Pool")]
    public int prewarm = 6;
}

public class InfiniteRoad : MonoBehaviour
{
    [Header("Variants")]
    public List<RoadVariant> variants = new List<RoadVariant>();

    [Header("Initial Build")]
    public int initialPieces = 10;
    public Transform player;
    public Transform startAnchorOverride;

    [Header("Recycle")]
    [Tooltip("כמה קדימה יחסית לנק' הסיום נחשיב שהשחקן עבר (Dot > buffer)")]
    public float recycleBuffer = 0.25f;

    [Header("Coin spawn control")]
    [Tooltip("כמה מקטעים ראשונים לא יכילו מטבעות")]
    public int skipCoinTiles = 5;

    [Header("Obstacle And Train Spawner")]
    public ObstacleAndTrainSpawner obstacleSpawner;

    [Header("Powerup spawn (Magnet/Shoes/...)")]
    [Tooltip("האם להפעיל ספאון פאווראפים על כל אריח")]
    public bool spawnPowerups = true;
    [Tooltip("כמה מקטעים ראשונים ללא פאווראפים")]
    public int skipPowerupTiles = 4;

    [Header("Hierarchy (אופציונלי)")]
    [Tooltip("איפה לשים אינסטנסים אקטיביים בהייררכיה (לסדר)")]
    [SerializeField] Transform activePiecesRoot;
    [Tooltip("איפה לשים פריטים של הפול (לסדר)")]
    [SerializeField] Transform poolRoot;

    // ---- מצב ריצה ----
    private readonly Queue<RoadPiece> _active = new Queue<RoadPiece>();
    private RoadPiece _lastPiece;

    // פול לפי פריפאב
    private readonly Dictionary<RoadPiece, Queue<RoadPiece>> _pools = new Dictionary<RoadPiece, Queue<RoadPiece>>();

    // מיפוי אינסטנס -> פריפאב־מפתח (לחזרה בטוחה לפול)
    private readonly Dictionary<RoadPiece, RoadPiece> _instanceToKey = new Dictionary<RoadPiece, RoadPiece>();

    // כללי פריסה
    private string _lastId = "";
    private int _lastIdRun = 0;
    private readonly Dictionary<string, int> _sinceLastById = new Dictionary<string, int>();

    // קאונטרים לדיבאג/כללים
    private int _straightStreak = 0;
    private int _tunnelCounter = 0;
    private int _spawnIndex = 0;
    private int _spawnedTileCount = 0;

    // ---- Bootstrapping ----
    private bool _bootstrapped = false;

    void Awake()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        if (!obstacleSpawner && ObstacleAndTrainSpawner.I != null)
            obstacleSpawner = ObstacleAndTrainSpawner.I;

        // יצירת שורשים להיררכיה אם לא שובצו
        if (!activePiecesRoot)
        {
            var go = new GameObject("[RoadPieces]");
            activePiecesRoot = go.transform;
            activePiecesRoot.SetParent(transform, false);
        }
        if (!poolRoot)
        {
            var go = new GameObject("[RoadPool]");
            poolRoot = go.transform;
            poolRoot.SetParent(transform, false);
        }
    }

    void Start()
    {
        if (variants == null || variants.Count == 0)
        {
            Debug.LogError("[InfiniteRoad] No variants assigned.");
            enabled = false;
            return;
        }

        if (!_bootstrapped)
            BuildInitial();   
    }

    void Update()
    {
        if (_active.Count == 0 || !player) return;

        var first = _active.Peek();
        var end = first.endAnchor ? first.endAnchor : first.transform;

        var toPlayer = player.position - end.position;
        var forward  = end.forward;
        bool passed  = Vector3.Dot(forward, toPlayer) > recycleBuffer;

        if (!passed) return;

        var old = _active.Dequeue();
        ReturnToPool(old);

        var picked = PickVariant();
        var next   = GetFromPool(picked.prefab);
        next.SnapAfter(_lastPiece);
        if (activePiecesRoot) next.transform.SetParent(activePiecesRoot, true);
        next.gameObject.SetActive(true);

        if (!obstacleSpawner && ObstacleAndTrainSpawner.I != null)
            obstacleSpawner = ObstacleAndTrainSpawner.I;
        if (obstacleSpawner != null)
            obstacleSpawner.SpawnOnRoad(next);

        NameSpawnedPiece(next, picked.id);

        _spawnedTileCount++;
        if (_spawnedTileCount > skipCoinTiles)
        {
            var coinSpawner = next.GetComponent<RoadCoinSpawner>();
            if (coinSpawner != null)
                coinSpawner.SpawnCoinsOnThisTile();
        }

        if (spawnPowerups && _spawnedTileCount > skipPowerupTiles)
        {
            var pwr = next.GetComponent<RoadPowerupSpawner>();
            if (pwr != null)
                pwr.SpawnOnThisTile(_spawnedTileCount);
        }

        _active.Enqueue(next);
        _lastPiece = next;

        UpdateRunAndSpacing(picked.id);
    }
    
    public void RebuildInitialRoad()
    {
        BuildInitial();
        Debug.Log("[InfiniteRoad] Rebuilt initial road.");
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }
    
    void BuildInitial()
    {
        _bootstrapped = true;

        ResetRoad();

        PrewarmPoolsAndInitCounters();

        BuildInitialPieces();
    }

    void PrewarmPoolsAndInitCounters()
    {
        _pools.Clear();
        _instanceToKey.Clear();
        _sinceLastById.Clear();

        foreach (var v in variants)
        {
            if (!v.prefab)
            {
                Debug.LogError($"[InfiniteRoad] Variant '{v.id}' missing prefab.", this);
                continue;
            }

            if (!_pools.ContainsKey(v.prefab))
                _pools[v.prefab] = new Queue<RoadPiece>();

            int count = Mathf.Max(1, v.prewarm);
            for (int i = 0; i < count; i++)
            {
                var piecePre = Instantiate(v.prefab);
                piecePre.gameObject.SetActive(false);
                if (poolRoot) piecePre.transform.SetParent(poolRoot, false);
                _pools[v.prefab].Enqueue(piecePre);
                _instanceToKey[piecePre] = v.prefab;
            }

            _sinceLastById[v.id] = 999999;
        }

        InitCountersOnly();
    }

    void InitCountersOnly()
    {
        _lastPiece = null;
        _lastId = "";
        _lastIdRun = 0;
        _straightStreak = 0;

        _spawnIndex = 0;
        _tunnelCounter = 0;
        _spawnedTileCount = 0;

        var keys = new List<string>(_sinceLastById.Keys);
        foreach (var id in keys) _sinceLastById[id] = 999999;
    }

    void BuildInitialPieces()
    {
        var anchorGo = new GameObject("[RoadStartAnchor]");
        var anchor = anchorGo.transform;
        anchor.SetParent(transform, false);
        if (startAnchorOverride)
            anchor.SetPositionAndRotation(startAnchorOverride.position, startAnchorOverride.rotation);
        else
            anchor.SetPositionAndRotation(transform.position, transform.rotation);

        RoadPiece prev = null;
        for (int i = 0; i < initialPieces; i++)
        {
            var picked = PickVariant();
            var piece  = GetFromPool(picked.prefab);

            if (i == 0) piece.SnapStartTo(anchor);
            else        piece.SnapAfter(prev);

            if (activePiecesRoot) piece.transform.SetParent(activePiecesRoot, true);
            piece.gameObject.SetActive(true);
            NameSpawnedPiece(piece, picked.id);

            if (!obstacleSpawner && ObstacleAndTrainSpawner.I != null)
                obstacleSpawner = ObstacleAndTrainSpawner.I;
            if (obstacleSpawner != null)
                obstacleSpawner.SpawnOnRoad(piece);

            _spawnedTileCount++;
            if (_spawnedTileCount > skipCoinTiles)
            {
                var coinSpawner = piece.GetComponent<RoadCoinSpawner>();
                if (coinSpawner != null)
                    coinSpawner.SpawnCoinsOnThisTile();
            }

            if (spawnPowerups && _spawnedTileCount > skipPowerupTiles)
            {
                var pwr = piece.GetComponent<RoadPowerupSpawner>();
                if (pwr != null)
                    pwr.SpawnOnThisTile(_spawnedTileCount);
            }

            _active.Enqueue(piece);
            _lastPiece = piece;

            UpdateRunAndSpacing(picked.id);
            prev = piece;
        }

        Destroy(anchorGo);
    }
    
    bool IsTunnelId(string id) =>
        !string.Equals(id, "Straight", System.StringComparison.OrdinalIgnoreCase);

    void NameSpawnedPiece(RoadPiece piece, string id)
    {
        if (IsTunnelId(id))
        {
            _tunnelCounter++;
            piece.gameObject.name = $"{id}_{_spawnIndex++} - מנהרה {_tunnelCounter}";
        }
        else
        {
            piece.gameObject.name = $"{id}_{_spawnIndex++}";
        }
    }

    RoadVariant PickVariant()
    {
        var cands = CollectCandidates(strict: true);
        if (cands.Count == 0)
        {
            cands = CollectCandidates(ignoreDisallow: true);
            if (cands.Count == 0)
            {
                cands = CollectCandidates(ignoreDisallow: true, ignoreMinSpacing: true);
                if (cands.Count == 0)
                {
                    cands = new List<RoadVariant>();
                    foreach (var v in variants)
                    {
                        if (IsTunnelId(v.id) && _straightStreak < 3) continue;
                        if (v.prefab && v.weight > 0f) cands.Add(v);
                    }
                    if (cands.Count == 0)
                    {
                        var straight = variants.Find(x => string.Equals(x.id, "Straight", System.StringComparison.OrdinalIgnoreCase));
                        if (straight != null) cands.Add(straight);
                        else cands.AddRange(variants);
                    }
                }
            }
        }

        float total = 0f;
        foreach (var v in cands) total += Mathf.Max(0.0001f, v.weight);

        float r = Random.value * total, cum = 0f;
        foreach (var v in cands)
        {
            cum += Mathf.Max(0.0001f, v.weight);
            if (r <= cum) return v;
        }
        return cands[cands.Count - 1];
    }

    List<RoadVariant> CollectCandidates(bool strict = false, bool ignoreDisallow = false, bool ignoreMinSpacing = false)
    {
        var list = new List<RoadVariant>();

        foreach (var v in variants)
        {
            if (v.prefab == null || v.weight <= 0f) continue;

            if (IsTunnelId(v.id) && _straightStreak < 3) continue;

            if (!string.IsNullOrEmpty(_lastId) && v.id == _lastId && _lastIdRun >= Mathf.Max(1, v.maxConsecutive))
                continue;

            if (!ignoreMinSpacing)
            {
                if (_sinceLastById.TryGetValue(v.id, out var since) && since < Mathf.Max(0, v.minSpacing))
                    continue;
            }

            if (!ignoreDisallow && !string.IsNullOrEmpty(_lastId))
            {
                var last = variants.Find(x => x.id == _lastId);
                if (last != null && last.disallowNextIds != null && last.disallowNextIds.Contains(v.id))
                    continue;
            }

            list.Add(v);
        }

        return list;
    }

    void UpdateRunAndSpacing(string pickedId)
    {
        if (_lastId == pickedId) _lastIdRun++;
        else { _lastId = pickedId; _lastIdRun = 1; }

        var keys = new List<string>(_sinceLastById.Keys);
        foreach (var k in keys) _sinceLastById[k] = _sinceLastById[k] + 1;
        _sinceLastById[pickedId] = 0;

        if (string.Equals(pickedId, "Straight", System.StringComparison.OrdinalIgnoreCase))
            _straightStreak++;
        else
            _straightStreak = 0;
    }

    // =========================================================
    //                           POOL
    // =========================================================
    RoadPiece GetFromPool(RoadPiece prefab)
    {
        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<RoadPiece>();

        RoadPiece inst;
        if (_pools[prefab].Count > 0)
        {
            inst = _pools[prefab].Dequeue();
        }
        else
        {
            inst = Instantiate(prefab);
            if (poolRoot) inst.transform.SetParent(poolRoot, false);
            _instanceToKey[inst] = prefab; 
        }

        if (!_instanceToKey.ContainsKey(inst))
            _instanceToKey[inst] = prefab;

        return inst;
    }

    void ReturnToPool(RoadPiece piece)
    {
        if (!piece) return;

        var coinSpawner = piece.GetComponent<RoadCoinSpawner>();
        if (coinSpawner) coinSpawner.ClearCoinsOnThisTile();

        var pwr = piece.GetComponent<RoadPowerupSpawner>();
        if (pwr != null)
        {
            var pickups = piece.GetComponentsInChildren<MagnetPickup>(true);
            foreach (var m in pickups)
            {
                if (PowerupPool.I && m.prefabKey)
                    PowerupPool.I.Release(m.gameObject, m.prefabKey);
                else
                    m.gameObject.SetActive(false);
            }
        }

        if (obstacleSpawner == null && ObstacleAndTrainSpawner.I != null)
            obstacleSpawner = ObstacleAndTrainSpawner.I;
        if (obstacleSpawner != null)
            obstacleSpawner.ClearRoadOccupancy(piece);

        piece.gameObject.SetActive(false);
        if (poolRoot) piece.transform.SetParent(poolRoot, true);

        if (_instanceToKey.TryGetValue(piece, out var keyPrefab))
        {
            if (!_pools.ContainsKey(keyPrefab))
                _pools[keyPrefab] = new Queue<RoadPiece>();
            _pools[keyPrefab].Enqueue(piece);
        }
        else
        {
            if (!_pools.ContainsKey(piece)) _pools[piece] = new Queue<RoadPiece>();
            _pools[piece].Enqueue(piece);
        }
    }

    // =========================================================
    //                      RESET
    // =========================================================
    public void ResetRoad()
    {
        while (_active.Count > 0) ReturnToPool(_active.Dequeue());

        _lastPiece = null;
        _lastId = "";
        _lastIdRun = 0;
        _straightStreak = 0;

        _spawnIndex = 0;
        _tunnelCounter = 0;
        _spawnedTileCount = 0;

        var keys = new List<string>(_sinceLastById.Keys);
        foreach (var id in keys) _sinceLastById[id] = 999999;
    }
}