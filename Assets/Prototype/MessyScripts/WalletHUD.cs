using UnityEngine;
using TMPro;

public class WalletHUD : MonoBehaviour
{
    public GameObject hudObject;
    public TMP_Text currencyText;
    private Coroutine hideCoroutine; 
    public float displayDuration = 3f;

    void Start()
    {
        Wallet.Instance.onBalanceChanged += UpdateDisplay;
        Wallet.Instance.onInsufficientFunds += OnInsufficientFunds;
        UpdateDisplay(Wallet.Instance.GetBalance());
        hudObject.SetActive(false);
    }

    void OnBalanceChanged(int newBalance)
    {
        UpdateDisplay(newBalance);
        ShowHUD();
    }

    void ShowHUD()
    {
        hudObject.SetActive(true);
        
        if(hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterDealy());
    }

    System.Collections.IEnumerator HideAfterDealy()
    {
        yield return new WaitForSeconds(displayDuration);
        hudObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Wallet.Instance != null)
        {
            Wallet.Instance.onBalanceChanged -= UpdateDisplay;
            Wallet.Instance.onInsufficientFunds -= OnInsufficientFunds;
        }
    }
    
    void OnInsufficientFunds()
    {
        currencyText.color = Color.red;
        // reset back to normal after a moment
        StartCoroutine(ResetColor());
    }

    System.Collections.IEnumerator ResetColor()
    {
        yield return new WaitForSeconds(1.5f);
        currencyText.color = Color.white; // or whatever your normal color is
    }

    void UpdateDisplay(int newBalance)
    {
        currencyText.text = "Scrap: " + newBalance;
    }
}
