using UnityEngine;

public enum GameMode
{
    Explore,
    Drawing,
    Sticker
}
public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance;
    public GameMode currentMode = GameMode.Explore;

    //what would be turned on for each mode change
    public GameObject exploreScreen;
    public GameObject drawingScreen;
    public GameObject stickerScreen;

    void Awake()
    {
        Instance = this; 
    }

    public void SetGameMode(GameMode mode)
    {
        currentMode = mode;
        
        //dictating what is turned on based off the current mode you are working on
        exploreScreen.SetActive(mode==GameMode.Explore || mode==GameMode.Sticker);
        drawingScreen.SetActive(mode==GameMode.Drawing);
        stickerScreen.SetActive(mode==GameMode.Sticker)                                     ;
    }
}
