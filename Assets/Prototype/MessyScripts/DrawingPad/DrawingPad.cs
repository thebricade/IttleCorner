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
        Watercolor,
        Watercolor2,
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

    [Tooltip("Optional stamp image for the watercolor brushes. Should be grayscale " +
             "(white = full opacity, black = none) - shape and texture come straight " +
             "from this image instead of the procedural circle. Must have Read/Write " +
             "Enabled checked in its texture import settings. Leave empty to use the " +
             "built-in procedural brush.")]
    public Texture2D brushStampTexture;

    // Per-stroke state for the watercolor brushes: strokeCoverage tracks how much
    // alpha the CURRENT stroke has already built up at each texel, so dragging back
    // and forth doesn't race to full opacity - it caps at a translucent wash. Lifting
    // the pen and starting a new stroke resets this, so a second pass can glaze darker.
    private float[] strokeCoverage;
    private Vector2 strokeGrainOffset;

    // How long (seconds) the pointer has been held roughly in place during the current
    // stroke. Watercolor brushes use this to keep deepening/spreading while you hold
    // still, like pigment blooming into wet paper, instead of hard-capping instantly.
    private float stationaryHoldTime = 0f;
    private const float StationaryMoveThreshold = 1.5f; // local-space pixels

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
            StartNewStroke();
            PaintAtLocalPoint(localPoint, rectTransform);
            lastLocalPoint = localPoint;
        }
    }

    void StartNewStroke()
    {
        int pixelCount = textureSize * textureSize;
        if (strokeCoverage == null || strokeCoverage.Length != pixelCount)
        {
            strokeCoverage = new float[pixelCount];
        }
        else
        {
            Array.Clear(strokeCoverage, 0, strokeCoverage.Length);
        }
        strokeGrainOffset = new Vector2(UnityEngine.Random.Range(0f, 1000f), UnityEngine.Random.Range(0f, 1000f));
        stationaryHoldTime = 0f;
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
                float moved = Vector2.Distance(lastLocalPoint.Value, localPoint);
                stationaryHoldTime = moved < StationaryMoveThreshold ? stationaryHoldTime + Time.deltaTime : 0f;
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
    float u = (localPoint.x / rt.rect.width) + 0.5f;
    float v = (localPoint.y / rt.rect.height) + 0.5f;

    int x = (int)(u * textureSize);
    int y = (int)(v * textureSize);

    switch (currentDrawingTool)
    {
        case DrawingTool.Brush:
            if (currentBrushStyle == BrushStyle.Big)
            {
                SetBrushSize(8);
                PaintHard(x, y);
            }
            else if (currentBrushStyle == BrushStyle.Medium)
            {
                SetBrushSize(4);
                PaintHard(x, y);
            }
            else if (currentBrushStyle == BrushStyle.Small)
            {
                SetBrushSize(2);
                PaintHard(x, y);
            }
            else if (currentBrushStyle == BrushStyle.Watercolor)
            {
                SetBrushSize(5);
                PaintWatercolor(x, y);
            }
            else if (currentBrushStyle == BrushStyle.Watercolor2)
            {
                SetBrushSize(6);
                PaintWatercolor2(x, y);
            }
            break;
        case DrawingTool.Eraser:
            PaintEraser(x, y);
            break;
        default:
            Debug.Log("invalid tool");
            break;
    }
}

void PaintHard(int centerX, int centerY)
{
    for (int i = -brushSize; i < brushSize; i++)
    {
        for (int j = -brushSize; j < brushSize; j++)
        {
            int px = centerX + i;
            int py = centerY + j;

            if (px >= 0 && px < textureSize && py >= 0 && py < textureSize)
            {
                float dist = Mathf.Sqrt(i * i + j * j);
                if (dist <= brushSize)
                {
                    drawTexture.SetPixel(px, py, brushColor);
                }
            }
        }
    }
    drawTexture.Apply();
}

void PaintEraser(int centerX, int centerY)
{
    for (int i = -brushSize; i < brushSize; i++)
    {
        for (int j = -brushSize; j < brushSize; j++)
        {
            int px = centerX + i;
            int py = centerY + j;

            if (px >= 0 && px < textureSize && py >= 0 && py < textureSize)
            {
                float dist = Mathf.Sqrt(i * i + j * j);
                if (dist <= brushSize)
                {
                    drawTexture.SetPixel(px, py, Color.clear);
                }
            }
        }
    }
    drawTexture.Apply();
}

