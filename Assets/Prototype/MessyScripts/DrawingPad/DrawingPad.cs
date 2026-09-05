using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using UnityEngine.EventSystems;

public class DrawingPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public AudioSource audioSource;
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
        GelGlitter,
    }

    private DrawingTool currentDrawingTool = DrawingTool.Brush;
    private BrushStyle currentBrushStyle = BrushStyle.Big;
    private int brushSize = 4;
    public int textureSize = 256;
    // Glitter Settings
    public float glitterDensity = 0.14f; // Range(0.01f, 0.4f)
    public float glitterShininess = 35f; // Range(5f, 120f)
    public Color glitterGlintColor = Color.white;
    public Texture2D glitterTexture;
    public float glitterTextureTiling = 4.0f;
    private Texture2D drawTexture;
    private RawImage rawImage;
    private RectTransform rectTransform;
    private Vector2? lastLocalPoint;
    private Color brushColor = Color.black; // starting brush color
    private bool isDrawing = false;
    private Camera pressCamera;
    // Brush Audio Settings
    public AudioClip[] bigBrushSounds = new AudioClip[4];
    public AudioClip[] mediumBrushSounds = new AudioClip[4];
    public AudioClip[] smallBrushSounds = new AudioClip[4];
    public AudioClip[] watercolorSounds = new AudioClip[4];
    public AudioClip[] watercolor2Sounds = new AudioClip[4];
    public AudioClip[] gelGlitterSounds = new AudioClip[4];
    public AudioClip[] eraserSounds = new AudioClip[4];
    private int bigBrushSoundIndex = -1;
    private int mediumBrushSoundIndex = -1;
    private int smallBrushSoundIndex = -1;
    private int watercolorSoundIndex = -1;
    private int watercolor2SoundIndex = -1;
    private int gelGlitterSoundIndex = -1;
    private int eraserSoundIndex = -1;
    private float soundCooldownTimer = 0f;
    private const float soundCooldownDuration = .61f; // Cooldown limits rapid fire overlap noise


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

    // Current stroke direction (radians), used to orient the stamp texture. Only updated
    // when the pointer actually moves, so it stays put while held still instead of
    // rerolling every frame (which is what made a held stamp flicker between orientations).
    private float strokeDirectionAngle = 0f;

    // How many watercolor dabs have been laid down so far in the CURRENT stroke - a proxy
    // for "how far the brush has dragged," used to simulate a loaded brush running dry:
    // strong at the start of a stroke, feathering/patching out toward the tail.
    private int strokeDabIndex = 0;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        InitializeSoundArrays();
    }

    private void InitializeSoundArrays()
    {
        if (bigBrushSounds == null || bigBrushSounds.Length != 4) bigBrushSounds = new AudioClip[4];
        if (mediumBrushSounds == null || mediumBrushSounds.Length != 4) mediumBrushSounds = new AudioClip[4];
        if (smallBrushSounds == null || smallBrushSounds.Length != 4) smallBrushSounds = new AudioClip[4];
        if (watercolorSounds == null || watercolorSounds.Length != 4) watercolorSounds = new AudioClip[4];
        if (watercolor2Sounds == null || watercolor2Sounds.Length != 4) watercolor2Sounds = new AudioClip[4];
        if (gelGlitterSounds == null || gelGlitterSounds.Length != 4) gelGlitterSounds = new AudioClip[4];
        if (eraserSounds == null || eraserSounds.Length != 4) eraserSounds = new AudioClip[4];
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Auto-load default sounds from Assets/Resources/Sound/ if they are not manually assigned in the Inspector
        LoadDefaultSounds();
    }

    private void LoadDefaultSounds()
    {
        for (int i = 0; i < 4; i++)
        {
            // If the slot in the Inspector is empty, look for the file in Assets/Resources/Sound/
            if (bigBrushSounds[i] == null) bigBrushSounds[i] = Resources.Load<AudioClip>($"Sound/Pencil{i}");
            if (mediumBrushSounds[i] == null) mediumBrushSounds[i] = Resources.Load<AudioClip>($"Sound/Pencil{i}");
            if (smallBrushSounds[i] == null) smallBrushSounds[i] = Resources.Load<AudioClip>($"Sound/Pencil{i}");

            if (watercolorSounds[i] == null) watercolorSounds[i] = Resources.Load<AudioClip>($"Sound/Paint{i}");
            if (watercolor2Sounds[i] == null) watercolor2Sounds[i] = Resources.Load<AudioClip>($"Sound/Paint{i}");

            if (gelGlitterSounds[i] == null) gelGlitterSounds[i] = Resources.Load<AudioClip>($"Sound/GelGlitter{i}");
            if (eraserSounds[i] == null) eraserSounds[i] = Resources.Load<AudioClip>($"Sound/Eraser{i}");
        }
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
        strokeDirectionAngle = 0f;
        strokeDabIndex = 0;
        PlayDrawingSound();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDrawing = false;
        lastLocalPoint = null;
    }

    // Update is called once per frame
    void Update()
    {
        if (soundCooldownTimer > 0f)
        {
            soundCooldownTimer -= Time.deltaTime;
        }
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

                // Play a sound cycle if the cursor has dragged far enough
                if (moved > 0.5f)
                {
                    PlayDrawingSound();
                }
            }
            else
            {
                PaintAtLocalPoint(localPoint, rectTransform);
                PlayDrawingSound(); // Play sound if we single click tap
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
        Vector2 direction = to - from;
        float distance = direction.magnitude;

        // only update the stamp's orientation when there's an actual direction to take -
        // this is what keeps a held-still dab's orientation stable instead of rerolling
        if (distance > 0.001f)
        {
            strokeDirectionAngle = Mathf.Atan2(direction.y, direction.x);
        }

        // watercolor brushes space dabs out (instead of stamping ~every pixel) so a fast
        // drag doesn't cram dozens of near-identical overlapping stamps into a straight
        // streak - see PaintWatercolorDab for the per-dab opacity/color variation
        float spacing = GetDabSpacingLocalUnits(rt);
        int steps = spacing > 0f ? Mathf.CeilToInt(distance / spacing) : Mathf.CeilToInt(distance);

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

    float GetDabSpacingLocalUnits(RectTransform rt)
    {
        float factor;
        if (currentDrawingTool != DrawingTool.Brush)
        {
            return 0f; // eraser: dense, no spacing
        }

        switch (currentBrushStyle)
        {
            case BrushStyle.Watercolor:
                factor = WatercolorPresetV1.dabSpacingFactor;
                break;
            case BrushStyle.Watercolor2:
                factor = WatercolorPresetV2.dabSpacingFactor;
                break;
            default:
                return 0f; // hard brush styles: dense, no spacing
        }

        float texelsToLocalUnits = rt.rect.width / textureSize;
        return Mathf.Max(1f, brushSize * factor) * texelsToLocalUnits;
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
            else if (currentBrushStyle == BrushStyle.GelGlitter)
            {
                SetBrushSize(8);
                PaintGelGlitter(x, y);
            }
            break;
        case DrawingTool.Eraser:
                PaintEraser(x, y, localPoint);
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

    void PaintEraser(int centerX, int centerY, Vector2 localPoint)
    {
        Color erasedColor = Color.clear;
        bool foundColor = false;

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
                        // Sample the color before erasing it
                        if (!foundColor)
                        {
                            Color c = drawTexture.GetPixel(px, py);
                            if (c.a > 0.15f)
                            {
                                erasedColor = c;
                                foundColor = true;
                            }
                        }
                        drawTexture.SetPixel(px, py, Color.clear);
                    }
                }
            }
        }
        drawTexture.Apply();

        // Only spawn crumbs if we actually erased some paint!
        if (foundColor)
        {
            // This calls our spawner and passes both required parameters
            SpawnEraserCrumbsLocal(localPoint, erasedColor);
        }
    }

    void PaintGelGlitter(int centerX, int centerY)
    {
        PaintGelGlitterDab(centerX, centerY, GelGlitterPreset);
    }

    void PaintGelGlitterDab(int centerX, int centerY, GelGlitterParams p)
    {
        if (drawTexture == null) return;

        Color baseColor = brushColor;

        // Fast, self-contained coordinate-independent local hash function
        float GetLocalHash(int x, int y, int seed)
        {
            float val = Mathf.Sin(x * 12.9898f + y * 78.233f + seed * 37.123f) * 43758.5453f;
            return Mathf.Abs(val - Mathf.Floor(val));
        }

        // ==========================================================
        // DYNAMIC COLOR-SEEDING (Solves the Tinting/Layering Issue)
        // ==========================================================
        // Derive a unique spatial offset based on the active color. 
        // Different colors will now generate completely independent ripple waves and glitter layouts!
        float colorOffsetF = (baseColor.r * 127.1f + baseColor.g * 311.7f + baseColor.b * 74.2f);
        float colorSeedX = Mathf.Repeat(colorOffsetF * 43.13f, 500f);
        float colorSeedY = Mathf.Repeat(colorOffsetF * 119.23f, 500f);

        // 3D Lighting setup (virtual light from top-left-front)
        Vector3 lightDir = new Vector3(-0.4f, 0.4f, 0.82f).normalized;
        Vector3 viewDir = new Vector3(0.0f, 0.0f, 1.0f);
        Vector3 halfDir = (lightDir + viewDir).normalized;

        float time = Time.time * p.shimmerSpeed;

        // Tighter cell size (4.0f instead of 6.0f) packs the glitter closer together for a dense coat
        float cellSize = 4.0f;

        for (int i = -brushSize; i < brushSize; i++)
        {
            for (int j = -brushSize; j < brushSize; j++)
            {
                float distSq = i * i + j * j;
                float rMax = brushSize;
                if (distSq >= rMax * rMax) continue;

                int px = centerX + i;
                int py = centerY + j;

                // Canvas bounds safety check
                if (px < 0 || px >= drawTexture.width || py < 0 || py >= drawTexture.height) continue;

                float dist = Mathf.Sqrt(distSq);
                float normDist = dist / rMax;

                // Retrieve the existing pixel color on the canvas
                Color existingColor = drawTexture.GetPixel(px, py);

                // ==========================================================
                // COLOR-SIMILARITY THRESHOLD GUARD
                // ==========================================================
                float colorDistSq =
                    (existingColor.r - baseColor.r) * (existingColor.r - baseColor.r) +
                    (existingColor.g - baseColor.g) * (existingColor.g - baseColor.g) +
                    (existingColor.b - baseColor.b) * (existingColor.b - baseColor.b);

                bool isInsideStroke = (colorDistSq < 0.05f);

                float profile = 1.0f;
                float edgeShadow = 1.0f;

                if (isInsideStroke)
                {
                    profile = 1.0f;
                    edgeShadow = 1.0f;
                }
                else
                {
                    if (normDist <= 0.82f)
                    {
                        profile = 1.0f;
                        edgeShadow = 1.0f;
                    }
                    else
                    {
                        profile = (1.0f - normDist) / 0.18f;
                        edgeShadow = 0.65f + 0.35f * profile;
                    }
                }

                // ==========================================================
                // DYNAMIC COLOR-SEEDED BUMP RIPPLES
                // ==========================================================
                float rippleScale = 0.08f;
                float rx = px + colorSeedX;
                float ry = py + colorSeedY;
                float h0_ripple = Mathf.PerlinNoise(rx * rippleScale, ry * rippleScale);
                float h1_ripple = Mathf.PerlinNoise((rx + 1) * rippleScale, ry * rippleScale);
                float h2_ripple = Mathf.PerlinNoise(rx * rippleScale, (ry + 1) * rippleScale);

                float nx = (h0_ripple - h1_ripple) * 0.35f;
                float ny = (h0_ripple - h2_ripple) * 0.35f;

                if (!isInsideStroke && normDist > 0.82f)
                {
                    float edgeF = (normDist - 0.82f) / 0.18f;
                    nx += (i / rMax) * edgeF * 1.5f;
                    ny += (j / rMax) * edgeF * 1.5f;
                }

                Vector3 gelNormal = new Vector3(nx, ny, 1.0f).normalized;

                // Ambient shading
                float ndotl = Vector3.Dot(gelNormal, lightDir);
                float diffuse = Mathf.Lerp(0.85f, 1.0f, Mathf.Max(0.0f, ndotl));
                Color shadedGel = baseColor * (diffuse * edgeShadow);

                // Specular gloss
                float specular = Mathf.Pow(Mathf.Max(0.0f, Vector3.Dot(gelNormal, halfDir)), p.glossiness);
                Color specularHighlightColor = Color.Lerp(baseColor, Color.white, 0.4f) * specular * 0.35f;
                shadedGel += specularHighlightColor;

                shadedGel.a = p.gelOpacity * profile;

                Color finalPixelColor = shadedGel;

                // ==========================================================
                // DYNAMIC COLOR-SEEDED CHUNKY GLITTER SPECKS
                // ==========================================================
                int cellX = Mathf.FloorToInt((px + colorSeedX) / cellSize);
                int cellY = Mathf.FloorToInt((py + colorSeedY) / cellSize);

                bool isOverFlake = false;
                Color flakeColorValue = Color.clear;
                float maxGlint = 0.0f;

                // Higher density multiplier to pack flakes tighter
                float targetDensity = p.glitterDensity * 1.6f;

                for (int cx = -1; cx <= 1; cx++)
                {
                    int nx_cell = cellX + cx;
                    for (int cy = -1; cy <= 1; cy++)
                    {
                        int ny_cell = cellY + cy;

                        float h0 = GetLocalHash(nx_cell, ny_cell, 1);
                        if (h0 > targetDensity) continue;

                        float h1 = GetLocalHash(nx_cell, ny_cell, 2);
                        float h2 = GetLocalHash(nx_cell, ny_cell, 3);

                        // Position flake based on cell coordinates + seed offset
                        float flakeCenterX = (nx_cell + h1) * cellSize;
                        float flakeCenterY = (ny_cell + h2) * cellSize;

                        float dx = (px + colorSeedX) - flakeCenterX;
                        float dy = (py + colorSeedY) - flakeCenterY;
                        float distToFlake = Mathf.Sqrt(dx * dx + dy * dy);

                        // Core flake bounds (1.0 to 2.2 pixels for visible chunky particles)
                        float flakeSize = Mathf.Lerp(1.0f, 2.2f, GetLocalHash(nx_cell, ny_cell, 4));
                        float twinklePhase = time + h0 * Mathf.PI * 2.0f;
                        float twinkle = 0.4f + 0.6f * Mathf.Sin(twinklePhase);

                        if (distToFlake <= flakeSize)
                        {
                            isOverFlake = true;

                            float h_hsv, s_hsv, v_hsv;
                            Color.RGBToHSV(baseColor, out h_hsv, out s_hsv, out v_hsv);

                            h_hsv = Mathf.Repeat(h_hsv + (GetLocalHash(nx_cell, ny_cell, 5) - 0.5f) * p.holographicShift, 1.0f);
                            s_hsv = Mathf.Clamp01(s_hsv * 1.3f);
                            v_hsv = Mathf.Clamp01(v_hsv * 1.4f);
                            Color flakeBaseColor = Color.HSVToRGB(h_hsv, s_hsv, v_hsv);

                            float facetAngle = (GetLocalHash(nx_cell, ny_cell, 6) * Mathf.PI * 2.0f) + time;
                            Vector3 facetNormal = new Vector3(Mathf.Cos(facetAngle), Mathf.Sin(facetAngle), 0.5f).normalized;

                            float flakeSpec = Mathf.Pow(Mathf.Max(0.0f, Vector3.Dot(facetNormal, halfDir)), p.glitterShininess);

                            flakeColorValue = Color.Lerp(flakeBaseColor * 1.3f, Color.white, flakeSpec * twinkle);
                            flakeColorValue.a = 1.0f;
                        }

                        // Sharp 4-Pointed Specular Sparkle Glint
                        float flareLength = Mathf.Lerp(2.5f, 6.0f, GetLocalHash(nx_cell, ny_cell, 7)) * twinkle;
                        float axisDistX = Mathf.Abs(dx);
                        float axisDistY = Mathf.Abs(dy);
                        float flareThickness = 0.75f;

                        float glintBrightness = 0.0f;
                        if (axisDistX < flareThickness && Mathf.Abs(dy) < flareLength)
                        {
                            glintBrightness = (1.0f - (Mathf.Abs(dy) / flareLength)) * (1.0f - (axisDistX / flareThickness));
                        }
                        else if (axisDistY < flareThickness && Mathf.Abs(dx) < flareLength)
                        {
                            glintBrightness = (1.0f - (Mathf.Abs(dx) / flareLength)) * (1.0f - (axisDistY / flareThickness));
                        }

                        if (glintBrightness > maxGlint)
                        {
                            maxGlint = glintBrightness;
                        }
                    }
                }

                // Render the solid chunky flake
                if (isOverFlake)
                {
                    finalPixelColor = Color.Lerp(shadedGel, flakeColorValue, 0.95f);
                }
                // --- Dynamic Glitter Texture Overlay ---
                if (glitterTexture != null)
                {
                    // Calculate tiled UV coordinates
                    float texU = (float)px / textureSize * glitterTextureTiling;
                    float texV = (float)py / textureSize * glitterTextureTiling;

                    // Shift coordinates slightly over time to simulate reflecting light
                    float animationSpeed = p.shimmerSpeed * 0.4f;
                    float offsetX = Mathf.Sin(Time.time * animationSpeed + py * 0.12f) * 0.015f;
                    float offsetY = Mathf.Cos(Time.time * animationSpeed + px * 0.12f) * 0.015f;

                    // Sample the texture and calculate dynamic sparkle twinkle
                    Color texColor = glitterTexture.GetPixelBilinear(Mathf.Repeat(texU + offsetX, 1.0f), Mathf.Repeat(texV + offsetY, 1.0f));
                    float texBrightness = (texColor.r + texColor.g + texColor.b) / 3.0f;
                    float twinkle = 0.4f + 0.6f * Mathf.Sin(Time.time * p.shimmerSpeed + (px * 0.2f) + (py * 0.2f));
                    float textureGlitterStrength = texBrightness * texColor.a * twinkle * p.glitterDensity * 2.2f;

                    if (textureGlitterStrength > 0.01f)
                    {
                        Color textureGlitterColor = Color.Lerp(baseColor, Color.white, 0.7f) * textureGlitterStrength;

                        // Screen blend the texture glitter over the brush stroke
                        finalPixelColor.r += textureGlitterColor.r * (1.0f - finalPixelColor.r);
                        finalPixelColor.g += textureGlitterColor.g * (1.0f - finalPixelColor.g);
                        finalPixelColor.b += textureGlitterColor.b * (1.0f - finalPixelColor.b);
                        finalPixelColor.a = Mathf.Max(finalPixelColor.a, textureGlitterStrength);
                    }
                }
                // Screen blend the brilliant flares over the paint surface
                if (maxGlint > 0.0f)
                {
                    Color flareColor = Color.white * maxGlint;
                    finalPixelColor.r += flareColor.r * (1.0f - finalPixelColor.r);
                    finalPixelColor.g += flareColor.g * (1.0f - finalPixelColor.g);
                    finalPixelColor.b += flareColor.b * (1.0f - finalPixelColor.b);
                    finalPixelColor.a = Mathf.Max(finalPixelColor.a, maxGlint);
                }

                // Alpha-blend the final pixel over the existing canvas pixel
                Color blendedColor = Color.Lerp(existingColor, finalPixelColor, finalPixelColor.a);
                drawTexture.SetPixel(px, py, blendedColor);
            }
        }
        drawTexture.Apply();
    }

    float ShaderHash(int x, int y, int seed)
    {
        int n = x + y * 37 + seed * 101;
        n = (n << 13) ^ n;
        return (float)((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647.0f;
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
    public float dabSpacingFactor; // min distance between dabs along a drag, as a fraction of brushSize
    public float hueJitter;        // color dynamics: per-dab random HSV drift off brushColor
    public float saturationJitter;
    public float brightnessJitter;
    public float smudgeAmount;   // 0-1: how much existing canvas color gets dragged forward per dab
    public float smudgeDistance; // texels behind the dab to sample the dragged color from
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
    dabSpacingFactor = 0.28f,
    hueJitter = 0.015f,
    saturationJitter = 0.08f,
    brightnessJitter = 0.08f,
    smudgeAmount = 0.45f,
    smudgeDistance = 5f,
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
    dabSpacingFactor = 0.25f,
    hueJitter = 0.03f,
    saturationJitter = 0.15f,
    brightnessJitter = 0.15f,
    smudgeAmount = 0.55f,
    smudgeDistance = 7f,
};

[System.Serializable]
    public struct GelGlitterParams 
    {
        public float gelOpacity;            // Max opacity of the gel center (0.0 to 1.0)
        public float glossiness;            // Specular highlight exponent (wetness)
        public float edgeSoftness;          // Width of the curved edge border (0.05 to 0.3)
        public float glitterDensity;        // Density of glitter particles (0.0 to 1.0)
        public float glitterShininess;      // Sharpness of glitter sparkles
        public float shimmerSpeed;          // Twinkle speed
        public float holographicShift;      // Holographic rainbow range (0.0 to 0.3)
    }
    private static readonly GelGlitterParams GelGlitterPreset = new GelGlitterParams
    {
        gelOpacity = 0.95f,         // Opaque gel core to preserve rich paint pigment
        glossiness = 40.0f,         // Rich, glassy glaze wetness
        edgeSoftness = 0.18f,       // Tight boundary edge rounding
        glitterDensity = 0.08f,     // 8% metallic glitter flake coverage
        glitterShininess = 30.0f,   // Pinpoint intense specular sparkle
        shimmerSpeed = 3.8f,        // Smooth real-time twinkling speed
        holographicShift = 0.12f    // Shifts flake color slightly around the brush color for deep luster
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
    strokeDabIndex++;

    // held-still bonus: grows the wash's reach the longer the pointer sits in place
    int holdSpreadBonus = Mathf.RoundToInt(stationaryHoldTime * p.holdSpreadRate);
    int spread = brushSize + p.spreadPad + holdSpreadBonus;

    // per-dab random opacity jitter so repeated strokes don't look uniform
    float dabJitter = UnityEngine.Random.Range(p.opacityJitterMin, p.opacityJitterMax);

    // no established drag direction yet on the very first dab of a stroke - skip the
    // smudge rather than smearing in an arbitrary default direction
    float effectiveSmudgeAmount = strokeDabIndex <= 1 ? 0f : p.smudgeAmount;

    // color dynamics: a slightly different tint per dab instead of flat brushColor
    Color dabColor = ApplyColorDynamics(brushColor, p);

    // orient the stamp to the direction of travel. This only changes when strokeDirectionAngle
    // changes (i.e. the pointer actually moved), so a held-still dab stays visually stable
    // instead of rerolling every frame.
    float dabCos = Mathf.Cos(strokeDirectionAngle);
    float dabSin = Mathf.Sin(strokeDirectionAngle);

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
                // procedural circle - white/bright = strong, black/dark = nothing.
                // rotated to the stroke direction so it "drags" naturally instead of
                // sampling the texture identically regardless of where the stroke is going
                float ri = i * dabCos - j * dabSin;
                float rj = i * dabSin + j * dabCos;
                float u = (ri / spread) * 0.5f + 0.5f;
                float v = (rj / spread) * 0.5f + 0.5f;
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
                ApplyWatercolorTexel(idx, px, py, strength, p, dabColor, dabCos, dabSin, effectiveSmudgeAmount);
            }
        }
    }
    drawTexture.Apply();
}

