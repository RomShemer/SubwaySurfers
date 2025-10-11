using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class PowerupPool : MonoBehaviour
{
    public static PowerupPool I { get; private set; }

    [System.Serializable]
    public class Entry
    {
        public GameObject prefab;
        public int defaultCapacity = 8;
        public int maxSize = 64;
    }

    [Header("Prefabs to pool")]
    public Entry[] entries;

    private readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools = new();

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        if (entries == null) return;
        foreach (var e in entries)
        {
            if (!e.prefab || _pools.ContainsKey(e.prefab)) continue;

            var pool = new ObjectPool<GameObject>(
                createFunc: () => {
                    var go = Instantiate(e.prefab);
                    go.SetActive(false);
                    return go;
                },
                actionOnGet: go => go.SetActive(true),
                actionOnRelease: go => go.SetActive(false),
                actionOnDestroy: go => Destroy(go),
                defaultCapacity: Mathf.Max(1, e.defaultCapacity),
                maxSize: Mathf.Max(1, e.maxSize)
            );

            // פריהיט
            var temp = new List<GameObject>(e.defaultCapacity);
            for (int i = 0; i < e.defaultCapacity; i++) temp.Add(pool.Get());
            foreach (var go in temp) pool.Release(go);

            _pools[e.prefab] = pool;
        }
    }

    public GameObject Spawn(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot)
    {
        if (!_pools.TryGetValue(prefab, out var pool))
        {
            // fallback: יצירה חד-פעמית
            var go = Instantiate(prefab, pos, rot, parent);
            return go;
        }

        var inst = pool.Get();
        if (parent) inst.transform.SetParent(parent, false);
        inst.transform.SetPositionAndRotation(pos, rot);
        //inst.transform.localScale = Vector3.one;
        if (inst.tag == "boots")
        {
            inst.transform.localScale = prefab.transform.localScale*3;
            float newYPos = inst.transform.localPosition.y - 0.3f;
            inst.transform.localPosition = new Vector3(inst.transform.localPosition.x, newYPos, inst.transform.localPosition.z);
        }
        else
        {
            inst.transform.localScale = prefab.transform.localScale*2;
        }
        return inst;
    }

    public void Release(GameObject inst, GameObject prefabKey)
    {
        if (!inst) return;
        if (_pools.TryGetValue(prefabKey, out var pool))
            pool.Release(inst);
        else
            Destroy(inst);
    }
}
