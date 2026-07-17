using UnityEngine;
using UnityEngine.UI;

public class SetBrushType : MonoBehaviour
{
    public DrawingPad.BrushStyle brushStyle;
    public DrawingPad drawingPad;

    private void Start()
    {
        Button button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        
    }

    void OnClick()
    {
        drawingPad.setBrushStyle(brushStyle);
    }
}
