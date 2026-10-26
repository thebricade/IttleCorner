using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Full-screen color mixer. One half is a paint pool to mix in, the other half is the target color
// with the player's current mix shown inside it. Builds its own UI at runtime so no scene setup is needed.
public class ColorMixerScreen : MonoBehaviour
{
    public static ColorMixerScreen Instance;

    [Tooltip("Colors the player can pour. Keep them paint-like, not pure RGB, so mixes read naturally.")]
    public Color[] paints =
    {
        new Color(0.85f, 0.12f, 0.15f), // red
        new Color(0.98f, 0.85f, 0.10f), // yellow
        new Color(0.10f, 0.30f, 0.80f), // blue
        new Color(0.97f, 0.97f, 0.97f), // white
        new Color(0.08f, 0.08f, 0.08f), // black
    };

    [Tooltip("Delta E under this counts as a match.")]
    public float matchThreshold = 8f;
    [Tooltip("Delta E at or above this reads as 0% close.")]
    public float farDeltaE = 50f;

    // Fires with the player's mixed color once it matches the target.
    public Action<Color> onMatched;

    Canvas canvas;
    MixingCanvas mixing;
    Image targetBackground;
    Image yourColorCircle;
    RectTransform barFill;
    TMP_Text statusText;
    Image[] potImages;
    Image[] toolImages;
    RectTransform mixerHalf;
    RectTransform targetHalf;

