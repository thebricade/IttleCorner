using UnityEngine;

public class IntroDrawingSetup : MonoBehaviour
{
    public DrawingPad drawingPad;
    public float colorCycleSpeed = 0.5f;

    void Start()
    {
        // Safety Check
        if (drawingPad == null)
        {
            Debug.LogError($"Alert: 'drawingPad' is NOT assigned in the Inspector on the '{gameObject.name}' GameObject! Please drag and drop your DrawingPad into the slot.", this);
            return;
        }

        drawingPad.setBrushStyle(DrawingPad.BrushStyle.GelGlitter);
        drawingPad.SetBrushSize(13);
    }

    void Update()
    {
        // If drawingPad missing, exit early so console doesn't spam errors every frame
        if (drawingPad == null)
        {
            return;
        }

        float hue = Mathf.Repeat(Time.time * colorCycleSpeed, 1f);
        Color rainbowColor = Color.HSVToRGB(hue, 0.8f, 0.9f);
        drawingPad.SetBrushColor(rainbowColor);
    }
}
