using System.Collections;
using UnityEngine;
using UnityEngine.Audio;  

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class Coin : MonoBehaviour
{
    [Header("Gameplay")]
    public int value = 1;

    [Header("FX (optional)")]
    public AudioClip pickupClip;
    public ParticleSystem pickupVfx;

    [Header("Audio Routing")]
    public AudioMixerGroup sfxGroup;   
    [Range(0,1)] public float spatialBlend = 0.35f;
    [Range(0,256)] public int priority = 80;

    bool _collected;
    Collider _col;
    Renderer _rend;
    AudioSource _audio;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _rend = GetComponentInChildren<Renderer>();
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();

        _audio.playOnAwake = false;
        _audio.spatialBlend = spatialBlend; 
        _audio.priority = priority;
        _audio.outputAudioMixerGroup = sfxGroup;

        _col.isTrigger = true;
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Update()
    {
        var mc = MagnetCollector.I;
        if (mc != null && mc.Active && mc.ShouldAttract(transform.position, out Vector3 aim))
        {
            float targetY = mc.aimAnchor ? mc.aimAnchor.position.y : aim.y;
            float newY = Mathf.MoveTowards(transform.position.y, targetY, 5f * Time.deltaTime);
            Vector3 softAim = new Vector3(aim.x, newY, aim.z);

            transform.position = Vector3.MoveTowards(
                transform.position, softAim, mc.pullSpeed * Time.deltaTime);

            if ((transform.position - aim).sqrMagnitude <= mc.snapDistance * mc.snapDistance)
                Collect();
        }
    }

    public void ResetForReuse()
    {
        _collected = false;
        if (_col)  _col.enabled = true;
        if (_rend) _rend.enabled = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player")) return;

        _collected = true;
        Collect();
    }

    void Collect()
    {
        CoinManager.I?.Add(value);

        if (pickupVfx)
        {
            var v = Instantiate(pickupVfx, transform.position, Quaternion.identity);
            v.Play();
            Destroy(v.gameObject, v.main.duration + 0.1f);
        }
        if (pickupClip)
        {
            _audio.PlayOneShot(pickupClip);
        }

        if (_rend) _rend.enabled = false;
        if (_col)  _col.enabled = false;

        float delay = pickupClip ? pickupClip.length : 0f;
        StartCoroutine(ReturnToPoolAfter(delay));
    }

    IEnumerator ReturnToPoolAfter(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);
        CoinPool.I?.Return(this);
    }
}
