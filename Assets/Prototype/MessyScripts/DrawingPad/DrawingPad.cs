using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using UnityEngine.EventSystems;

public class DrawingPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
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
        Small,
    }

    private DrawingTool currentDrawingTool = DrawingTool.Brush;
    private BrushStyle currentBrushStyle = BrushStyle.Big;
    private int brushSize = 4;
    public int textureSize = 256;
    private Texture2D drawTexture;
    private RawImage rawImage;
    private RectTransform rectTransform;
    private Vector2? lastLocalPoint;
    private Color brushColor = Color.black; // starting brush color
    private bool isDrawing = false;
    private Camera pressCamera;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    private void OnEnable()
    {
        isDrawing = false;
        lastLocalPoint = null;
        ClearDrawBoard();
    }

    private void OnDisable()
    {
        isDrawing = false;
        lastLocalPoint = null;
    }

    // Only fires when uGUI's raycaster resolves the pointer-down onto this RawImage itself -
    // a click on a color/tool button never reaches here, so there's no frame where a stray
    // paint can sneak through while the pointer is actually over other UI.
    public void OnPointerDown(PointerEventData eventData)
    {
        pressCamera = eventData.pressEventCamera;
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, pressCamera, out localPoint))
        {
            isDrawing = true;
            PaintAtLocalPoint(localPoint, rectTransform);
            lastLocalPoint = localPoint;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDrawing = false;
        lastLocalPoint = null;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isDrawing)
        {
            return;
        }

        if (!Input.GetMouseButton(0))
        {
            isDrawing = false;
            lastLocalPoint = null;
            return;
        }

        Vector2 localPoint;
        bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, Input.mousePosition, pressCamera, out localPoint);
        if (inside)
        {
            if (lastLocalPoint.HasValue)
            {
                PaintLine(lastLocalPoint.Value, localPoint, rectTransform);
            }
            else
            {
                PaintAtLocalPoint(localPoint, rectTransform);
            }
            lastLocalPoint = localPoint;
        }
        else
        {
            lastLocalPoint = null;
        }
    }
    void PaintLine(Vector2 from, Vector2 to, RectTransform rt)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.CeilToInt(distance);

        if (steps <= 0)
        {
            // from == to (mouse hasn't moved since the last sample) - painting a "line" here
            // would divide by zero (step / steps), so just paint the single point.
            PaintAtLocalPoint(to, rt);
            return;
        }

        for (int step = 0; step <= steps; step++)
        {
            float t = (float)step / steps;
            Vector2 point = Vector2.Lerp(from, to, t);
            PaintAtLocalPoint(point, rt);
        }
    }

    void ClearDrawBoard()
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
    
    void PaintAtLocalPoint(Vector2 localPoint, RectTransform rt)
    {
        // convert from "center" to uv range
        float u = (localPoint.x / rt.rect.width) + 0.5f;
        float v = (localPoint.y / rt.rect.height) + 0.5f;

        int x = (int)(u * textureSize);
        int y = (int)(v * textureSize);

        Color paintColor = brushColor; // default to the brush color
        
        //up here you are going to check what the current tool or brushsize is. 
        switch (currentDrawingTool)
        {
            case DrawingTool.Brush:
                if (currentBrushStyle == BrushStyle.Big)
                {
                    SetBrushSize(8);
                }else if (currentBrushStyle == BrushStyle.Medium)
                {
                    SetBrushSize(4);
                }else if (currentBrushStyle == BrushStyle.Small)
                {
                    SetBrushSize(2);
                }
                break;
            case DrawingTool.Eraser:
                paintColor = Color.clear; // local variable
                break; 
            default:
                Debug.Log("invalid tool");
                break;
        }
        
        for (int i = -brushSize; i < brushSize; i++) // this is the drawing loop, eventually this could be a method for each different style that varies on how it would draw
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
                        drawTexture.SetPixel(px, py, paintColor);
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

