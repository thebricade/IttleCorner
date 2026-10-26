using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Eyedropper for the world. Toggle it with the on-screen button, tap anything to grab the color
// under your finger, then the color mixer opens with that color as the target.
// Add this to any object in the scene; it builds its own button and creates the ColorMixerScreen if needed.
public class ColorDropper : MonoBehaviour
{
    public static ColorDropper Instance;

    [Tooltip("Optional. Found automatically if left empty.")]
    public DrawingPad drawingPad;
    [Tooltip("Square of pixels averaged around the tap, so dithering and noise don't skew the color.")]
    public int sampleSize = 5;

    public Color TargetColor { get; private set; }

    Canvas canvas;
    GameObject dropperButton;
    GameObject hint;
    bool sampling;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        ColorMixerScreen mixer = GetComponent<ColorMixerScreen>();
        if (mixer == null) mixer = gameObject.AddComponent<ColorMixerScreen>();
        mixer.onMatched = OnMixerMatched;

        BuildUI();
    }

    void Update()
    {
        GameMode mode = GameModeManager.Instance.currentMode;
        canvas.gameObject.SetActive(mode == GameMode.Explore || mode == GameMode.Dropper);
        hint.SetActive(mode == GameMode.Dropper);

        if (mode != GameMode.Dropper || sampling) return;

        if (Input.GetMouseButtonDown(0) && !IsOverUI())
        {
            StartCoroutine(SampleAt(Input.mousePosition));
        }
    }

    public void ToggleDropper()
    {
        GameMode mode = GameModeManager.Instance.currentMode;
        if (mode == GameMode.Explore) GameModeManager.Instance.SetGameMode(GameMode.Dropper);
        else if (mode == GameMode.Dropper) GameModeManager.Instance.SetGameMode(GameMode.Explore);
    }

    static bool IsOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    IEnumerator SampleAt(Vector2 screenPos)
    {
        sampling = true;

        // Hide our own overlay for a frame so the hint text doesn't end up in the sample.
        canvas.enabled = false;
        yield return null;
        yield return new WaitForEndOfFrame();

        int half = sampleSize / 2;
        int x = Mathf.Clamp(Mathf.RoundToInt(screenPos.x) - half, 0, Screen.width - sampleSize);
        int y = Mathf.Clamp(Mathf.RoundToInt(screenPos.y) - half, 0, Screen.height - sampleSize);

        Texture2D tex = new Texture2D(sampleSize, sampleSize, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(x, y, sampleSize, sampleSize), 0, 0);
        Color[] pixels = tex.GetPixels();
        Destroy(tex);

        Color sum = Color.black;
        foreach (Color p in pixels) sum += p;
        Color picked = sum / pixels.Length;
        picked.a = 1f;

        canvas.enabled = true;
        sampling = false;

        TargetColor = picked;
        ColorMixerScreen.Instance.Open(picked);
    }

    void OnMixerMatched(Color mixed)
    {
        StartCoroutine(OpenDrawBoard(mixed));
    }

    IEnumerator OpenDrawBoard(Color color)
    {
        GameModeManager.Instance.SetGameMode(GameMode.Drawing);
        yield return null; // let the drawing screen activate

        DrawingPad pad = drawingPad != null ? drawingPad : FindFirstObjectByType<DrawingPad>();
        if (pad != null) pad.SetBrushColor(color);
        else Debug.LogWarning("ColorDropper: no DrawingPad found to receive the matched color");
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("ColorDropperCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        dropperButton = new GameObject("DropperButton", typeof(RectTransform), typeof(Image), typeof(Button));
        dropperButton.transform.SetParent(canvasGO.transform, false);
        RectTransform rt = dropperButton.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-30, 30);
        rt.sizeDelta = new Vector2(220, 100);
        Image image = dropperButton.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.9f);
        dropperButton.GetComponent<Button>().onClick.AddListener(ToggleDropper);
        AddLabel(dropperButton.transform, "Dropper", 36, new Color(0.15f, 0.12f, 0.1f));

        hint = new GameObject("Hint", typeof(RectTransform));
        hint.transform.SetParent(canvasGO.transform, false);
        RectTransform hr = hint.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0.1f, 0.9f);
        hr.anchorMax = new Vector2(0.9f, 0.98f);
        hr.offsetMin = hr.offsetMax = Vector2.zero;
        AddLabel(hint.transform, "Tap something to grab its color", 40, Color.white);
        hint.SetActive(false);
    }

    static void AddLabel(Transform parent, string text, float size, Color color)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        t.outlineWidth = color == Color.white ? 0.25f : 0f;
        t.outlineColor = new Color32(0, 0, 0, 160);
    }
}