// Tuning knobs for a watercolor preset - see WatercolorPresetV1/V2 below for the
// actual "Watercolor" vs "Watercolor2" personalities.
private struct WatercolorParams
{
    public int spreadPad;          // extra radius beyond brushSize for the bloom/backrun zone
    public float maxStrength;      // peak per-dab strength at the brush center
    public float strokeCap;        // max cumulative alpha a quick pass can build at a texel
    public float holdMaxCap;       // higher cap reachable if the brush is held roughly still
    public float holdSaturateSeconds; // seconds of holding still to approach holdMaxCap
    public float holdSpreadRate;   // extra texels of spread gained per second held still
    public float grainScale;       // paper-grain noise frequency
    public float grainMin;         // grain multiplies strength within this range
    public float grainMax;
    public float opacityJitterMin; // per-dab random opacity multiplier range, for natural
    public float opacityJitterMax; // stroke-to-stroke variation instead of uniform strength
    public float edgeNoiseAmount;  // how much perlin noise perturbs the circular edge (0-1)
    public float backrunBandStart; // fraction of radius where edge pooling/backrun starts
    public float backrunBoost;     // extra strength added in the backrun band
    public float bloomChance;      // per-texel chance of a fringe dot beyond the main edge
    public float bloomMin;
    public float bloomMax;
    public bool pigmentMix;        // true = multiply-mix with existing color (lets it show through as a tint/mix rather than a flat overlay)
}

// Watercolor: a soft, capped translucent wash. Underlying color reads through as a tint.
private static readonly WatercolorParams WatercolorPresetV1 = new WatercolorParams
{
    spreadPad = 5,
    maxStrength = 0.28f,
    strokeCap = 0.5f,
    holdMaxCap = 0.85f,
    holdSaturateSeconds = 1.4f,
    holdSpreadRate = 2.5f,
    grainScale = 0.15f,
    grainMin = 0.8f,
    grainMax = 1f,
    opacityJitterMin = 0.8f,
    opacityJitterMax = 1.15f,
    edgeNoiseAmount = 0.18f,
    backrunBandStart = 0.8f,
    backrunBoost = 0.06f,
    bloomChance = 0.05f,
    bloomMin = 0.02f,
    bloomMax = 0.05f,
    pigmentMix = false,
};

// Watercolor 2: heavier pigment-mixing wash. Overlapping colors multiply together
// (blue over yellow reads as green) instead of just tinting, with stronger grain,
// a more irregular edge, and a visible darker backrun ring at the wash boundary.
private static readonly WatercolorParams WatercolorPresetV2 = new WatercolorParams
{
    spreadPad = 9,
    maxStrength = 0.24f,
    strokeCap = 0.7f,
    holdMaxCap = 0.95f,
    holdSaturateSeconds = 1.1f,
    holdSpreadRate = 4f,
    grainScale = 0.08f,
    grainMin = 0.55f,
    grainMax = 1f,
    opacityJitterMin = 0.65f,
    opacityJitterMax = 1.3f,
    edgeNoiseAmount = 0.32f,
    backrunBandStart = 0.62f,
    backrunBoost = 0.16f,
    bloomChance = 0.1f,
    bloomMin = 0.02f,
    bloomMax = 0.08f,
    pigmentMix = true,
};

void PaintWatercolor(int centerX, int centerY)
{
    PaintWatercolorDab(centerX, centerY, WatercolorPresetV1);
}

void PaintWatercolor2(int centerX, int centerY)
{
    PaintWatercolorDab(centerX, centerY, WatercolorPresetV2);
}

