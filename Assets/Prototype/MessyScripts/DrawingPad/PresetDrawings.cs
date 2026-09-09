using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GameDrawingEntry
{
    public string tag;
    public Texture2D texture;
}

public class PresetDrawings : MonoBehaviour
{
    public static PresetDrawings Instance;

    [Header("NPC/Game authored starting drawings")]
    public List<GameDrawingEntry> gameDrawings = new List<GameDrawingEntry>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    { 
        foreach (GameDrawingEntry entry in gameDrawings)
        {
            if (entry.texture != null && !string.IsNullOrEmpty(entry.tag))
            {
                DrawingManager.Instance.SaveGameDrawing(entry.texture, entry.tag);
            }
        }
    }
}