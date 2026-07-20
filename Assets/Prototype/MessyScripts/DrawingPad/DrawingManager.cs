using System.Collections.Generic;
using UnityEngine;

public class DrawingManager : MonoBehaviour
{
    public static DrawingManager Instance;

    public List<Drawing> savedDrawings = new List<Drawing>();

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
 
}
