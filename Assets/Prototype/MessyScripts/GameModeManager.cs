using UnityEngine;

public enum GameMode
{
    Explore, //player walking around
    Drawing, //player making drawings
    Placing, //player placing creations
    Sticker //player browsing creations
}
public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance;
    public GameMode currentMode = GameMode.Explore;

    //what would be turned on for each mode change
    public GameObject exploreScreen; 
    public GameObject drawingScreen;
    public GameObject stickerScreen;
    public GameObject placingScreen;

    void Awake()
    {
        Instance = this; 
    }

    public void SetGameMode(GameMode mode)
    {
        currentMode = mode;
        Debug.Log("Switching to mode: " + mode);
        //dictating what is turned on based off the current mode you are working on
        exploreScreen.SetActive(mode==GameMode.Explore || mode==GameMode.Sticker||mode==GameMode.Placing);
        drawingScreen.SetActive(mode==GameMode.Drawing);
        stickerScreen.SetActive(mode==GameMode.Sticker);
        placingScreen.SetActive(mode==GameMode.Placing);
    }
}
