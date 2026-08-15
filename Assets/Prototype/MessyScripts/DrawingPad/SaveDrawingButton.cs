using UnityEngine;
using UnityEngine.UI;

public class SaveDrawingButton : MonoBehaviour
{
    public DrawingPad drawingPad;

    private void Start()
    {
        Button button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        
    }

    void OnClick()
    {
        //grabs the texture and crops anywhere we didn't paint
        Texture2D snapshot = drawingPad.GetCurrentTextureCopy();
        Texture2D cropped = drawingPad.CropToContent(snapshot);
        Debug.Log("drawingForNPC: '" + DrawingManager.Instance.drawingForNPC + "'");
        //names the drawing based off it's current tag if a tag exists
        string drawingName = !string.IsNullOrEmpty(DrawingManager.Instance.pendingDrawingTag)
            ? DrawingManager.Instance.pendingDrawingTag
            : "Drawing " + (DrawingManager.Instance.savedDrawings.Count + 1);

        if (!string.IsNullOrEmpty(DrawingManager.Instance.drawingForNPC))
        {
            // find the NPC by name using our registry
            GameObject npcObject = NPCManager.Instance.FindNPC(DrawingManager.Instance.drawingForNPC);
            Debug.Log("NPC object found: " + (npcObject != null ? npcObject.name : "NULL"));
            if (npcObject != null)
            {
                SpriteRenderer sr = npcObject.GetComponent<SpriteRenderer>();

                if (sr != null)
                {
                    Sprite newSprite = Sprite.Create(
                        cropped,
                        new Rect(0, 0, cropped.width, cropped.height),
                        new Vector2(0.5f, 0f)
                    );
                    sr.sprite = newSprite;
                    Debug.Log("Applied drawing to NPC: " + DrawingManager.Instance.drawingForNPC);
                }
            }
            else
            {
                Debug.LogWarning("NPC not found: " + DrawingManager.Instance.drawingForNPC);
            }

            // still save the drawing regardless
            DrawingManager.Instance.SaveDrawing(cropped, drawingName);
        }
        else
        {
            DrawingManager.Instance.SaveDrawing(cropped, drawingName);
        }

        // always clear both pending values
        DrawingManager.Instance.pendingDrawingTag = "";
        DrawingManager.Instance.drawingForNPC = "";
        
       
    }
}
