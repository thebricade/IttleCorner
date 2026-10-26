using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// A small pool of wet paint. Pour paint in, swirl it around with a finger, tap to pick a color out of it.
// Every pixel keeps a color and an "amount" (how much paint is there) so thin smears fade to paper.
[RequireComponent(typeof(RawImage))]
public class MixingCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum Tool { Pour, Swirl, Pick }

    public int textureSize = 192;
    public int brushRadius = 14;
    public Color paperColor = new Color(0.96f, 0.95f, 0.92f);

    public Tool tool = Tool.Pour;
    public Color pourColor = Color.red;

    // Fires with the color under the brush as you pour, swirl or pick, and onPickReleased fires when you lift.
    public Action<Color> onColorPicked;
    public Action onPickReleased;

    Texture2D texture;
    Color[] display;
    Color[] paint;
    float[] amount;
    RectTransform rect;
    Camera pressCamera;

    bool pointerDown;
    Vector2 lastPixel;
    Vector2 currentPixel;
    Color carryColor;
    float carryAmount;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        int n = textureSize * textureSize;
        display = new Color[n];
        paint = new Color[n];
        amount = new float[n];

        texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        GetComponent<RawImage>().texture = texture;
        Clear();
    }

    void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    public void Clear()
    {
        for (int i = 0; i < display.Length; i++)
        {
            display[i] = paperColor;
            paint[i] = paperColor;
            amount[i] = 0f;
        }
        texture.SetPixels(display);
        texture.Apply();
    }

    public void OnPointerDown(PointerEventData e)
    {
        pressCamera = e.pressEventCamera;
        if (!TryGetPixel(e.position, out currentPixel)) return;

        pointerDown = true;
        lastPixel = currentPixel;

        if (tool == Tool.Swirl) PickUpCarry(currentPixel);
        ApplyAt(currentPixel, true);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!pointerDown) return;
        if (!TryGetPixel(e.position, out currentPixel)) return;

        // Walk the segment so fast drags don't leave gaps.
        float spacing = Mathf.Max(1f, brushRadius * 0.35f);
        float dist = Vector2.Distance(lastPixel, currentPixel);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / spacing));
        for (int i = 1; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(lastPixel, currentPixel, i / (float)steps);
            ApplyAt(p, false);
        }
        lastPixel = currentPixel;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!pointerDown) return;
        pointerDown = false;
        onPickReleased?.Invoke();
    }

    // Holding still with paint selected keeps pouring.
    void Update()
    {
        if (pointerDown && tool == Tool.Pour && Input.GetMouseButton(0))
        {
            ApplyAt(currentPixel, true);
        }
    }

    bool TryGetPixel(Vector2 screenPos, out Vector2 pixel)
    {
        pixel = Vector2.zero;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, pressCamera, out Vector2 local))
            return false;

        Rect r = rect.rect;
        float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
        pixel = new Vector2(u * (textureSize - 1), v * (textureSize - 1));
        return true;
    }

    void ApplyAt(Vector2 center, bool stationary)
    {
        switch (tool)
        {
            case Tool.Pour:
                Pour(center, stationary ? 0.12f : 0.4f);
                ReportLiveColor(center);
                break;
            case Tool.Swirl:
                Swirl(center);
                ReportLiveColor(center);
                break;
            case Tool.Pick:
                if (SampleAverage(center, out Color c, out float a) && a > 0.15f)
                    onColorPicked?.Invoke(c);
                break;
        }
    }

    // The color under the brush after it has mixed in, so the comparison circle follows what you're making.
    void ReportLiveColor(Vector2 center)
    {
        if (SampleAverage(center, out Color c, out float a) && a > 0.05f)
            onColorPicked?.Invoke(c);
    }

    void Pour(Vector2 center, float strength)
    {
        ForEachInBrush(center, (idx, falloff) =>
        {
            float w = falloff * strength;
            float existing = amount[idx];
            paint[idx] = existing <= 0.001f ? pourColor : PaintMix.Mix(paint[idx], pourColor, w / (existing + w));
            amount[idx] = Mathf.Min(1f, existing + w);
        });
        Refresh(center);
    }

    void PickUpCarry(Vector2 center)
    {
        if (SampleAverage(center, out Color c, out float a))
        {
            carryColor = c;
            carryAmount = a;
        }
        else
        {
            carryAmount = 0f;
        }
    }

    // Drag the paint under the finger along with it, leaving a thinning trail.
    void Swirl(Vector2 center)
    {
        bool hadSample = SampleAverage(center, out Color under, out float underAmount);

        if (carryAmount > 0.01f)
        {
            ForEachInBrush(center, (idx, falloff) =>
            {
                float w = falloff * 0.45f;
                if (amount[idx] <= 0.001f)
                    paint[idx] = carryColor;
                else
                    paint[idx] = PaintMix.Mix(paint[idx], carryColor, w * carryAmount / (amount[idx] + w * carryAmount));
                amount[idx] = Mathf.Max(amount[idx], Mathf.Lerp(amount[idx], carryAmount, w));
            });
        }

        // The finger picks up some of what it just touched and loses a little as it goes.
        if (hadSample)
        {
            carryColor = carryAmount > 0.01f ? PaintMix.Mix(carryColor, under, 0.2f) : under;
            carryAmount = Mathf.Lerp(carryAmount, underAmount, 0.2f);
        }
        carryAmount *= 0.985f;

        Refresh(center);
    }

    bool SampleAverage(Vector2 center, out Color color, out float avgAmount)
    {
        Color sum = Color.black;
        float weight = 0f;
        float amountSum = 0f;
        int count = 0;
        int radius = Mathf.Max(2, brushRadius / 2);

        ForEachInRadius(center, radius, (idx, falloff) =>
        {
            float w = amount[idx] * falloff;
            sum += paint[idx] * w;
            weight += w;
            amountSum += amount[idx];
            count++;
        });

        if (weight <= 0.001f || count == 0)
        {
            color = paperColor;
            avgAmount = 0f;
            return false;
        }
        color = sum / weight;
        color.a = 1f;
        avgAmount = amountSum / count;
        return true;
    }

    void ForEachInBrush(Vector2 center, Action<int, float> visit)
    {
        ForEachInRadius(center, brushRadius, visit);
    }

    void ForEachInRadius(Vector2 center, int radius, Action<int, float> visit)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
        int maxX = Mathf.Min(textureSize - 1, Mathf.CeilToInt(center.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
        int maxY = Mathf.Min(textureSize - 1, Mathf.CeilToInt(center.y + radius));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / radius;
                if (d > 1f) continue;
                float falloff = 1f - d * d;
                visit(y * textureSize + x, falloff);
            }
        }
    }

    // Only re-bake the pixels around the brush, then upload once.
    void Refresh(Vector2 center)
    {
        int r = brushRadius + 1;
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - r));
        int maxX = Mathf.Min(textureSize - 1, Mathf.CeilToInt(center.x + r));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - r));
        int maxY = Mathf.Min(textureSize - 1, Mathf.CeilToInt(center.y + r));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int idx = y * textureSize + x;
                display[idx] = Color.Lerp(paperColor, paint[idx], Mathf.Clamp01(amount[idx] * 2f));
            }
        }
        texture.SetPixels(display);
        texture.Apply();
    }
}
