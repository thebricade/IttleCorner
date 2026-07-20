using System;
using UnityEngine;
using UnityEngine.UI;

public class SetColor : MonoBehaviour
{
    public Color buttonColor;
    public DrawingPad drawingPad;

    private void Start()
    {
        Button button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        
    }

    void OnClick()
    {
        drawingPad.SetBrushColor(buttonColor);
    }
}
