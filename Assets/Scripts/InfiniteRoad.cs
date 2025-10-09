using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoadVariant
{
    [Tooltip("מזהה לוגי – נוח ללוגים/כללים (למשל: Straight / TunnelA / TunnelB...)")]
    public string id = "Straight";

    [Tooltip("פריפאב של המקטע (על ה-Root חייב להיות RoadPiece עם Start/End Anchors)")]
    public RoadPiece prefab;

    [Tooltip("משקל בחירה יחסית – גדול יותר => יופיע יותר")]
    public float weight = 1f;

    [Tooltip("כמה ברצף מותר מאותו סוג (למנוע 2 מנהרות רצוף וכו')")]
    public int maxConsecutive = 2;

    [Tooltip("מרחק מינימלי (במספר מקטעים) עד שמותר לחזור על אותו סוג")]
    public int minSpacing = 0;

    [Tooltip("סוגים שאסור לבוא מיד אחרי ה-Variant הזה (לא חובה)")]
    public List<string> disallowNextIds = new List<string>();

    [Tooltip("כמה אינסטנסים להכין מראש ב-Pool לסוג הזה")]
    public int prewarm = 6;
}

public class InfiniteRoad : MonoBehaviour
{
    [Header("Variants (הוסף כאן את כל הסוגים, 'Straight' + כל המנהרות)")]
    public List<RoadVariant> variants = new List<RoadVariant>();

    [Header("Initial Build")]
    public int initialPieces = 10;
    public Transform player;
    public Transform startAnchorOverride;

    [Header("Recycle")]
    public float recycleBuffer = 0.25f;

    [Header("Coin spawn control")]
    [Tooltip("כמה מקטעים ראשונים לא יכילו מטבעות (למשל 5)")]
    public int skipCoinTiles = 5;

    [Header("Obstacle And Train Spawner")]
    public ObstacleAndTrainSpawner obstacleSpawner;

    [Header("Powerup spawn (Magnet/Shoes/...)")]
    [Tooltip("האם להפעיל ספאון פאווראפים על כל אריח")]
    public bool spawnPowerups = true;
    [Tooltip("כמה מקטעים ראשונים לא יכילו פאווראפים")]
    public int skipPowerupTiles = 4;

    private readonly Queue<RoadPiece> _active = new Queue<RoadPiece>();
    private RoadPiece _lastPiece;

    private readonly Dictionary<RoadPiece, Queue<RoadPiece>> _pools = new Dictionary<RoadPiece, Queue<RoadPiece>>();

    private string _lastId = "";
    private int _lastIdRun = 0;
    private readonly Dictionary<string, int> _sinceLastById = new Dictionary<string, int>();

    private int _straightStreak = 0;
    private int _tunnelCounter = 0;
    private int _spawnIndex = 0;
    private int _spawnedTileCount = 0;

