using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class DrawingPad : MonoBehaviour
{
    public enum DrawingTool
    {
        Brush,
        Eraser,
    }

    public enum BrushStyle
    {
        Big,
        Medium,
    }

    private DrawingTool currentDrawingTool = DrawingTool.Brush;
    private BrushStyle currentBrushStyle = BrushStyle.Big;
    private int brushSize = 4;
    public int textureSize = 256;
    private Texture2D drawTexture;
    private RawImage rawImage;
    private Vector2? lastLocalPoint;
    private Color brushColor = Color.black; // starting brush color
    
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

        //int brushSize = 4; //make this changable 

        for (int i = -brushSize; i < brushSize; i++)
        {
            for (int j = -brushSize; j < brushSize; j++)
            {
                int px = x + i;
                int py = y + j; 
                
                //make sure we don't crashout near an edge
                if (px >= 0 && px < textureSize && py >= 0 && py < textureSize)
                {
                    float dist = Mathf.Sqrt(i*i+j*j); // this is what helps make it circle 
                    //kinda in the same brain we could have maybe a texture2d image here that is in greyscale
                    //this would allow us to have crayon texture/ pencil/paint

                    if (dist <= brushSize)
                    {
                        if (currentDrawingTool == DrawingTool.Brush) // look into reducing nested ifs here clean up
                        {
                            //check type of brush
                            if (currentBrushStyle == BrushStyle.Big)
                            {
                                SetBrushSize(8);
                                drawTexture.SetPixel(px, py, brushColor);
                            }else if (currentBrushStyle == BrushStyle.Medium)
                            {
                                SetBrushSize(4);
                                drawTexture.SetPixel(px, py, brushColor); 
                            }
                            
                        }
                        else if (currentDrawingTool == DrawingTool.Eraser)
                        {
                            drawTexture.SetPixel(px, py, Color.clear); //transparency when eraser is selected
                        }
                    }
                }
            }
        }
        drawTexture.Apply();
    }
    
    public void SetBrushColor(Color newColor)
    {
        brushColor = newColor;
    }

    public void SetDrawingTool(DrawingTool tool)
    {
        currentDrawingTool = tool;
    }

    public void setBrushStyle(BrushStyle style)
    {
        currentBrushStyle = style;
    }

    public void SetBrushSize(int size)
    {
        brushSize = size;
    }

    public Texture2D GetCurrentTextureCopy()
    {
        Texture2D copy = new Texture2D(textureSize, textureSize);
        copy.SetPixels(drawTexture.GetPixels());
        copy.Apply();
        return copy;
    }
    
    public Texture2D CropToContent(Texture2D source)
    {
        int minX = source.width;
        int minY = source.height;
        int maxX = 0;
        int maxY = 0;

        Color[] pixels = source.GetPixels();

        for (int y = 0; y < source.height; y++)
        {
            for (int x = 0; x < source.width; x++)
            {
                Color pixel = pixels[y * source.width + x];

                if (pixel.a > 0.01f) // not transparent
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        // nothing was drawn at all - avoid crashing
        if (maxX < minX || maxY < minY)
        {
            return source;
        }

        int croppedWidth = (maxX - minX) + 1;
        int croppedHeight = (maxY - minY) + 1;

        Texture2D cropped = new Texture2D(croppedWidth, croppedHeight);
        Color[] croppedPixels = source.GetPixels(minX, minY, croppedWidth, croppedHeight);
        cropped.SetPixels(croppedPixels);
        cropped.Apply();

        return cropped;
    }
}

