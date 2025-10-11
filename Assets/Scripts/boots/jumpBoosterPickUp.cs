using UnityEngine;

public class jumpBoosterPickUp : MonoBehaviour
{
    public float duration = 7f;

    [Header("Pool binding")]
    public GameObject prefabKey; // שייכי באינספקטור לאיזה פריפאב שייך הפאווראפ הזה

    [Header("Optional FX")]
    public AudioClip pickupSfx;
    public ParticleSystem pickupVfx;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // מציאת המנהל על השחקן/ילדיו
        var collector = other.GetComponentInChildren<jumpBoosterCollector>();
        if (!collector) collector = jumpBoosterCollector.I;

        if (collector != null) 
        {
            
            collector.Activate(duration);

            // אפקטים (אופציונלי)
            if (pickupVfx)
            {
                var v = Instantiate(pickupVfx, transform.position, Quaternion.identity);
                v.Play();
                Destroy(v.gameObject, v.main.duration + 0.1f);
            }
            if (pickupSfx)
                AudioSource.PlayClipAtPoint(pickupSfx, transform.position);

            if(prefabKey)
                PowerupPool.I.Release(gameObject, prefabKey);
            else
                gameObject.SetActive(false);
        }
    }
}