    Color targetColor;
    Color yourColor;
    bool hasYourColor;
    bool finishing;
    static Sprite circleSprite;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
        canvas.gameObject.SetActive(false);
    }

    public void Open(Color target)
    {
        targetColor = target;
        hasYourColor = false;
        finishing = false;

        targetBackground.color = target;
        yourColorCircle.color = MixerPaper;
        mixing.Clear();
        SelectPaint(0);
        UpdateCloseness();

        LayoutForOrientation();
        GameModeManager.Instance.SetGameMode(GameMode.Mixing);
        canvas.gameObject.SetActive(true);
    }

    public void Close()
    {
        canvas.gameObject.SetActive(false);
        GameModeManager.Instance.SetGameMode(GameMode.Explore);
    }

    static Color MixerPaper => new Color(0.96f, 0.95f, 0.92f);

    // ---------- behaviour ----------

    void SelectPaint(int index)
    {
        mixing.tool = MixingCanvas.Tool.Pour;
        mixing.pourColor = paints[index];
        for (int i = 0; i < potImages.Length; i++)
            potImages[i].transform.localScale = Vector3.one * (i == index ? 1.2f : 1f);
        HighlightTool(0);
    }

    void SelectTool(MixingCanvas.Tool tool)
    {
        mixing.tool = tool;
        HighlightTool(tool == MixingCanvas.Tool.Swirl ? 1 : 2);
        if (tool != MixingCanvas.Tool.Pour)
            foreach (Image pot in potImages) pot.transform.localScale = Vector3.one;
    }

    void HighlightTool(int active)
    {
        for (int i = 0; i < toolImages.Length; i++)
            toolImages[i].color = i == active ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.45f);
    }

    void OnColorPicked(Color c)
    {
        yourColor = c;
        hasYourColor = true;
        yourColorCircle.color = c;
        UpdateCloseness();
    }

    void OnPickReleased()
    {
        if (hasYourColor && !finishing && PaintMix.DeltaE(yourColor, targetColor) <= matchThreshold)
            StartCoroutine(FinishAfterDelay());
    }

    IEnumerator FinishAfterDelay()
    {
        finishing = true;
        statusText.text = "Matched!";
        yield return new WaitForSeconds(1.2f);
        canvas.gameObject.SetActive(false);
        onMatched?.Invoke(yourColor);
    }

    void UpdateCloseness()
    {
        float closeness = 0f;
        if (hasYourColor)
        {
            float dE = PaintMix.DeltaE(yourColor, targetColor);
            closeness = Mathf.Clamp01(1f - dE / farDeltaE);
            if (dE <= matchThreshold) closeness = 1f;
        }

        barFill.anchorMax = new Vector2(closeness, 1f);

        if (finishing) return;
        if (!hasYourColor) statusText.text = "Pour and swirl to match the color";
        else if (closeness >= 1f) statusText.text = "Matched!";
        else if (closeness > 0.85f) statusText.text = "So close!";
        else if (closeness > 0.6f) statusText.text = "Getting warmer";
        else statusText.text = "Keep mixing";
    }

    // Landscape: mixer left, target right. Portrait: mixer bottom, target top.
    void LayoutForOrientation()
    {
        bool landscape = Screen.width >= Screen.height;
        if (landscape)
        {
            Anchor(mixerHalf, 0f, 0f, 0.5f, 1f);
            Anchor(targetHalf, 0.5f, 0f, 1f, 1f);
        }
        else
        {
            Anchor(mixerHalf, 0f, 0f, 1f, 0.5f);
            Anchor(targetHalf, 0f, 0.5f, 1f, 1f);
        }
    }

    // ---------- UI construction ----------

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("ColorMixerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasGO.GetComponent<RectTransform>();

        // Mixer half
        mixerHalf = NewRect("MixerHalf", root);
        Image mixerBg = mixerHalf.gameObject.AddComponent<Image>();
        mixerBg.color = new Color(0.30f, 0.26f, 0.24f);

        RectTransform canvasArea = NewRect("CanvasArea", mixerHalf);
        Anchor(canvasArea, 0f, 0.28f, 1f, 1f);
        canvasArea.offsetMin = new Vector2(24, 12);
        canvasArea.offsetMax = new Vector2(-24, -24);

        RectTransform poolRect = NewRect("MixingCanvas", canvasArea);
        Anchor(poolRect, 0f, 0f, 1f, 1f);
        poolRect.gameObject.AddComponent<RawImage>();
        AspectRatioFitter fitter = poolRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;
        mixing = poolRect.gameObject.AddComponent<MixingCanvas>();
        mixing.onColorPicked = OnColorPicked;
        mixing.onPickReleased = OnPickReleased;

        RectTransform controls = NewRect("Controls", mixerHalf);
        Anchor(controls, 0f, 0f, 1f, 0.28f);
        controls.offsetMin = new Vector2(16, 12);
        controls.offsetMax = new Vector2(-16, -6);

        RectTransform potRow = NewRow("PotRow", controls, 0f, 0.5f, 1f, 1f);
        potImages = new Image[paints.Length];
        for (int i = 0; i < paints.Length; i++)
        {
            int index = i;
            potImages[i] = NewCircleButton("Pot" + i, potRow, paints[i], () => SelectPaint(index)).image;
        }

        RectTransform toolRow = NewRow("ToolRow", controls, 0f, 0f, 1f, 0.5f);
        toolImages = new Image[3];
        toolImages[0] = NewTextButton("Pour", toolRow, "Pour", () => SelectPaint(IndexOfPour())).image;
        toolImages[1] = NewTextButton("Swirl", toolRow, "Swirl", () => SelectTool(MixingCanvas.Tool.Swirl)).image;
        toolImages[2] = NewTextButton("Pick", toolRow, "Pick", () => SelectTool(MixingCanvas.Tool.Pick)).image;
        NewTextButton("Clear", toolRow, "Clear", () => mixing.Clear());

        // Target half
        targetHalf = NewRect("TargetHalf", root);
        targetBackground = targetHalf.gameObject.AddComponent<Image>();

        RectTransform circleArea = NewRect("CircleArea", targetHalf);
        Anchor(circleArea, 0.2f, 0.25f, 0.8f, 0.8f);
        RectTransform ring = NewRect("Ring", circleArea);
        Anchor(ring, 0f, 0f, 1f, 1f);
        Image ringImage = ring.gameObject.AddComponent<Image>();
        ringImage.sprite = CircleSprite();
        ringImage.color = Color.white;
        AspectRatioFitter ringFit = ring.gameObject.AddComponent<AspectRatioFitter>();
        ringFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        ringFit.aspectRatio = 1f;

        RectTransform inner = NewRect("YourColor", ring);
        Anchor(inner, 0.04f, 0.04f, 0.96f, 0.96f);
        yourColorCircle = inner.gameObject.AddComponent<Image>();
        yourColorCircle.sprite = CircleSprite();

        TMP_Text title = NewText("Title", targetHalf, "Match this color!", 44);
        Anchor(title.rectTransform, 0.1f, 0.84f, 0.9f, 0.97f);
        title.outlineWidth = 0.25f;
        title.outlineColor = new Color32(0, 0, 0, 160);

        RectTransform bar = NewRect("Bar", targetHalf);
        Anchor(bar, 0.12f, 0.14f, 0.88f, 0.19f);
        bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
        barFill = NewRect("Fill", bar);
        Anchor(barFill, 0f, 0f, 0f, 1f);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        barFill.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);

        statusText = NewText("Status", targetHalf, "", 36);
        Anchor(statusText.rectTransform, 0.1f, 0.03f, 0.9f, 0.13f);
        statusText.outlineWidth = 0.25f;
        statusText.outlineColor = new Color32(0, 0, 0, 160);

        Button closeButton = NewTextButton("Close", targetHalf, "X", Close).button;
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-16, -16);
        closeRect.sizeDelta = new Vector2(80, 80);
        Destroy(closeButton.GetComponent<LayoutElement>());

        LayoutForOrientation();
    }

    int IndexOfPour()
    {
        for (int i = 0; i < paints.Length; i++)
            if (paints[i] == mixing.pourColor) return i;
        return 0;
    }

    static RectTransform NewRect(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Anchor(RectTransform rt, float minX, float minY, float maxX, float maxY)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static RectTransform NewRow(string name, RectTransform parent, float minX, float minY, float maxX, float maxY)
    {
        RectTransform row = NewRect(name, parent);
        Anchor(row, minX, minY, maxX, maxY);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return row;
    }

    static TMP_Text NewText(string name, RectTransform parent, string text, float size)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static (Button button, Image image) NewCircleButton(string name, RectTransform parent, Color color, Action onClick)
    {
        RectTransform rt = NewRect(name, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = CircleSprite();
        image.color = color;
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = image;
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => onClick());
        AspectRatioFitter fit = rt.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        fit.aspectRatio = 1f;
        return (b, image);
    }

    static (Button button, Image image) NewTextButton(string name, RectTransform parent, string label, Action onClick)
    {
        RectTransform rt = NewRect(name, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.45f);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = image;
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => onClick());
        TMP_Text text = NewText("Label", rt, label, 32);
        text.color = new Color(0.15f, 0.12f, 0.1f);
        Anchor(text.rectTransform, 0f, 0f, 1f, 1f);
        rt.gameObject.AddComponent<LayoutElement>();
        return (b, image);
    }

    static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }
}
