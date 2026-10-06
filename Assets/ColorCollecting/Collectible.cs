using UnityEngine;

public class Collectible : MonoBehaviour
{
    public int value = 1;

    private void OnMouseDown()
    {
        Debug.Log("clicked");
        if (GameModeManager.Instance.currentMode != GameMode.Explore) return;
        
        Debug.Log("add currency");
        Wallet.Instance.AddCurrency(value);
        Debug.Log("Collected! Balance: " + Wallet.Instance.GetBalance());
        gameObject.SetActive(false);
    }
}