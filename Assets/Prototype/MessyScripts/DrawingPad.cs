using UnityEngine;
using UnityEngine.UI;

public class DrawingPad : MonoBehaviour
{
    public int textureSize = 256;
    private Texture2D drawTexture;
    private RawImage rawImage;
    private Vector2? lastLocalPoint;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rawImage=  GetComponent<RawImage>();
        drawTexture = new Texture2D(textureSize, textureSize);
        //fill with transparent pixels because we'll cut this out later
        Color[] fillColor = new Color[textureSize * textureSize];
        for (int i = 0; i < fillColor.Length; i++)
        {
            fillColor[i] = Color.clear;
        }
        drawTexture.SetPixels(fillColor);
        drawTexture.Apply();
        rawImage.texture = drawTexture;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector2 localPoint;
            RectTransform rt = GetComponent<RectTransform>();
            
            bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Input.mousePosition, null, out localPoint);
            if (inside)
            {
                if (lastLocalPoint.HasValue)
                {
                    PaintLine(lastLocalPoint.Value, localPoint, rt);
                }
                else
                {
                    PaintAtLocalPoint(localPoint, rt);
                }
                lastLocalPoint = localPoint;
            }
            else
            {
                {
                    lastLocalPoint = null;
                }
            }
        }

        if (!Input.GetMouseButton(0))
        {
            lastLocalPoint = null;
        }
    }
    void PaintLine(Vector2 from, Vector2 to, RectTransform rt)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.CeilToInt(distance);

        for (int step = 0; step <= steps; step++)
        {
            float t = (float)step / steps;
            Vector2 point = Vector2.Lerp(from, to, t);
            PaintAtLocalPoint(point, rt);
        }
    }
    void PaintAtLocalPoint(Vector2 localPoint, RectTransform rt)
    {
        // convert from "center" to uv range
        float u = (localPoint.x / rt.rect.width) + 0.5f;
        float v = (localPoint.y / rt.rect.height) + 0.5f;

        int x = (int)(u * textureSize);
        int y = (int)(v * textureSize);

        int brushSize = 4; //make this changable 

        for (int i = -brushSize; i < brushSize; i++)
        {
            for (int j = -brushSize; j < brushSize; j++)
            {
                int px = x + i;
                int py = y + j; 
                
                //make sure we don't crashout near an edge
                if (px >= 0 && px < textureSize && py >= 0 && py < textureSize)
                {
                    drawTexture.SetPixel(px,py,Color.black);
                }
            }
        }
        drawTexture.Apply();
    }
}

