using UnityEngine;

[System.Serializable]

public class MagnetAnimatorBridge : MonoBehaviour
{
    [SerializeField] Animator playerAnimator;
    [SerializeField] GameObject magnetVisual;
    [SerializeField] MagnetCollector collector;

    void Reset()
    {
        if (!playerAnimator) playerAnimator = GetComponentInChildren<Animator>();
        if (!collector) collector = GetComponentInChildren<MagnetCollector>();
    }

    void OnEnable()
    {
        if (collector != null)
        {
            collector.OnActivated += HandleOn;
            collector.OnExpired   += HandleOff;
        }
    }
    void OnDisable()
    {
        if (collector != null)
        {
            collector.OnActivated -= HandleOn;
            collector.OnExpired   -= HandleOff;
        }
    }

    void HandleOn()
    {
        if (magnetVisual) magnetVisual.SetActive(true);
        if (playerAnimator) playerAnimator.SetBool("magnetOn", true);
    }
    void HandleOff()
    {
        if (magnetVisual) magnetVisual.SetActive(false);
        if (playerAnimator) playerAnimator.SetBool("magnetOn", false);
    }
}
