using System.Collections.Generic;
using UnityEngine;

public class CoinPool : MonoBehaviour
{
    public static CoinPool I { get; private set; }

    [Header("Prefab & Prewarm")]
    [SerializeField] private Coin coinPrefab;
    [SerializeField] private int prewarmCount = 100;
    [SerializeField] private bool persistAcrossScenes = true;

    private readonly Queue<Coin> _pool = new Queue<Coin>();
    private Transform _poolRoot;

    private void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

        _poolRoot = new GameObject("CoinPoolRoot").transform;
        if (persistAcrossScenes) DontDestroyOnLoad(_poolRoot.gameObject);

        Prewarm();
    }

    private void Prewarm()
    {
        if (!coinPrefab) { Debug.LogError("[CoinPool] coinPrefab missing"); return; }
        for (int i = 0; i < prewarmCount; i++)
        {
            var c = Instantiate(coinPrefab, _poolRoot);
            c.gameObject.SetActive(false);
            _pool.Enqueue(c);
        }
    }

    public Coin Spawn(Transform parent, Vector3 worldPos, Quaternion rot)
    {
        Coin c = _pool.Count > 0 ? _pool.Dequeue()
            : Instantiate(coinPrefab, _poolRoot);

        var t = c.transform;
        t.SetParent(parent, false);
        t.position = worldPos;
        t.rotation = rot;

        c.ResetForReuse();
        c.gameObject.SetActive(true);
        return c;
    }

    public void Return(Coin c)
    {
        if (!c) return;
        c.gameObject.SetActive(false);
        c.transform.SetParent(_poolRoot, false);
        _pool.Enqueue(c);
    }

    public void ReturnAllUnder(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var ch = parent.GetChild(i);
            if (ch.TryGetComponent<Coin>(out var coin))
                Return(coin);
        }
    }
}