using UnityEngine;

public class Wallet : MonoBehaviour
{
    public static Wallet Instance;
    private int balance = 20;

    public int placementCost = 20; 
    
    
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

    public bool CanAfford(int amount)
    {
        return balance >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount))
        {
            Debug.Log("Not enough current balance " + balance );
            onInsufficientFunds.Invoke();
            return false;
        }
        balance -= amount;
        Debug.Log("Spend current balance: " + balance);
        onBalanceChanged?.Invoke(balance);
        return true;
    }
    

    public int GetBalance() => balance;

    public System.Action<int> onBalanceChanged;
    public System.Action onInsufficientFunds;
}
