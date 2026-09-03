using UnityEngine;

public class Wallet : MonoBehaviour
{
    public static Wallet Instance;
    private int balance = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddCurrency(int amount)
    {
        balance += amount;
        Debug.Log("Currency balance: " + balance);
        onBalanceChanged?.Invoke(balance);
    }

    public int GetBalance() => balance;

    public System.Action<int> onBalanceChanged;
}
