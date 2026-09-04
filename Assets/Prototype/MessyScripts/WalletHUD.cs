using UnityEngine;
using TMPro;

public class WalletHUD : MonoBehaviour
{
    public TMP_Text currencyText;

    void Start()
    {
        Wallet.Instance.onBalanceChanged += UpdateDisplay;
        UpdateDisplay(Wallet.Instance.GetBalance());
    }

    void OnDestroy()
    {
        if (Wallet.Instance != null)
            Wallet.Instance.onBalanceChanged -= UpdateDisplay;
    }

    void UpdateDisplay(int newBalance)
    {
        currencyText.text = "Scrap: " + newBalance;
    }
}
