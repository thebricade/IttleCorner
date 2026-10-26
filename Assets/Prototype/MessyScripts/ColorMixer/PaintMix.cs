using UnityEngine;

// Color math for the mixer: a paint-like blend (blue + yellow = green) and a perceptual
// distance so "how close is this to the target" matches what the eye sees.
public static class PaintMix
{
    const float Floor = 0.02f;

    // 0 = plain RGB lerp (blue + yellow = grey), 1 = fully multiplicative (blue + yellow = green, but darkens)
    public static float pigmentStrength = 0.6f;

    // t is the weight of b.
    public static Color Mix(Color a, Color b, float t)
    {
        t = Mathf.Clamp01(t);
        Color lerp = Color.Lerp(a, b, t);
        Color geo = new Color(
            Mathf.Pow(Mathf.Max(a.r, Floor), 1f - t) * Mathf.Pow(Mathf.Max(b.r, Floor), t),
            Mathf.Pow(Mathf.Max(a.g, Floor), 1f - t) * Mathf.Pow(Mathf.Max(b.g, Floor), t),
            Mathf.Pow(Mathf.Max(a.b, Floor), 1f - t) * Mathf.Pow(Mathf.Max(b.b, Floor), t),
            1f);
        Color result = Color.Lerp(lerp, geo, pigmentStrength);
        result.a = 1f;
        return result;
    }

    // CIE76 delta E between two sRGB colors. ~2 is barely noticeable, ~10 is clearly different.
    public static float DeltaE(Color a, Color b)
    {
        Vector3 la = ToLab(a);
        Vector3 lb = ToLab(b);
        return Vector3.Distance(la, lb);
    }

    static Vector3 ToLab(Color c)
    {
        float r = Linear(c.r), g = Linear(c.g), bl = Linear(c.b);

        float x = (r * 0.4124f + g * 0.3576f + bl * 0.1805f) / 0.95047f;
        float y = (r * 0.2126f + g * 0.7152f + bl * 0.0722f);
        float z = (r * 0.0193f + g * 0.1192f + bl * 0.9505f) / 1.08883f;

        x = LabF(x); y = LabF(y); z = LabF(z);
        return new Vector3(116f * y - 16f, 500f * (x - y), 200f * (y - z));
    }

    static float Linear(float v)
    {
        return v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
    }

    static float LabF(float t)
    {
        return t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
    }
}
