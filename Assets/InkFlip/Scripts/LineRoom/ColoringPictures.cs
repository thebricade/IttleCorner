using UnityEngine;

namespace InkFlip
{
    // The colour-only pictures for the "reverse colouring book": shapes filled with flat colour and no outlines.
    // Each picture is a grid of region numbers (0 = empty paper) plus a colour for every region number.
    public static class ColoringPictures
    {
        public const int Count = 5;

        public static readonly string[] Names = { "tree", "sun", "house", "fish", "flower" };

        public class Picture
        {
            public int size;
            public byte[] region;      // size * size, row 0 at the bottom
            public Color32[] palette;  // index = region number (0 unused)
        }

        public static Picture Generate(int index, int size = 512)
        {
            var canvas = new Canvas(size);
            float s = size / 512f; // the shapes below are drawn for a 512 grid

            switch (((index % Count) + Count) % Count)
            {
                case 0: // tree
                    canvas.Rect(222, 40, 290, 235, s, new Color(0.55f, 0.35f, 0.2f));
                    canvas.Disc(256, 330, 125, s, new Color(0.3f, 0.7f, 0.3f));
                    canvas.Disc(180, 285, 80, s, new Color(0.3f, 0.7f, 0.3f));
                    canvas.Disc(332, 285, 80, s, new Color(0.3f, 0.7f, 0.3f));
                    break;

                case 1: // sun
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI * 0.25f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Vector2 side = new Vector2(-dir.y, dir.x);
                        Vector2 baseA = new Vector2(256, 256) + dir * 135f + side * 28f;
                        Vector2 baseB = new Vector2(256, 256) + dir * 135f - side * 28f;
                        Vector2 tip = new Vector2(256, 256) + dir * 225f;
                        canvas.Triangle(baseA, baseB, tip, s, new Color(1f, 0.6f, 0.15f));
                    }
                    canvas.Disc(256, 256, 135, s, new Color(1f, 0.85f, 0.2f));
                    break;

                case 2: // house
                    canvas.Rect(110, 60, 402, 270, s, new Color(0.85f, 0.3f, 0.3f));
                    canvas.Triangle(new Vector2(80, 270), new Vector2(432, 270), new Vector2(256, 440), s, new Color(0.25f, 0.4f, 0.8f));
                    canvas.Rect(226, 60, 286, 175, s, new Color(1f, 0.85f, 0.3f));
                    canvas.Rect(136, 165, 200, 230, s, new Color(0.65f, 0.85f, 0.95f));
                    canvas.Rect(312, 165, 376, 230, s, new Color(0.65f, 0.85f, 0.95f));
                    break;

                case 3: // fish
                    canvas.Triangle(new Vector2(380, 256), new Vector2(485, 355), new Vector2(485, 157), s, new Color(1f, 0.55f, 0.2f));
                    canvas.Ellipse(235, 256, 175, 105, s, new Color(1f, 0.55f, 0.2f));
                    canvas.Ellipse(225, 215, 120, 45, s, new Color(1f, 0.85f, 0.45f));
                    canvas.Triangle(new Vector2(190, 345), new Vector2(270, 345), new Vector2(230, 420), s, new Color(0.95f, 0.4f, 0.15f));
                    break;

                default: // flower
                    canvas.Rect(244, 40, 268, 270, s, new Color(0.3f, 0.65f, 0.3f));
                    canvas.Ellipse(205, 120, 52, 24, s, new Color(0.3f, 0.65f, 0.3f));
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * Mathf.PI / 3f + 0.3f;
                        canvas.Disc(256 + Mathf.Cos(a) * 88f, 340 + Mathf.Sin(a) * 88f, 62, s, new Color(1f, 0.5f, 0.7f));
                    }
                    canvas.Disc(256, 340, 52, s, new Color(1f, 0.85f, 0.25f));
                    break;
            }

            return canvas.ToPicture();
        }

        // a tiny rasteriser: every shape stamps a region number into the grid
        class Canvas
        {
            readonly int size;
            readonly byte[] region;
            readonly System.Collections.Generic.List<Color32> palette = new System.Collections.Generic.List<Color32> { new Color32(0, 0, 0, 0) };

            public Canvas(int size)
            {
                this.size = size;
                region = new byte[size * size];
            }

            byte IdOf(Color color)
            {
                Color32 c = color;
                for (int i = 1; i < palette.Count; i++)
                {
                    if (palette[i].r == c.r && palette[i].g == c.g && palette[i].b == c.b) return (byte)i;
                }
                palette.Add(c);
                return (byte)(palette.Count - 1);
            }

            public void Disc(float cx, float cy, float r, float s, Color color) => Ellipse(cx, cy, r, r, s, color);

            public void Ellipse(float cx, float cy, float rx, float ry, float s, Color color)
            {
                cx *= s; cy *= s; rx *= s; ry *= s;
                byte id = IdOf(color);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + rx));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + ry));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = (x - cx) / rx, dy = (y - cy) / ry;
                        if (dx * dx + dy * dy <= 1f) region[y * size + x] = id;
                    }
                }
            }

            public void Rect(float xMin, float yMin, float xMax, float yMax, float s, Color color)
            {
                byte id = IdOf(color);
                int x0 = Mathf.Max(0, Mathf.RoundToInt(xMin * s)), x1 = Mathf.Min(size - 1, Mathf.RoundToInt(xMax * s));
                int y0 = Mathf.Max(0, Mathf.RoundToInt(yMin * s)), y1 = Mathf.Min(size - 1, Mathf.RoundToInt(yMax * s));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++) region[y * size + x] = id;
                }
            }

            public void Triangle(Vector2 a, Vector2 b, Vector2 c, float s, Color color)
            {
                a *= s; b *= s; c *= s;
                byte id = IdOf(color);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x))), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x)));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y))), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y)));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2(x, y);
                        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
                        bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
                        bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
                        if (!(negative && positive)) region[y * size + x] = id;
                    }
                }
            }

            static float Sign(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

            public Picture ToPicture() => new Picture { size = size, region = region, palette = palette.ToArray() };
        }
    }
}
