using System.Collections;
using TMPro;
using UnityEngine;

public class CoinCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text runText;
    [SerializeField] private TMP_Text totalText;

    private bool _subscribed;

    private void OnEnable()
    {
        // ננסה מיד, ואם הסינגלטון עדיין לא מוכן – נחכה בקורוטינה קצרה
        TrySubscribe();
        if (!_subscribed) StartCoroutine(WaitAndSubscribe());
    }

    private void OnDisable()
    {
        if (CoinManager.I != null) CoinManager.I.OnChanged -= HandleChanged;
        _subscribed = false;
    }

    private IEnumerator WaitAndSubscribe()
    {
        // מחכה עד שמופיע CoinManager.I (למקרה שסדר האתחול גורם לעיכוב)
        while (CoinManager.I == null)
            yield return null;

        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (CoinManager.I == null) return;
        if (!_subscribed)
        {
            CoinManager.I.OnChanged += HandleChanged;
            _subscribed = true;
        }

        // משיכה חד־פעמית של הערכים הנוכחיים גם אם לא נורה אירוע עדיין
        HandleChanged(CoinManager.I.RunCoins, CoinManager.I.TotalCoins);
    }

    private void HandleChanged(int run, int total)
    {
        if (runText)   runText.text   = run.ToString();
        if (totalText) totalText.text = total.ToString();
    }
}