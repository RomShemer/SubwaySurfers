using TMPro;
using UnityEngine;

public class CoinCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text runText;   // השדה של הטקסט על המסך
    [SerializeField] private TMP_Text totalText; // אופציונלי – “Overall”

    void OnEnable()
    {
        if (CoinManager.I != null) CoinManager.I.OnChanged += HandleChanged;
        HandleChanged(CoinManager.I?.RunCoins ?? 0, CoinManager.I?.TotalCoins ?? 0);
    }
    void OnDisable()
    {
        if (CoinManager.I != null) CoinManager.I.OnChanged -= HandleChanged;
    }
    void HandleChanged(int run, int total)
    {
        if (runText) runText.text = run.ToString();       // לדוגמה: "x 123" אם תרצי
        if (totalText) totalText.text = total.ToString();
    }
}