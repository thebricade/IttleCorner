using System.Collections.Generic;
using UnityEngine;

public class DrawingManager : MonoBehaviour
{
    public static DrawingManager Instance;

    public List<Drawing> savedDrawings = new List<Drawing>();
    public Drawing selectedDrawing;

    void Awake()
    {
        Instance = this;
    }

    public void SaveDrawing(Texture2D texture, string name)
    {
        Drawing newDrawing = new Drawing();
        newDrawing.texture = texture;
        newDrawing.drawingName = name;
        savedDrawings.Add(newDrawing);
        Debug.Log("Saved drawing:  " + name);
    }

    public void SelectDrawing(Drawing drawing)
    {
        selectedDrawing = drawing;
        GameModeManager.Instance.SetGameMode(GameMode.Placing);
        Debug.Log("Now Selected: " + drawing.drawingName);
    }
 
}
