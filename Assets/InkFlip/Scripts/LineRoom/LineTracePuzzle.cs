using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace InkFlip
{
    [Serializable]
    public class TraceGuide
    {
        [Tooltip("Where the dotted line starts, on the board: (0,0) is the bottom-left corner and (1,1) the top-right.")]
        public Vector2 from;
        [Tooltip("Where it ends.")]
        public Vector2 to;

        public TraceGuide() { }
        public TraceGuide(float x0, float y0, float x1, float y1)
        {
            from = new Vector2(x0, y0);
            to = new Vector2(x1, y1);
        }
    }

    // A line-drawing practice on a wall, to power the room on. The board shows dotted guide lines - across, down and
    // slanted - and the player traces each one with the paint brush in a single straight stroke, from one dot to the other.
    // A stroke that is close enough snaps into a clean bright line; a wobbly one just fades and can be tried again.
    // When every guide has been traced the room is powered: the lamps light up and On Solved is raised.
    //
    // FOR DESIGNERS - edit the Guides list. Each guide is two points on the board in 0-1 coordinates ((0,0) bottom-left,
    // (1,1) top-right). Horizontal = same Y, vertical = same X, diagonal = both different. The scene view updates as you
    // type. The right-click menu on this component has presets. Tolerance and End Radius set how forgiving it is.
    [ExecuteAlways]
    public class LineTracePuzzle : MonoBehaviour
    {
        [Header("The lines to trace (edit these!)")]
        public TraceGuide[] guides =
        {
            new TraceGuide(0.12f, 0.88f, 0.88f, 0.88f), // across, top
            new TraceGuide(0.88f, 0.88f, 0.88f, 0.12f), // down, right
            new TraceGuide(0.88f, 0.12f, 0.12f, 0.12f), // across, bottom
            new TraceGuide(0.12f, 0.12f, 0.12f, 0.88f), // down, left
            new TraceGuide(0.12f, 0.88f, 0.88f, 0.12f), // slant one way
            new TraceGuide(0.12f, 0.12f, 0.88f, 0.88f), // slant the other way
        };

        [Header("How forgiving")]
        [Tooltip("How far (metres) a stroke may wander from the dotted line and still count.")]
        public float tolerance = 0.16f;
        [Tooltip("How close (metres) to each dot the stroke has to start and finish.")]
        public float endRadius = 0.32f;

        [Header("Board parts (set by the scene builder)")]
        [Tooltip("Size of the board on the wall, in metres.")]
        public Vector2 boardSize = new Vector2(2.2f, 2.0f);
        public Transform screen;
        public Transform backing;
        public Transform powerStrip;
        public MeshRenderer screenRenderer;
        public Collider screenCollider;

        [Header("Playing")]
        public float reach = 3.4f;
        public FirstPersonController player;
        public ToolSwitcher tools;

        [Header("Power")]
        [Tooltip("These light up when the puzzle is solved (the power strip on the board is added automatically).")]
        public Renderer[] powerLamps;
        public Color lampOff = new Color(0.16f, 0.17f, 0.2f);
        public Color lampOn = new Color(1f, 0.93f, 0.62f);
        public Color stripOff = new Color(0.8f, 0.2f, 0.2f);
        public Color stripOn = new Color(0.3f, 1f, 0.5f);
        public UnityEvent onSolved;

        public bool Solved { get; private set; }
        public event Action BecameSolved;
        public int Total => guides != null ? guides.Length : 0;
        public int Traced
        {
            get
            {
                int n = 0;
                if (done != null) foreach (bool d in done) if (d) n++;
                return n;
            }
        }

        const float PixelsPerMetre = 190f;

        bool[] done;
        readonly List<Vector2> stroke = new List<Vector2>(); // metres on the board
        bool stroking;
        bool wasHeld;
        bool promptShown;
        int texW, texH;
        Texture2D texture;
        Color32[] pixels;
        MaterialPropertyBlock block;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        void OnEnable() => Rebuild();

        void OnDestroy()
        {
            if (texture != null)
            {
                if (Application.isPlaying) Destroy(texture);
                else DestroyImmediate(texture);
            }
            if (promptShown && Application.isPlaying) LineRoomUI.Instance.SetPrompt(this, "");
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Rebuild();
            };
#endif
        }

        [ContextMenu("Preset: box with an X (across, down, slants)")]
        void PresetBoxWithX()
        {
            guides = new[]
            {
                new TraceGuide(0.12f, 0.88f, 0.88f, 0.88f), new TraceGuide(0.88f, 0.88f, 0.88f, 0.12f),
                new TraceGuide(0.88f, 0.12f, 0.12f, 0.12f), new TraceGuide(0.12f, 0.12f, 0.12f, 0.88f),
                new TraceGuide(0.12f, 0.88f, 0.88f, 0.12f), new TraceGuide(0.12f, 0.12f, 0.88f, 0.88f),
            };
            Rebuild();
        }

        [ContextMenu("Preset: lightning bolt (across, slant, across, slant)")]
        void PresetBolt()
        {
            guides = new[]
            {
                new TraceGuide(0.3f, 0.9f, 0.7f, 0.9f), new TraceGuide(0.7f, 0.9f, 0.3f, 0.5f),
                new TraceGuide(0.3f, 0.5f, 0.7f, 0.5f), new TraceGuide(0.7f, 0.5f, 0.3f, 0.1f),
            };
            Rebuild();
        }

        [ContextMenu("Reset puzzle")]
        public void ResetPuzzle()
        {
            Solved = false;
            Rebuild();
        }

        // ---------- building the board ----------

        public void Rebuild()
        {
            int count = Total;
            if (done == null || done.Length != count || !Solved) done = new bool[count];
            if (Solved) for (int i = 0; i < done.Length; i++) done[i] = true;
            stroke.Clear();
            stroking = false;

            SetScale(screen, new Vector3(boardSize.x, boardSize.y, 1f));
            if (backing != null)
            {
                SetScale(backing, new Vector3(boardSize.x + 0.24f, boardSize.y + 0.24f + 0.12f, 0.05f));
                SetPosition(backing, new Vector3(0f, 0.06f, 0.03f));
            }
            if (powerStrip != null)
            {
                SetScale(powerStrip, new Vector3(boardSize.x, 0.05f, 0.03f));
                SetPosition(powerStrip, new Vector3(0f, boardSize.y * 0.5f + 0.12f, -0.01f));
            }

            int w = Mathf.Max(64, Mathf.RoundToInt(boardSize.x * PixelsPerMetre));
            int h = Mathf.Max(64, Mathf.RoundToInt(boardSize.y * PixelsPerMetre));
            if (texture == null || texture.width != w || texture.height != h)
            {
                if (texture != null)
                {
                    if (Application.isPlaying) Destroy(texture);
                    else DestroyImmediate(texture);
                }
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "LineTraceBoard",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
            }
            texW = w;
            texH = h;
            pixels = new Color32[w * h];
            Draw();
            ApplyPower(Solved);
        }

        static void SetScale(Transform t, Vector3 scale)
        {
            if (t != null && (t.localScale - scale).sqrMagnitude > 0.000001f) t.localScale = scale;
        }

        static void SetPosition(Transform t, Vector3 position)
        {
            if (t != null && (t.localPosition - position).sqrMagnitude > 0.000001f) t.localPosition = position;
        }

        // ---------- drawing the texture ----------

        Vector2 ToPixels(Vector2 metres) => new Vector2(metres.x / boardSize.x * texW, metres.y / boardSize.y * texH);
        Vector2 GuideMetres(Vector2 normalised) => new Vector2(normalised.x * boardSize.x, normalised.y * boardSize.y);

        void Draw()
        {
            if (texture == null || pixels == null) return;

            var background = new Color32(14, 17, 30, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;

            // a thin frame, so it reads as a screen
            Color32 frame = new Color32(40, 48, 78, 255);
            FillRect(0, 0, texW, 5, frame);
            FillRect(0, texH - 5, texW, texH, frame);
            FillRect(0, 0, 5, texH, frame);
            FillRect(texW - 5, 0, texW, texH, frame);

            for (int g = 0; g < Total; g++)
            {
                Vector2 a = ToPixels(GuideMetres(guides[g].from));
                Vector2 b = ToPixels(GuideMetres(guides[g].to));
                if (done[g])
                {
                    Color32 glow = Solved ? new Color32(70, 200, 120, 255) : new Color32(255, 214, 70, 255);
                    Segment(a, b, 11f, new Color32((byte)(glow.r / 3), (byte)(glow.g / 3), (byte)(glow.b / 3), 255));
                    Segment(a, b, 6f, glow);
                    Disc(a, 11f, glow);
                    Disc(b, 11f, glow);
                }
                else
                {
                    Dashed(a, b, 3f, 16f, 12f, new Color32(80, 150, 190, 255));
                    Ring(a, 13f, 4f, new Color32(120, 220, 255, 255));
                    Ring(b, 13f, 4f, new Color32(120, 220, 255, 255));
                }
            }

            // the stroke being drawn right now
            for (int i = 1; i < stroke.Count; i++) Segment(ToPixels(stroke[i - 1]), ToPixels(stroke[i]), 6f, new Color32(255, 255, 255, 255));

            texture.SetPixels32(pixels);
            texture.Apply(false);

            if (screenRenderer != null)
            {
                block ??= new MaterialPropertyBlock();
                screenRenderer.GetPropertyBlock(block);
                block.SetTexture(BaseMapId, texture);
                block.SetTexture(MainTexId, texture);
                screenRenderer.SetPropertyBlock(block);
            }
        }

        void FillRect(int x0, int y0, int x1, int y1, Color32 color)
        {
            x0 = Mathf.Clamp(x0, 0, texW); x1 = Mathf.Clamp(x1, 0, texW);
            y0 = Mathf.Clamp(y0, 0, texH); y1 = Mathf.Clamp(y1, 0, texH);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    pixels[y * texW + x] = color;
        }

        void Disc(Vector2 c, float radius, Color32 color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - radius)), x1 = Mathf.Min(texW - 1, Mathf.CeilToInt(c.x + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - radius)), y1 = Mathf.Min(texH - 1, Mathf.CeilToInt(c.y + radius));
            float r2 = radius * radius;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - c.x, dy = y + 0.5f - c.y;
                    if (dx * dx + dy * dy <= r2) pixels[y * texW + x] = color;
                }
        }

        void Ring(Vector2 c, float radius, float width, Color32 color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - radius)), x1 = Mathf.Min(texW - 1, Mathf.CeilToInt(c.x + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - radius)), y1 = Mathf.Min(texH - 1, Mathf.CeilToInt(c.y + radius));
            float outer = radius * radius, inner = (radius - width) * (radius - width);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - c.x, dy = y + 0.5f - c.y;
                    float d = dx * dx + dy * dy;
                    if (d <= outer && d >= inner) pixels[y * texW + x] = color;
                }
        }

        void Segment(Vector2 a, Vector2 b, float thickness, Color32 color)
        {
            float half = thickness * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - half)), x1 = Mathf.Min(texW - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + half));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - half)), y1 = Mathf.Min(texH - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + half));
            Vector2 ab = b - a;
            float lengthSq = Mathf.Max(0.0001f, ab.sqrMagnitude);
            float h2 = half * half;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
                    if ((p - (a + ab * t)).sqrMagnitude <= h2) pixels[y * texW + x] = color;
                }
        }

        void Dashed(Vector2 a, Vector2 b, float thickness, float dash, float gap, Color32 color)
        {
            float length = Vector2.Distance(a, b);
            Vector2 direction = (b - a) / Mathf.Max(0.0001f, length);
            for (float d = 0f; d < length; d += dash + gap)
                Segment(a + direction * d, a + direction * Mathf.Min(length, d + dash), thickness, color);
        }

        // ---------- power ----------

        void ApplyPower(bool on)
        {
            var b = new MaterialPropertyBlock();
            if (powerLamps != null)
            {
                foreach (Renderer lamp in powerLamps)
                {
                    if (lamp == null) continue;
                    lamp.GetPropertyBlock(b);
                    b.SetColor(BaseColorId, on ? lampOn : lampOff);
                    lamp.SetPropertyBlock(b);
                }
            }
            if (powerStrip != null && powerStrip.TryGetComponent(out Renderer strip))
            {
                strip.GetPropertyBlock(b);
                b.SetColor(BaseColorId, on ? stripOn : stripOff);
                strip.SetPropertyBlock(b);
            }
        }

        // ---------- playing ----------

        void Update()
        {
            if (!Application.isPlaying) return;
            if (Solved)
            {
                if (promptShown) ShowPrompt("");
                return;
            }

            if (player == null) player = FindAnyObjectByType<FirstPersonController>();
            if (tools == null) tools = FindAnyObjectByType<ToolSwitcher>();
            Camera cam = Camera.main;
            if (cam == null || screenCollider == null || Total == 0) return;

            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            bool held = (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightTrigger.isPressed);
            bool pressed = held && !wasHeld;
            wasHeld = held;

            bool captured = FirstPersonController.InputCaptured && !LineRoomUI.Busy;
            bool aiming = false;
            Vector2 metres = default;
            if (captured && screenCollider.Raycast(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out RaycastHit hit, reach))
            {
                Vector3 local = screen.InverseTransformPoint(hit.point);
                float u = local.x + 0.5f, v = local.y + 0.5f;
                aiming = u >= 0f && u <= 1f && v >= 0f && v <= 1f;
                metres = new Vector2(u * boardSize.x, v * boardSize.y);
            }

            bool brush = tools != null && !tools.HandsEmpty && tools.Active.requires == Ability.PaintBrush;
            if (!brush)
            {
                if (stroking) EndStroke(false);
                if (aiming) ShowPrompt("Switch to the Paint Brush to trace the lines");
                else if (promptShown) ShowPrompt("");
                return;
            }

            if (aiming && !stroking) ShowPrompt($"Trace each dotted line in one straight stroke, dot to dot   ({Traced} / {Total})");
            else if (!aiming && !stroking && promptShown) ShowPrompt("");

            if (pressed && aiming)
            {
                stroking = true;
                stroke.Clear();
                stroke.Add(metres);
            }
            else if (stroking)
            {
                if (held)
                {
                    if (aiming && Vector2.Distance(stroke[stroke.Count - 1], metres) >= 0.02f)
                    {
                        stroke.Add(metres);
                        Draw();
                    }
                }
                else
                {
                    EndStroke(true);
                }
            }
        }

        void ShowPrompt(string text)
        {
            promptShown = !string.IsNullOrEmpty(text);
            LineRoomUI.Instance.SetPrompt(this, text);
        }

        void EndStroke(bool evaluate)
        {
            stroking = false;
            if (evaluate && stroke.Count >= 3) Evaluate();
            stroke.Clear();
            Draw();
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        void Evaluate()
        {
            Vector2 first = stroke[0], last = stroke[stroke.Count - 1];
            float length = 0f;
            for (int i = 1; i < stroke.Count; i++) length += Vector2.Distance(stroke[i - 1], stroke[i]);
            if (length < 0.25f) return; // a dab, not a line

            int best = -1;
            float bestDeviation = float.MaxValue;
            bool nearMiss = false;

            for (int g = 0; g < Total; g++)
            {
                if (done[g]) continue;
                Vector2 a = GuideMetres(guides[g].from), b = GuideMetres(guides[g].to);
                bool forward = Vector2.Distance(first, a) <= endRadius && Vector2.Distance(last, b) <= endRadius;
                bool reverse = Vector2.Distance(first, b) <= endRadius && Vector2.Distance(last, a) <= endRadius;
                if (!forward && !reverse) continue;

                float deviation = 0f;
                foreach (Vector2 p in stroke) deviation = Mathf.Max(deviation, DistanceToSegment(p, a, b));
                if (deviation <= tolerance)
                {
                    if (deviation < bestDeviation)
                    {
                        bestDeviation = deviation;
                        best = g;
                    }
                }
                else
                {
                    nearMiss = true;
                }
            }

            if (best < 0)
            {
                LineRoomUI.Instance.Say(nearMiss ? "Wobbly! Keep it straight and try again" : "Start on one dot and finish on the other", 1.6f);
                return;
            }

            done[best] = true;
            if (Traced >= Total)
            {
                Solved = true;
                Draw();
                ApplyPower(true);
                if (promptShown) ShowPrompt("");
                LineRoomUI.Instance.Say("Power restored!", 3f);
                onSolved?.Invoke();
                BecameSolved?.Invoke();
            }
            else
            {
                LineRoomUI.Instance.Say($"Nice and straight!  ({Traced} / {Total})", 1.2f);
            }
        }

        // finish it from a test key or another script
        public void ForceSolve()
        {
            if (Solved) return;
            Solved = true;
            for (int i = 0; i < done.Length; i++) done[i] = true;
            Draw();
            ApplyPower(true);
            onSolved?.Invoke();
            BecameSolved?.Invoke();
        }
    }
}
