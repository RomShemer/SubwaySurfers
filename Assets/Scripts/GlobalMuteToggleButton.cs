// GlobalMuteToggleButton.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Audio;

public class GlobalMuteToggleButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Mixer (אופציונלי אבל מומלץ)")]
    [SerializeField] private AudioMixer mixer;                     // גרור לכאן את GameMixer
    [SerializeField] private string masterVolumeParam = "MasterVolume"; // חייב להיות בדיוק כמו Exposed Parameter

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

    // -80dB ≈ מושתק, 0dB = פול ווליום
    private const float MutedDb = -80f;
    private const float UnmutedDb = 0f;

    private void Awake()
    {
        if (!buttonImage) buttonImage = GetComponent<Image>();

        // טען מצב אחרון מה-PlayerPrefs
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
        // 1) אם יש מיקסר ופרמטר חשוף – נשתמש בו (משתיק הכל שעובר דרך המיקסר)
        if (mixer)
        {
            mixer.SetFloat(masterVolumeParam, isMuted ? MutedDb : UnmutedDb);
        }

        // 2) בנוסף תמיד נשלוט גם ב-AudioListener.volume כדי לתפוס *כל* מקור,
        //    כולל כאלה שלא מחוברים למיקסר (שחקן/שומר/בוסטרים וכו').
        AudioListener.pause = false;                 // לא מקפיאים אודיו; רק משתיקים
        AudioListener.volume = isMuted ? 0f : 1f;    // 0 = מושתק מוחלט, 1 = מלא
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
