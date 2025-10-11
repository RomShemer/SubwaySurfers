using UnityEngine;

public class jumpBoosterCollector : MonoBehaviour
{
    public static jumpBoosterCollector I { get; private set; }

    [Header("Links")]
    [SerializeField] private GameObject leftBootsVisual;
    [SerializeField] private GameObject rightBootsVisual;

    [Header("Timing")]
    [SerializeField] private float defaultDuration = 7f;
    private float _timer;

    [Header("Player")]
    [SerializeField] PlayerController player;

    public bool Active => _timer > 0f;

    // אופציונלי: אירועים למי שרוצה להאזין (UI, סאונד)
    public System.Action OnActivated;
    public System.Action OnExpired;

    void Awake()
    {
        I = this;
        if (leftBootsVisual) leftBootsVisual.SetActive(false);
        if (rightBootsVisual) rightBootsVisual.SetActive(false);
    }

    void Update()
    {
        if (_timer > 0f)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) Expire();
        }
    }

    /// הפעלה/רענון משך הנעליים
    public void Activate(float seconds = -1f)
    {
        float add = (seconds > 0f ? seconds : defaultDuration);
        // הארכת משך אם כבר פועל, או התחלה אם כבוי
        bool wasActive = Active;
        _timer = Mathf.Max(_timer, 0f) + (wasActive ? add : add);

        if (!wasActive)
        {
            if (player != null)
            {
                Debug.Log("start jump booster");
                player.IsJumpBooster = true;
            }
            
            // הדלקת ויזואל/אנימציה
            if (leftBootsVisual) leftBootsVisual.SetActive(true);
            if (rightBootsVisual) rightBootsVisual.SetActive(true);
            
            OnActivated?.Invoke();
        }
    }

    /// כיבוי ידני/אוטומטי בסוף הזמן
    public void Expire()
    {
        _timer = 0f;

        if (player != null)
        {
            Debug.Log("end jump booster");
            player.IsJumpBooster = false;
        }
        
        if (leftBootsVisual) leftBootsVisual.SetActive(false);
        if (rightBootsVisual) rightBootsVisual.SetActive(false);

        OnExpired?.Invoke();
    }

    public void ForceStop() => Expire();

}
