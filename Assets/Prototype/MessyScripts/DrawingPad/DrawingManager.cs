using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlacedDrawing
{
    public string tag;
    public Vector3 worldPosition;
    public GameObject worldObject;
}

[System.Serializable]
public class NPCRuntimeState
{
    public string npcName;
    public bool requestComplete;
}

public class DrawingManager : MonoBehaviour
{
    public static DrawingManager Instance;

    public List<Drawing> savedDrawings = new List<Drawing>();
    public Drawing selectedDrawing;
    public string pendingDrawingTag = "";
    public List<PlacedDrawing> placedDrawings = new List<PlacedDrawing>();
    public List<NPCRuntimeState> npcStates = new List<NPCRuntimeState>();

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
        Debug.Log("Saved drawing: " + name);
    }

    public void SelectDrawing(Drawing drawing)
    {
        selectedDrawing = drawing;
        GameModeManager.Instance.SetGameMode(GameMode.Placing);
        Debug.Log("Now Selected: " + drawing.drawingName);
    }

    public void SetPendingTag(string tag)
    {
        pendingDrawingTag = tag;
        Debug.Log("Pending tag set: " + tag);
    }

    public void RegisterPlacedDrawing(string tag, Vector3 position, GameObject obj)
    {
        PlacedDrawing placed = new PlacedDrawing();
        placed.tag = tag;
        placed.worldPosition = position;
        placed.worldObject = obj;
        placedDrawings.Add(placed);
        Debug.Log("Registered placed drawing: " + tag + " at " + position);
    }

    public NPCRuntimeState GetNPCState(NPCData npc)
    {
        NPCRuntimeState state = npcStates.Find(s => s.npcName == npc.npcName);

        if (state == null)
        {
            state = new NPCRuntimeState();
            state.npcName = npc.npcName;
            state.requestComplete = false;
            npcStates.Add(state);
        }

        return state;
    }
}