// Small random HSV drift off the base brush color, so consecutive dabs aren't
// perfectly flat/identical in color (Procreate calls this "Color Dynamics").
Color ApplyColorDynamics(Color baseColor, WatercolorParams p)
{
    float h, s, v;
    Color.RGBToHSV(baseColor, out h, out s, out v);
    h = Mathf.Repeat(h + UnityEngine.Random.Range(-p.hueJitter, p.hueJitter), 1f);
    s = Mathf.Clamp01(s + UnityEngine.Random.Range(-p.saturationJitter, p.saturationJitter));
    v = Mathf.Clamp01(v + UnityEngine.Random.Range(-p.brightnessJitter, p.brightnessJitter));
    Color result = Color.HSVToRGB(h, s, v);
    result.a = baseColor.a;
    return result;
}

void ApplyWatercolorTexel(int idx, int px, int py, float strength, WatercolorParams p, Color dabColor, float dabCos, float dabSin, float smudgeAmount)
{
    // clamp this texel's total build-up for the CURRENT stroke, so dragging back and
    // forth doesn't race the wash to full opacity - but holding the brush roughly still
    // lets the cap itself creep up toward holdMaxCap, like pigment soaking in over time.
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

    // smudge: drag a bit of whatever color the brush just passed over forward with it,
    // like a wet brush smearing paint instead of stamping the same flat color every time
    Color paintColor = dabColor;
    if (smudgeAmount > 0f)
    {
        int sx = Mathf.Clamp(px - Mathf.RoundToInt(dabCos * p.smudgeDistance), 0, textureSize - 1);
        int sy = Mathf.Clamp(py - Mathf.RoundToInt(dabSin * p.smudgeDistance), 0, textureSize - 1);
        Color smudgeSource = drawTexture.GetPixel(sx, sy);
        if (smudgeSource.a > 0.05f)
        {
            paintColor = Color.Lerp(paintColor, new Color(smudgeSource.r, smudgeSource.g, smudgeSource.b, paintColor.a), smudgeAmount);
        }
    }

    Color newColor;
    if (p.pigmentMix && existing.a > 0.05f)
    {
            // subtractive-style pigment mixing when painting over existing color - this is
            // what makes the underlying color show through as a mix rather than a flat tint
            // Reconstruct the background color against white paper so it doesn't default to black multiplication
            Color paperColor = Color.Lerp(Color.white, existing, existing.a);
            newColor = new Color(paperColor.r * paintColor.r, paperColor.g * paintColor.g, paperColor.b * paintColor.b, 1.0f);
        }
    else
    {
        newColor = paintColor;
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

    public float GetCoveragePercentage()
    {
        Color[] pixels = drawTexture.GetPixels();
        int nonTransparent = 0;
        int total = pixels.Length; // same as textureSize * textureSize

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a > 0.01f)
            {
                nonTransparent++;
            }
        }

        return (float)nonTransparent / total;
    }
    void PlayDrawingSound()
    {
        if (audioSource == null) return;
        if (soundCooldownTimer > 0f) return;

        AudioClip[] activeClips = null;
        int currentIndex = 0;

        if (currentDrawingTool == DrawingTool.Eraser)
        {
            activeClips = eraserSounds;
            if (activeClips != null && activeClips.Length > 0)
            {
                currentIndex = GetNonRepeatingRandomIndex(ref eraserSoundIndex, activeClips.Length);
            }
        }
        else
        {
            switch (currentBrushStyle)
            {
                case BrushStyle.Big:
                    activeClips = bigBrushSounds;
                    if (activeClips != null && activeClips.Length > 0)
                    {
                        currentIndex = GetNonRepeatingRandomIndex(ref bigBrushSoundIndex, activeClips.Length);
                    }
                    break;
                case BrushStyle.Medium:
                    activeClips = mediumBrushSounds;
                    if (activeClips != null && activeClips.Length > 0)
                    {
                        currentIndex = GetNonRepeatingRandomIndex(ref mediumBrushSoundIndex, activeClips.Length);
                    }
                    break;
                case BrushStyle.Small:
                    activeClips = smallBrushSounds;
                    if (activeClips != null && activeClips.Length > 0)
                    {
                        currentIndex = GetNonRepeatingRandomIndex(ref smallBrushSoundIndex, activeClips.Length);
                    }
                    break;
                case BrushStyle.Watercolor:
                    activeClips = watercolorSounds;
                    if (activeClips != null && activeClips.Length > 0)
                    {
                        currentIndex = GetNonRepeatingRandomIndex(ref watercolorSoundIndex, activeClips.Length);
                    }
                    break;
                case BrushStyle.Watercolor2:
                    activeClips = watercolor2Sounds;
                    if (activeClips != null && activeClips.Length > 0)
                    {
                        currentIndex = GetNonRepeatingRandomIndex(ref watercolor2SoundIndex, activeClips.Length);
                    }
                    break;
                case BrushStyle.GelGlitter:
                    activeClips = gelGlitterSounds;
                    if (activeClips != null && activeClips.Length > 0)
                    {
                        currentIndex = GetNonRepeatingRandomIndex(ref gelGlitterSoundIndex, activeClips.Length);
                    }
                    break;
            }
        }

        if (activeClips != null && activeClips.Length > 0)
        {
            AudioClip clip = activeClips[currentIndex % activeClips.Length];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
                soundCooldownTimer = soundCooldownDuration; // Cooldown resets!
            }
        }
    }
    private int GetNonRepeatingRandomIndex(ref int lastIndex, int arrayLength)
    {
        if (arrayLength <= 1) return 0;

        int nextIndex = lastIndex;
        // Keep picking a random number until it doesn't match the last played one
        while (nextIndex == lastIndex)
        {
            nextIndex = UnityEngine.Random.Range(0, arrayLength);
        }

        lastIndex = nextIndex; // Remember this index for next time!
        return nextIndex;
    }
    private void SpawnEraserCrumbsLocal(Vector2 localPoint, Color erasedColor)
    {
        int count = UnityEngine.Random.Range(1, 3);
        for (int i = 0; i < count; i++)
        {
            GameObject crumbObj = new GameObject("EraserCrumb", typeof(RectTransform), typeof(Image), typeof(EraserCrumb));
            crumbObj.transform.SetParent(transform, false);

            RectTransform crumbRect = crumbObj.GetComponent<RectTransform>();
            Vector2 randomOffset = new Vector2(UnityEngine.Random.Range(-12f, 12f), UnityEngine.Random.Range(-12f, 12f));
            crumbRect.anchoredPosition = localPoint + randomOffset;

            float size = UnityEngine.Random.Range(2f, 5f);
            crumbRect.sizeDelta = new Vector2(size, size);

            Image crumbImage = crumbObj.GetComponent<Image>();

            // Mix the paint color with a little white to look like real rubber eraser dust!
            Color mixedColor = Color.Lerp(erasedColor, Color.white, UnityEngine.Random.Range(0.15f, 0.35f));
            mixedColor.a = 0.95f; // Solid start

            crumbImage.color = mixedColor;
        }
    }
}

