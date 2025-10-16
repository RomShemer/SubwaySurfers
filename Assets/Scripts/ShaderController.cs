using System.Collections.Generic;
using UnityEngine;

public class ShaderController : MonoBehaviour
{
    [Header("Property name (must match Shader Graph Reference)")]
    [SerializeField] string curveProp = "_CurveAmount";

    [Header("Mode")]
    [Tooltip("When ON: writes Shader.SetGlobalFloat(curveProp, value). " +
             "When OFF: writes per material via MaterialPropertyBlock on targets.")]
    [SerializeField] bool useGlobal = true;

    [Header("Targets (used only when useGlobal = false)")]
    [SerializeField] Renderer[] explicitTargets;
    [SerializeField] string findByTag = "";     
    [SerializeField] string findByLayer = "";  

    [Header("Curve value ranges")]
    [SerializeField, Range(-1f, 1f)] float maxAbsCurve = 0.25f;               
    [SerializeField] Vector2 randomRange = new Vector2(-0.18f, -0.02f);       

    [Header("Timing")]
    [SerializeField] bool startFlat = true;   
    [SerializeField] float fixedDuration = 3f; 
    [SerializeField] float transitionDuration = 2f; 

    float _cur, _vel, _target;
    float _timer;
    bool _inTransition;

    // Per-material path
    readonly List<Renderer> _targets = new List<Renderer>();
    readonly Dictionary<Renderer, MaterialPropertyBlock> _mpbs = new Dictionary<Renderer, MaterialPropertyBlock>();

    void Start()
    {
        SetCurveImmediate(0.0005f); 
    }
    
    void Awake()
    {
        _cur = startFlat ? 0f : Mathf.Clamp(Random.Range(randomRange.x, randomRange.y), -maxAbsCurve, maxAbsCurve);

        if (!useGlobal)
        {
            CollectTargets();
            foreach (var r in _targets) if (r) r.allowOcclusionWhenDynamic = r.allowOcclusionWhenDynamic; 
        }

        ApplyValue(_cur);
    }

    void Update()
    {
        if (!_inTransition)
        {
            _timer += Time.deltaTime;
            if (_timer >= fixedDuration)
            {
                _timer = 0f;
                _inTransition = true;
                _target = Mathf.Clamp(Random.Range(randomRange.x, randomRange.y), -maxAbsCurve, maxAbsCurve);
            }
        }

        if (_inTransition)
        {
            _cur = Mathf.SmoothDamp(_cur, _target, ref _vel, transitionDuration);
            if (Mathf.Abs(_cur - _target) < 0.002f) _inTransition = false;
        }

        ApplyValue(_cur);
    }

    // --- Public API ---
    public void SetCurveImmediate(float v)
    {
        _cur = Mathf.Clamp(v, -maxAbsCurve, maxAbsCurve);
        _inTransition = false;
        _vel = 0f;
        ApplyValue(_cur);
    }
    public void LerpTo(float v, float duration)
    {
        _target = Mathf.Clamp(v, -maxAbsCurve, maxAbsCurve);
        transitionDuration = Mathf.Max(0.0001f, duration);
        _inTransition = true;
    }

    // --- Helpers ---
    void ApplyValue(float v)
    {
        if (useGlobal)
        {
            Shader.SetGlobalFloat(curveProp, v);
        }
        else
        {
            foreach (var r in _targets)
            {
                if (!r) continue;
                if (!_mpbs.TryGetValue(r, out var mpb))
                {
                    mpb = new MaterialPropertyBlock();
                    _mpbs[r] = mpb;
                }
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(curveProp, v);
                r.SetPropertyBlock(mpb);
            }
        }
    }

    void CollectTargets()
    {
        _targets.Clear();
        if (explicitTargets != null && explicitTargets.Length > 0)
        {
            _targets.AddRange(explicitTargets);
        }
        else
        {
            if (!string.IsNullOrEmpty(findByTag))
            {
                var gos = GameObject.FindGameObjectsWithTag(findByTag);
                foreach (var go in gos)
                    _targets.AddRange(go.GetComponentsInChildren<Renderer>(true));
            }
            if (!string.IsNullOrEmpty(findByLayer))
            {
                int layer = LayerMask.NameToLayer(findByLayer);
                if (layer >= 0)
                {
                    var all = FindObjectsOfType<Renderer>(true);
                    foreach (var r in all) if (r.gameObject.layer == layer) _targets.Add(r);
                }
            }
            if (_targets.Count == 0)
            {
                _targets.AddRange(FindObjectsOfType<Renderer>(true));
            }
        }
    }
}
