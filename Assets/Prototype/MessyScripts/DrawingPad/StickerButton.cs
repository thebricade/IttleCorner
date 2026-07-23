using UnityEngine;
using UnityEngine.UI;

public class StickerButton : MonoBehaviour
{
    private Drawing myDrawing;

    public void Setup(Drawing drawing)
    {
        myDrawing = drawing;

        RawImage image = GetComponent<RawImage>();
        image.texture = drawing.texture;

        Button button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        Debug.Log("Selected: " + myDrawing.drawingName);
        DrawingManager.Instance.SelectDrawing(myDrawing);
    }
}