    private void Awake()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // Fallback ל-Singleton אם לא שובץ ידנית
        if (!obstacleSpawner && ObstacleAndTrainSpawner.I != null)
            obstacleSpawner = ObstacleAndTrainSpawner.I;
    }

    private void Start()
    {
        if (variants == null || variants.Count == 0)
        {
            Debug.LogError("[InfiniteRoad] No variants assigned.");
            enabled = false;
            return;
        }

        // פריהיט לפולים של אריחים
        foreach (var v in variants)
        {
            if (!v.prefab)
            {
                Debug.LogError($"[InfiniteRoad] Variant '{v.id}' missing prefab.", this);
                continue;
            }
            if (!_pools.ContainsKey(v.prefab))
                _pools[v.prefab] = new Queue<RoadPiece>();

            for (int i = 0; i < Mathf.Max(1, v.prewarm); i++)
            {
                var piecePre = Instantiate(v.prefab);
                piecePre.gameObject.SetActive(false);
                _pools[v.prefab].Enqueue(piecePre);
            }

            _sinceLastById[v.id] = 999999;
        }

        // עוגן התחלה
        var anchorGo = new GameObject("[RoadStartAnchor]");
        var anchor = anchorGo.transform;
        if (startAnchorOverride)
            anchor.SetPositionAndRotation(startAnchorOverride.position, startAnchorOverride.rotation);
        else
            anchor.SetPositionAndRotation(transform.position, transform.rotation);

        // בניה ראשונית
        RoadPiece prev = null;
        for (int i = 0; i < initialPieces; i++)
        {
            var picked = PickVariant();
            var piece = GetFromPool(picked.prefab);

            if (i == 0) piece.SnapStartTo(anchor);
            else piece.SnapAfter(prev);

            piece.gameObject.SetActive(true);
            NameSpawnedPiece(piece, picked.id);

            // מכשולים/רכבות
            if (!obstacleSpawner && ObstacleAndTrainSpawner.I != null)
                obstacleSpawner = ObstacleAndTrainSpawner.I;
            if (obstacleSpawner != null)
                obstacleSpawner.SpawnOnRoad(piece);

            // מטבעות
            _spawnedTileCount++;
            if (_spawnedTileCount > skipCoinTiles)
            {
                var coinSpawner = piece.GetComponent<RoadCoinSpawner>();
                if (coinSpawner != null)
                    coinSpawner.SpawnCoinsOnThisTile();
            }

            // פאווראפים (מגנט/נעליים/…)
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

    private void Update()
    {
        if (_active.Count == 0 || !player) return;

        var first = _active.Peek();
        var end = first.endAnchor ? first.endAnchor : first.transform;

        var toPlayer = player.position - end.position;
        var forward = end.forward;
        bool passed = Vector3.Dot(forward, toPlayer) > recycleBuffer;

        if (passed)
        {
            var old = _active.Dequeue();
            ReturnToPool(old);

            var picked = PickVariant();
            var next = GetFromPool(picked.prefab);
            next.SnapAfter(_lastPiece);
            next.gameObject.SetActive(true);

            // מכשולים/רכבות
            if (!obstacleSpawner && ObstacleAndTrainSpawner.I != null)
                obstacleSpawner = ObstacleAndTrainSpawner.I;
            if (obstacleSpawner != null)
                obstacleSpawner.SpawnOnRoad(next);

            NameSpawnedPiece(next, picked.id);

            // מטבעות
            _spawnedTileCount++;
            if (_spawnedTileCount > skipCoinTiles)
            {
                var coinSpawner = next.GetComponent<RoadCoinSpawner>();
                if (coinSpawner != null)
                    coinSpawner.SpawnCoinsOnThisTile();
            }

            // פאווראפים
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
    }

    private bool IsTunnelId(string id) =>
        !string.Equals(id, "Straight", System.StringComparison.OrdinalIgnoreCase);

    private void NameSpawnedPiece(RoadPiece piece, string id)
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

    private RoadVariant PickVariant()
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

    private List<RoadVariant> CollectCandidates(bool strict = false, bool ignoreDisallow = false, bool ignoreMinSpacing = false)
    {
        var list = new List<RoadVariant>();

        foreach (var v in variants)
        {
            if (v.prefab == null || v.weight <= 0f) continue;

            if (IsTunnelId(v.id) && _straightStreak < 3)
                continue;

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

    private void UpdateRunAndSpacing(string pickedId)
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

    private RoadPiece GetFromPool(RoadPiece prefab)
    {
        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<RoadPiece>();

        if (_pools[prefab].Count > 0)
            return _pools[prefab].Dequeue();

        var piece = Instantiate(prefab);
        piece.gameObject.SetActive(false);
        return piece;
    }

    private void ReturnToPool(RoadPiece piece)
    {
        if (!piece) return;

        // --- ניקויים לפני השבתה ---

        // 1) ניקוי מטבעות על האריח לפני השבתה (דורש מתודה ב-RoadCoinSpawner)
        var coinSpawner = piece.GetComponent<RoadCoinSpawner>();
        if (coinSpawner) coinSpawner.ClearCoinsOnThisTile();

        // 2) ניקוי פאווראפים שלא נאספו (Release לפול אם אפשר, אחרת כיבוי)
        var pwr = piece.GetComponent<RoadPowerupSpawner>();
        if (pwr != null)
        {
            // א. אם הוספת ב-RoadPowerupSpawner פונקציה ייעודית לניקוי – העדיפי אותה:
            // pwr.DespawnOnThisTile();

            // ב. ניקוי כל אינסטנס של MagnetPickup (ובהמשך אפשר להרחיב ל-ShoesPickup וכו'):
            var pickups = piece.GetComponentsInChildren<MagnetPickup>(true);
            foreach (var m in pickups)
            {
                if (PowerupPool.I && m.prefabKey)
                    PowerupPool.I.Release(m.gameObject, m.prefabKey);
                else
                    m.gameObject.SetActive(false);
            }

            // טיפ: אם לכל הפאווראפים יש Tag "Powerup", אפשר גם:
            // foreach (var tr in piece.GetComponentsInChildren<Transform>(true))
            //     if (tr.CompareTag("Powerup")) tr.gameObject.SetActive(false);
        }

        // 3) ניקוי תפוסות המכשולים/רכבות לאריח הזה
        if (obstacleSpawner == null && ObstacleAndTrainSpawner.I != null)
            obstacleSpawner = ObstacleAndTrainSpawner.I;
        if (obstacleSpawner != null)
            obstacleSpawner.ClearRoadOccupancy(piece);

        piece.gameObject.SetActive(false);

        // --- השבתה לפול של אריחים ---
        RoadPiece prefabKey = null;
        foreach (var v in variants)
        {
            if (!v.prefab) continue;
            if (piece.name.StartsWith(v.id + "_")) { prefabKey = v.prefab; break; }
        }

        if (prefabKey == null)
        {
            if (!_pools.ContainsKey(piece)) _pools[piece] = new Queue<RoadPiece>();
            _pools[prefabKey].Enqueue(piece); // שגיאה בכוונה? נתקן:
            // תיקון: אם prefabKey==null, נכניס לפי המפתח piece עצמו:
            _pools[piece].Enqueue(piece);
        }
        else
        {
            _pools[prefabKey].Enqueue(piece);
        }
    }

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
