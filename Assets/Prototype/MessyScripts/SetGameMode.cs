using UnityEngine;
using UnityEngine.UI;

public class SetGameMode : MonoBehaviour
{
    public GameMode modeToSet;


    private void Start()
    {
        Button button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        
    }

    void OnClick()
    {
       GameModeManager.Instance.SetGameMode(modeToSet);
    }
}
