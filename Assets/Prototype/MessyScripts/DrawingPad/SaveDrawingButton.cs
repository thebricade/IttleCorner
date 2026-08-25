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
        //gets whatever is currently drawn on the canvas
        Texture2D snapshot = drawingPad.GetCurrentTextureCopy();
        //crops any transparent space 
        Texture2D cropped = drawingPad.CropToContent(snapshot);
        
        Debug.Log("drawingForNPC: '" + DrawingManager.Instance.drawingForNPC + "'");
        
        //names the drawing, if pendingdrawingtag was set we'll use that name for the quest, otherwise it will auto gen name Drawing 1, Drawing2 ect
        string drawingName = !string.IsNullOrEmpty(DrawingManager.Instance.pendingDrawingTag)
            ? DrawingManager.Instance.pendingDrawingTag
            : "Drawing " + (DrawingManager.Instance.savedDrawings.Count + 1);
        
        //drawingforNPC is set in opendrawing.quest only when CreateNPC for other quest it stays empty
        //is this a CreateNPC quest? yes/no
        if (!string.IsNullOrEmpty(DrawingManager.Instance.drawingForNPC))
        {
            // if yes - find the NPC by name using our registry so we can replace it's sprite
            GameObject npcObject = NPCManager.Instance.FindNPC(DrawingManager.Instance.drawingForNPC);
            Debug.Log("NPC object found: " + (npcObject != null ? npcObject.name : "NULL"));
            if (npcObject != null)
            {
                SpriteRenderer sr = npcObject.GetComponent<SpriteRenderer>();

                if (sr != null)
                {
                    //converts the texture2D to a sprite
                    Sprite newSprite = Sprite.Create(
                        cropped,
                        new Rect(0, 0, cropped.width, cropped.height),
                        new Vector2(0.5f, 0f)
                    );
                    //swap the NPC sprite with the drawing
                    sr.sprite = newSprite;
                    Debug.Log("Applied drawing to NPC: " + DrawingManager.Instance.drawingForNPC);
                }
            }
            else
            {
                Debug.LogWarning("NPC not found: " + DrawingManager.Instance.drawingForNPC);
            }

            // does not save the drawing to our Sticker list. We may eventually want another area for saved NPC drawings
            //DrawingManager.Instance.SaveDrawing(cropped, drawingName);
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
