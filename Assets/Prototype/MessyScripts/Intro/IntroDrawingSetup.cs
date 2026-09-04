using UnityEngine;

public class IntroDrawingSetup : MonoBehaviour
{
    public DrawingPad drawingPad;
    public float colorCycleSpeed = 0.5f;

    void Start()
    {
        drawingPad.setBrushStyle(DrawingPad.BrushStyle.GelGlitter);
        drawingPad.SetBrushSize(13);
    }

    void Update()
    {
        float hue = Mathf.Repeat(Time.time * colorCycleSpeed, 1f);
        Color rainbowColor = Color.HSVToRGB(hue, 0.8f, 0.9f);
        drawingPad.SetBrushColor(rainbowColor);
    }
}
