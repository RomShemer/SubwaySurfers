using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Audio;

public class GlobalMuteToggleButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Mixer (אופציונלי אבל מומלץ)")]
    [SerializeField] private AudioMixer mixer;                
    [SerializeField] private string masterVolumeParam = "MasterVolume";

    [Header("UI")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite soundOnSprite;   // 🔈
    [SerializeField] private Sprite soundOffSprite;  // 🔇
    [SerializeField] private Sprite soundOnHoverSprite;
    [SerializeField] private Sprite soundOffHoverSprite;

    [Header("Prefs")]
    [SerializeField] private string prefsKey = "global_mute";

    private bool isHovering = false;
    private bool isMuted = false;

    private const float MutedDb = -80f;
    private const float UnmutedDb = 0f;

    private void Awake()
    {
        if (!buttonImage) buttonImage = GetComponent<Image>();

        isMuted = PlayerPrefs.GetInt(prefsKey, 0) == 1;

        ApplyMuteState();
        UpdateIcon();
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        PlayerPrefs.SetInt(prefsKey, isMuted ? 1 : 0);
        PlayerPrefs.Save();

        ApplyMuteState();
        UpdateIcon();
    }

    private void ApplyMuteState()
    {
        if (mixer)
        {
            mixer.SetFloat(masterVolumeParam, isMuted ? MutedDb : UnmutedDb);
        }

        AudioListener.pause = false;              
        AudioListener.volume = isMuted ? 0f : 1f;  
    }

    private void UpdateIcon()
    {
        if (!buttonImage) return;

        if (isHovering)
            buttonImage.sprite = isMuted ? soundOffHoverSprite : soundOnHoverSprite;
        else
            buttonImage.sprite = isMuted ? soundOffSprite : soundOnSprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        UpdateIcon();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        UpdateIcon();
    }
}
