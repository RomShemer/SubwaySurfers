using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MusicToggleButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private AudioSource backgroundMusic;  
    [SerializeField] private Image buttonImage;            
    [SerializeField] private Sprite soundOnSprite;         // 🔈 
    [SerializeField] private Sprite soundOffSprite;        // 🔇 
    [SerializeField] private Sprite soundOnHoverSprite;    
    [SerializeField] private Sprite soundOffHoverSprite;   

    private bool isHovering = false;

    private void Awake()
    {
        if (!backgroundMusic)
        {
            var go = GameObject.Find("BackgroundMusic");
            if (go) backgroundMusic = go.GetComponent<AudioSource>();
            if (!backgroundMusic) backgroundMusic = FindObjectOfType<AudioSource>();
        }
        if (!buttonImage) buttonImage = GetComponent<Image>();
    }

    private void Start()
    {
        UpdateIcon();
    }

    public void ToggleMusic()
    {
        if (!backgroundMusic) return;

        backgroundMusic.mute = !backgroundMusic.mute;
        AudioListener.pause = false;
        UpdateIcon();
    }

    private void UpdateIcon()
    {
        if (!buttonImage) return;

        bool soundOn = backgroundMusic && !backgroundMusic.mute;

        if (isHovering)
        {
            buttonImage.sprite = soundOn ? soundOnHoverSprite : soundOffHoverSprite;
        }
        else
        {
            buttonImage.sprite = soundOn ? soundOnSprite : soundOffSprite;
        }
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