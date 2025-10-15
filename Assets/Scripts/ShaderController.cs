/*using UnityEngine;

public class ShaderController : MonoBehaviour
{
    [SerializeField, Range(-1, 1)] private float curveX;
    [SerializeField, Range(-1, 1)] private float curveY;
    [SerializeField] private Material[] materials;
    [SerializeField] private float transitionDuration = 2f;
    [SerializeField] private float fixedDuration = 3f;
    public GameManager gameManager;

    private float timer;
    private bool isTransitioning;
    private float currentCurveX;
    private float currentCurveY;

    void Start()
    {
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        currentCurveX = 0f;
        currentCurveY = 0f;
        foreach (var m in materials)
        {
            m.SetFloat(Shader.PropertyToID("_Curve_X"), currentCurveX);
            m.SetFloat(Shader.PropertyToID("_Curve_Y"), currentCurveY);
        }

        if (gameManager.CanMove)
        {
            currentCurveX = UnityEngine.Random.Range(-0.3f, 0.3f);
            currentCurveY = UnityEngine.Random.Range(-0.3f, 0.0f); // עיקול רק מטה
        }
    }

    void Update()
    {
        if (gameManager.CanMove)
        {
            if (!isTransitioning)
            {
                timer += Time.deltaTime;
                if (timer >= fixedDuration)
                {
                    isTransitioning = true;
                    timer = 0f;
                    float targetCurveX = UnityEngine.Random.Range(-0.3f, 0.3f);
                    float targetCurveY = UnityEngine.Random.Range(-0.3f, 0.0f);
                    StartCoroutine(TransitionCurve(targetCurveX, targetCurveY));
                }
            }
        }

        foreach (var m in materials)
        {
            m.SetFloat(Shader.PropertyToID("_Curve_X"), currentCurveX);
            m.SetFloat(Shader.PropertyToID("_Curve_Y"), currentCurveY);
        }

        Shader.SetGlobalFloat("_Curve_X", currentCurveX);
        Shader.SetGlobalFloat("_Curve_Y", currentCurveY);

    }

    private System.Collections.IEnumerator TransitionCurve(float targetCurveX, float targetCurveY)
    {
        float elapsedTime = 0f;
        float startCurveX = currentCurveX;
        float startCurveY = currentCurveY;
        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / transitionDuration;
            currentCurveX = Mathf.Lerp(startCurveX, targetCurveX, t);
            currentCurveY = Mathf.Lerp(startCurveY, targetCurveY, t);
            yield return null;
        }
        currentCurveX = targetCurveX;
        currentCurveY = targetCurveY;
        isTransitioning = false;
    }
} */

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
    [SerializeField] string findByTag = "";     // אופציונלי: תג (למשל "Road")
    [SerializeField] string findByLayer = "";   // אופציונלי: שכבה (למשל "Road")

    [Header("Curve value ranges")]
    [SerializeField, Range(-1f, 1f)] float maxAbsCurve = 0.25f;               // קלמפ קשיח
    [SerializeField] Vector2 randomRange = new Vector2(-0.18f, -0.02f);       // למשל רק כלפי מטה

    [Header("Timing")]
    [SerializeField] bool startFlat = true;     // להתחיל מ-0
    [SerializeField] float fixedDuration = 3f;  // כמה זמן להישאר על ערך
    [SerializeField] float transitionDuration = 2f; // כמה זמן "גלישה" לערך הבא

    float _cur, _vel, _target;
    float _timer;
    bool _inTransition;

    // Per-material path
    readonly List<Renderer> _targets = new List<Renderer>();
    readonly Dictionary<Renderer, MaterialPropertyBlock> _mpbs = new Dictionary<Renderer, MaterialPropertyBlock>();

    void Start()
    {
        // התנע ערך לא-אפס כדי לראות מיד עיקום
        SetCurveImmediate(0.0005f);   // ← הוסף/י שורה זו
    }
    
    void Awake()
    {
        _cur = startFlat ? 0f : Mathf.Clamp(Random.Range(randomRange.x, randomRange.y), -maxAbsCurve, maxAbsCurve);

        if (!useGlobal)
        {
            CollectTargets();
            // מכבים אינסטנסינג כדי שלא יעקוף פר-אינסטנס ערכים
            foreach (var r in _targets) if (r) r.allowOcclusionWhenDynamic = r.allowOcclusionWhenDynamic; // dummy line כדי לשמור על הסדר
        }

        ApplyValue(_cur); // סטארט
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

    // --- Public API אם תרצי לשלוט ידנית מבחוץ ---
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
                // fallback: עדכן את כל הרנדררים בסצנה (לא מומלץ בפרודקשן)
                _targets.AddRange(FindObjectsOfType<Renderer>(true));
            }
        }
    }
}
