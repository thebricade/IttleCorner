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
        Texture2D snapshot = drawingPad.GetCurrentTextureCopy();
        Texture2D cropped = drawingPad.CropToContent(snapshot);
        
        string drawingName = !string.IsNullOrEmpty(DrawingManager.Instance.pendingDrawingTag)
            ? DrawingManager.Instance.pendingDrawingTag
            : "Drawing " + (DrawingManager.Instance.savedDrawings.Count + 1);

        DrawingManager.Instance.SaveDrawing(cropped, drawingName);
        DrawingManager.Instance.pendingDrawingTag = ""; // clear after use
    }
}