void PaintWatercolorDab(int centerX, int centerY, WatercolorParams p)
{
    bool useStamp = brushStampTexture != null;

    // held-still bonus: grows the wash's reach the longer the pointer sits in place
    int holdSpreadBonus = Mathf.RoundToInt(stationaryHoldTime * p.holdSpreadRate);
    int spread = brushSize + p.spreadPad + holdSpreadBonus;

    // per-dab random opacity jitter so repeated strokes don't look uniform
    float dabJitter = UnityEngine.Random.Range(p.opacityJitterMin, p.opacityJitterMax);

    for (int i = -spread; i < spread; i++)
    {
        for (int j = -spread; j < spread; j++)
        {
            int px = centerX + i;
            int py = centerY + j;

            if (px < 0 || px >= textureSize || py < 0 || py >= textureSize)
            {
                continue;
            }

            float dist = Mathf.Sqrt(i * i + j * j);
            int idx = py * textureSize + px;
            float strength;

            if (useStamp)
            {
                if (dist > spread)
                {
                    continue;
                }

                // sample the stamp image directly for shape + opacity instead of the
                // procedural circle - white/bright = strong, black/dark = nothing
                float u = (i / (float)spread) * 0.5f + 0.5f;
                float v = (j / (float)spread) * 0.5f + 0.5f;
                float mask = brushStampTexture.GetPixelBilinear(u, v).grayscale;
                if (mask <= 0.02f)
                {
                    continue;
                }

                float grain = Mathf.PerlinNoise(
                    (centerX + px) * p.grainScale + strokeGrainOffset.x,
                    (centerY + py) * p.grainScale + strokeGrainOffset.y);
                grain = Mathf.Lerp(p.grainMin, p.grainMax, grain);

                strength = mask * grain * p.maxStrength * dabJitter;
            }
            else
            {
                // perlin noise keyed by angle warps the boundary so it isn't a perfect circle
                float angle = Mathf.Atan2(j, i);
                float edgeNoise = Mathf.PerlinNoise(
                    strokeGrainOffset.x + Mathf.Cos(angle) * 2f,
                    strokeGrainOffset.y + Mathf.Sin(angle) * 2f);
                float effectiveRadius = brushSize * (1f - p.edgeNoiseAmount * 0.5f + edgeNoise * p.edgeNoiseAmount);

                if (dist <= effectiveRadius)
                {
                    float falloff = 1f - Mathf.Clamp01(dist / effectiveRadius);
                    float smoothFalloff = falloff * falloff * (3f - 2f * falloff);

                    // coherent (perlin) paper-grain noise instead of white noise, so pigment
                    // settles in organic mottled patches rather than a smooth gradient
                    float grain = Mathf.PerlinNoise(
                        (centerX + px) * p.grainScale + strokeGrainOffset.x,
                        (centerY + py) * p.grainScale + strokeGrainOffset.y);
                    grain = Mathf.Lerp(p.grainMin, p.grainMax, grain);

                    strength = smoothFalloff * grain * p.maxStrength * dabJitter;

                    // backrun/pooling: pigment concentrates near the outer edge of the wash
                    float edgeFraction = dist / effectiveRadius;
                    if (edgeFraction >= p.backrunBandStart)
                    {
                        float bandT = Mathf.InverseLerp(p.backrunBandStart, 1f, edgeFraction);
                        strength += p.backrunBoost * bandT;
                    }
                }
                else if (dist <= spread && UnityEngine.Random.value < p.bloomChance)
                {
                    strength = UnityEngine.Random.Range(p.bloomMin, p.bloomMax) * dabJitter;
                }
                else
                {
                    strength = 0f;
                }
            }

            if (strength > 0f)
            {
                ApplyWatercolorTexel(idx, px, py, strength, p);
            }
        }
    }
    drawTexture.Apply();
}

void ApplyWatercolorTexel(int idx, int px, int py, float strength, WatercolorParams p)
{
    // clamp this texel's total build-up for the CURRENT stroke, so dragging back and
    // forth doesn't race the wash to full opacity - but holding the brush roughly still
    // lets the cap itself creep up toward holdMaxCap, like pigment soaking in over time
    float holdT = Mathf.Clamp01(stationaryHoldTime / p.holdSaturateSeconds);
    float effectiveCap = Mathf.Lerp(p.strokeCap, p.holdMaxCap, holdT);

    float already = strokeCoverage[idx];
    float appliedStrength = Mathf.Min(strength, Mathf.Max(0f, effectiveCap - already));
    if (appliedStrength <= 0.0001f)
    {
        return;
    }
    strokeCoverage[idx] = already + appliedStrength;

    Color existing = drawTexture.GetPixel(px, py);

    Color newColor;
    if (p.pigmentMix && existing.a > 0.05f)
    {
        // subtractive-style pigment mixing when painting over existing color - this is
        // what makes the underlying color show through as a mix rather than a flat tint
        newColor = new Color(existing.r * brushColor.r, existing.g * brushColor.g, existing.b * brushColor.b, 1f);
    }
    else
    {
        newColor = brushColor;
    }

    // proper "over" compositing for straight (non-premultiplied) alpha. A plain RGB lerp
    // toward existing.rgb would blend toward (0,0,0) on a still-transparent pixel (Color.clear
    // is black), which is what made early low-opacity dabs look grey/muddy instead of true-color.
    float oldAlpha = existing.a;
    float newAlpha = appliedStrength + oldAlpha * (1f - appliedStrength);
    float r, g, b;
    if (newAlpha > 0.0001f)
    {
        r = (newColor.r * appliedStrength + existing.r * oldAlpha * (1f - appliedStrength)) / newAlpha;
        g = (newColor.g * appliedStrength + existing.g * oldAlpha * (1f - appliedStrength)) / newAlpha;
        b = (newColor.b * appliedStrength + existing.b * oldAlpha * (1f - appliedStrength)) / newAlpha;
    }
    else
    {
        r = newColor.r;
        g = newColor.g;
        b = newColor.b;
    }

    drawTexture.SetPixel(px, py, new Color(r, g, b, newAlpha));
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

