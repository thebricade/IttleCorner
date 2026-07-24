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
        DrawingManager.Instance.SaveDrawing(cropped, "Drawing " + (DrawingManager.Instance.savedDrawings.Count + 1));
    }
}
