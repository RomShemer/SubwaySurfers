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
    public Transform player;                   // אם ריק – נמצא לפי Tag=Player
    public Transform startAnchorOverride;      // לא חובה

    [Header("Recycle")]
    public float recycleBuffer = 0.25f;

    // מצב
    private readonly Queue<RoadPiece> _active = new Queue<RoadPiece>();
    private RoadPiece _lastPiece;

    // Pools לפי פריפאב
    private readonly Dictionary<RoadPiece, Queue<RoadPiece>> _pools = new Dictionary<RoadPiece, Queue<RoadPiece>>();

    // מעקב רצף + מרווחים
    private string _lastId = "";
    private int _lastIdRun = 0;
    private readonly Dictionary<string, int> _sinceLastById = new Dictionary<string, int>(); // כמה מקטעים עברו מאז הופעת id

    // --- כללים שביקשת ---
    // חובה לפחות 3 ישרים בין כל "מנהרות"
    private int _straightStreak = 0;   // כמה "Straight" רצופים לאחרונה
    private int _tunnelCounter = 0;    // מספר מנהרות שהונחו (לשם התצוגה)
    private int _spawnIndex = 0;       // אינדקס כללי לייצוב שמות/Pool

    private void Awake()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    private void Start()
    {
        if (variants == null || variants.Count == 0)
        {
            Debug.LogError("[InfiniteRoad] No variants assigned.");
            enabled = false;
            return;
        }

        // הכנת Pools + איפוס מונים
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
                var piece = Instantiate(v.prefab);
                piece.gameObject.SetActive(false);
                _pools[v.prefab].Enqueue(piece);
            }

            _sinceLastById[v.id] = 999999; // גדול מאוד כדי לאפשר בחירה בהתחלה
        }

        // עוגן התחלה
        var anchorGo = new GameObject("[RoadStartAnchor]");
        var anchor = anchorGo.transform;
        if (startAnchorOverride)
            anchor.SetPositionAndRotation(startAnchorOverride.position, startAnchorOverride.rotation);
        else
            anchor.SetPositionAndRotation(transform.position, transform.rotation);

        // בניית רצף התחלתי
        RoadPiece prev = null;
        for (int i = 0; i < initialPieces; i++)
        {
            var picked = PickVariant();
            var piece = GetFromPool(picked.prefab);

            if (i == 0) piece.SnapStartTo(anchor);
            else piece.SnapAfter(prev);

            piece.gameObject.SetActive(true);
            NameSpawnedPiece(piece, picked.id);  // שם כולל מספור מנהרות

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
            NameSpawnedPiece(next, picked.id);   // שם כולל מספור מנהרות

            _active.Enqueue(next);
            _lastPiece = next;

            UpdateRunAndSpacing(picked.id);
        }
    }

    // ----------------- עזר: זיהוי "מנהרה" -----------------
    private bool IsTunnelId(string id)
    {
        // כל מה שאינו "Straight" נחשב מנהרה
        return !string.Equals(id, "Straight", System.StringComparison.OrdinalIgnoreCase);
    }

    // ----------------- קביעת שם לאובייקט שנוצר -----------------
    private void NameSpawnedPiece(RoadPiece piece, string id)
    {
        // חשוב: שומרים prefix עם id_ כדי ש-ReturnToPool יזהה את ה-variant
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

    // ----------------- בחירה משוקללת עם החוקים -----------------

    private RoadVariant PickVariant()
    {
        // 1) כבד את כל החוקים (maxConsecutive, minSpacing, disallowNextIds, וגם 3 ישרים לפני מנהרה)
        var cands = CollectCandidates(strict:true);
        if (cands.Count == 0)
        {
            // 2) ותר על disallowNextIds בלבד
            cands = CollectCandidates(ignoreDisallow:true);
            if (cands.Count == 0)
            {
                // 3) ותר גם על minSpacing
                cands = CollectCandidates(ignoreDisallow:true, ignoreMinSpacing:true);
                if (cands.Count == 0)
                {
                    // 4) fallback אחרון – רק משקלות (עדיין נשמור כלל 3 ישרים לפני מנהרה כדי לא לשבור את הדרישה שלך)
                    cands = new List<RoadVariant>();
                    foreach (var v in variants)
                    {
                        if (IsTunnelId(v.id) && _straightStreak < 3) continue; // עדיין לא מאפשרים מנהרה
                        if (v.prefab && v.weight > 0f) cands.Add(v);
                    }
                    if (cands.Count == 0)
                    {
                        // אם איכשהו אין מועמדים בכלל, נכפה ישר
                        var straight = variants.Find(x => string.Equals(x.id, "Straight", System.StringComparison.OrdinalIgnoreCase));
                        if (straight != null) cands.Add(straight);
                        else cands.AddRange(variants); // מקרה קצה
                    }
                }
            }
        }

        // בחירה משוקללת
        float total = 0f;
        foreach (var v in cands) total += Mathf.Max(0.0001f, v.weight);

        float r = Random.value * total;
        float cum = 0f;
        foreach (var v in cands)
        {
            cum += Mathf.Max(0.0001f, v.weight);
            if (r <= cum) return v;
        }
        return cands[cands.Count - 1];
    }

    private List<RoadVariant> CollectCandidates(bool strict=false, bool ignoreDisallow=false, bool ignoreMinSpacing=false)
    {
        var list = new List<RoadVariant>();

        foreach (var v in variants)
        {
            if (v.prefab == null || v.weight <= 0f) continue;

            // כלל 3 ישרים לפני כל מנהרה
            if (IsTunnelId(v.id) && _straightStreak < 3)
                continue;

            // מגבלת רצף
            if (!string.IsNullOrEmpty(_lastId) && v.id == _lastId && _lastIdRun >= Mathf.Max(1, v.maxConsecutive))
                continue;

            // מרווח מינימלי לכל variant (אם לא מתעלמים)
            if (!ignoreMinSpacing)
            {
                if (_sinceLastById.TryGetValue(v.id, out var since) && since < Mathf.Max(0, v.minSpacing))
                    continue;
            }

            // disallowNextIds מהחלק הקודם (אם לא מתעלמים)
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
        // עדכן רצף לפי id שנבחר
        if (_lastId == pickedId) _lastIdRun++;
        else { _lastId = pickedId; _lastIdRun = 1; }

        // עדכן מרווחים לכל הסוגים
        var keys = new List<string>(_sinceLastById.Keys);
        foreach (var k in keys) _sinceLastById[k] = _sinceLastById[k] + 1;
        _sinceLastById[pickedId] = 0;

        // עדכן מונה ישרים רצופים
        if (string.Equals(pickedId, "Straight", System.StringComparison.OrdinalIgnoreCase))
            _straightStreak++;
        else
            _straightStreak = 0;
    }

    // ----------------- Pool -----------------

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
        piece.gameObject.SetActive(false);

        // מצא את ה-variant שאליו שייך האינסטנס לפי prefix בשם (id_)
        RoadPiece prefabKey = null;
        foreach (var v in variants)
        {
            if (!v.prefab) continue;
            if (piece.name.StartsWith(v.id + "_")) { prefabKey = v.prefab; break; }
        }

        if (prefabKey == null)
        {
            // fallback – הכנס לתור של עצמו
            if (!_pools.ContainsKey(piece)) _pools[piece] = new Queue<RoadPiece>();
            _pools[piece].Enqueue(piece);
        }
        else
        {
            _pools[prefabKey].Enqueue(piece);
        }
    }

    // איפוס (אופציונלי)
    public void ResetRoad()
    {
        while (_active.Count > 0) ReturnToPool(_active.Dequeue());
        _lastPiece = null;
        _lastId = "";
        _lastIdRun = 0;
        _straightStreak = 0;
        foreach (var id in new List<string>(_sinceLastById.Keys)) _sinceLastById[id] = 999999;
        // לא מאפסים מספור מנהרות בכוונה; אם תרצה – אפס כאן:
        // _tunnelCounter = 0;
        // _spawnIndex = 0;
    }
}
