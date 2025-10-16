using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MagnetPickup : MonoBehaviour
{
    public float duration = 7f;

    [Header("Pool binding")]
    public GameObject prefabKey; 

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        MagnetCollector.I?.Activate(duration);

        if (PowerupPool.I && prefabKey)
            PowerupPool.I.Release(gameObject, prefabKey);
        else
            gameObject.SetActive(false);
    }
}