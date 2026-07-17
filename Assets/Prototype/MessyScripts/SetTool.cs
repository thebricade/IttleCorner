using UnityEngine;
using UnityEngine.UI;

public class SetTool : MonoBehaviour
{
    public DrawingPad.DrawingTool toolType;
    public DrawingPad drawingPad;

    private void Start()
    {
        Button button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        
    }

    void OnClick()
    {
        drawingPad.SetDrawingTool(toolType);
    }
}
