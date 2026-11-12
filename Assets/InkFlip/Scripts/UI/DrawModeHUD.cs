using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InkFlip
{
    // Shown only in draw mode: a brush toolbar on the right (the old draw board's
    // brushes + eraser), brush size controls, a Done button, a controls hint, and a
    // ring under the mouse showing exactly how big the brush is.
    public class DrawModeHUD : MonoBehaviour
    {
        public SelectionDrawMode drawMode;

        static readonly (DrawBrush brush, string label)[] Brushes =
        {
            (DrawBrush.Big, "Big Pen"),
            (DrawBrush.Medium, "Medium Pen"),
            (DrawBrush.Small, "Small Pen"),
            (DrawBrush.Watercolor, "Watercolor"),
            (DrawBrush.Watercolor2, "Watercolor 2"),
            (DrawBrush.GelGlitter, "Gel Glitter"),
            (DrawBrush.Eraser, "Eraser"),
        };

        static readonly Color ButtonColor = new Color(0.1f, 0.1f, 0.12f, 0.8f);
        static readonly Color SelectedButtonColor = new Color(1f, 1f, 1f, 0.9f);

        RectTransform root;
        RectTransform content;
        Image[] brushButtons;
        Text[] brushLabels;
        Text sizeLabel;
        RectTransform cursorRing;
        Image cursorRingImage;
        Canvas canvas;
        RectTransform turnPad;
        int heldArrow = -1;
        float heldTime;

        // left arrow, right arrow, up, down: (yaw, tilt) - the same directions as the A / D / W / S keys
        static readonly Vector2[] ArrowDirections = { new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f) };
        const float TapDegrees = 45f;
        const float HoldSeconds = 0.25f;
        const float HoldDegreesPerSecond = 150f;

        void Start()
        {
            if (drawMode == null) drawMode = FindAnyObjectByType<SelectionDrawMode>();
            if (drawMode == null)
            {
                enabled = false;
                return;
            }

            HudFactory.EnsureEventSystem();
            Build();

            drawMode.ModeChanged += OnModeChanged;
            drawMode.BrushChanged += Refresh;
            OnModeChanged(drawMode.IsActive);
        }

        void OnDestroy()
        {
            if (drawMode == null) return;
            drawMode.ModeChanged -= OnModeChanged;
            drawMode.BrushChanged -= Refresh;
        }

        void OnModeChanged(bool active)
        {
            content.gameObject.SetActive(active);
            if (active) Refresh();
        }

        void Update()
        {
            UpdateTurnPad();
            if (!drawMode.IsActive || Mouse.current == null) return;

            // brush-size ring follows the mouse
            Vector2 mouse = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, mouse, null, out Vector2 local);
            cursorRing.anchoredPosition = local;
            float diameter = drawMode.BrushPixelRadius * 2f / Mathf.Max(0.01f, canvas.scaleFactor);
            cursorRing.sizeDelta = new Vector2(diameter, diameter);

            Color c = drawMode.brush == DrawBrush.Eraser ? Color.white : drawMode.CurrentColor;
            c.a = 0.9f;
            cursorRingImage.color = c;
        }

        // the arrows only show while drawing on something that can be turned
        void UpdateTurnPad()
        {
            DrawTurntable turntable = drawMode.IsActive && drawMode.Target != null ? drawMode.Target.GetComponent<DrawTurntable>() : null;
            if (turnPad.gameObject.activeSelf != (turntable != null)) turnPad.gameObject.SetActive(turntable != null);
            if (turntable == null)
            {
                heldArrow = -1;
                return;
            }

            if (heldArrow >= 0)
            {
                heldTime += Time.unscaledDeltaTime;
                if (heldTime > HoldSeconds)
                {
                    Vector2 d = ArrowDirections[heldArrow] * HoldDegreesPerSecond * Time.unscaledDeltaTime;
                    turntable.Nudge(d.x, d.y);
                }
            }
        }

        // tap = a quick 45 degree turn, hold = keep turning
        void ReleaseArrow(int index)
        {
            if (heldArrow != index) return;
            if (heldTime <= HoldSeconds && drawMode.Target != null)
            {
                DrawTurntable turntable = drawMode.Target.GetComponent<DrawTurntable>();
                if (turntable != null) turntable.Nudge(ArrowDirections[index].x * TapDegrees, ArrowDirections[index].y * TapDegrees);
            }
            heldArrow = -1;
        }

        void BuildTurnPad()
        {
            turnPad = HudFactory.CreateGroup("TurnPad", content);
            string[] labels = { "\u25C4", "\u25BA", "\u25B2", "\u25BC" };
            Vector2[] positions = { new Vector2(60f, 0f), new Vector2(200f, 0f), new Vector2(130f, 75f), new Vector2(130f, -75f) };

            Text title = HudFactory.CreateText("TurnTitle", turnPad, "Turn it", 24, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(130f, 0f);
            title.rectTransform.sizeDelta = new Vector2(100f, 40f);

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                HudFactory.CreateButton("Turn" + i, turnPad, labels[i], new Vector2(78f, 78f), () => { }, out Image background, out Text label);
                label.fontSize = 36;
                RectTransform rect = background.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.anchoredPosition = positions[i];

                var trigger = background.gameObject.AddComponent<EventTrigger>();
                var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                down.callback.AddListener(_ => { heldArrow = index; heldTime = 0f; });
                var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                up.callback.AddListener(_ => ReleaseArrow(index));
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => { if (heldArrow == index && heldTime > HoldSeconds) heldArrow = -1; });
                trigger.triggers.Add(down);
                trigger.triggers.Add(up);
                trigger.triggers.Add(exit);
            }
            turnPad.gameObject.SetActive(false);
        }

        void Refresh()
        {
            for (int i = 0; i < Brushes.Length; i++)
            {
                bool selected = Brushes[i].brush == drawMode.brush;
                brushButtons[i].color = selected ? SelectedButtonColor : ButtonColor;
                brushLabels[i].color = selected ? Color.black : Color.white;
                brushLabels[i].GetComponent<Outline>().enabled = !selected;
            }
            sizeLabel.text = $"Size  x{drawMode.sizeMultiplier:0.0}";
        }

        void Build()
        {
            root = HudFactory.CreateCanvas("DrawModeCanvas", transform, 20);
            canvas = root.GetComponent<Canvas>();
            content = HudFactory.CreateGroup("DrawMode", root);

            // toolbar on the right edge
            const float buttonWidth = 200f;
            const float buttonHeight = 44f;
            const float gap = 8f;
            float y = 220f;

            Text title = HudFactory.CreateText("Title", content, "Draw Mode", 30, TextAnchor.MiddleCenter);
            PlaceRight(title.rectTransform, y + 56f, new Vector2(buttonWidth, 40f));

            brushButtons = new Image[Brushes.Length];
            brushLabels = new Text[Brushes.Length];
            for (int i = 0; i < Brushes.Length; i++)
            {
                DrawBrush brush = Brushes[i].brush;
                HudFactory.CreateButton(Brushes[i].label, content, Brushes[i].label, new Vector2(buttonWidth, buttonHeight),
                    () => drawMode.SetBrush(brush), out Image background, out Text label);
                PlaceRight(background.rectTransform, y, background.rectTransform.sizeDelta);
                brushButtons[i] = background;
                brushLabels[i] = label;
                y -= buttonHeight + gap;
            }

            // size: [-]  Size x1.0  [+]
            y -= 10f;
            sizeLabel = HudFactory.CreateText("Size", content, "", 20, TextAnchor.MiddleCenter);
            PlaceRight(sizeLabel.rectTransform, y, new Vector2(buttonWidth, buttonHeight));
            HudFactory.CreateButton("Smaller", content, "-", new Vector2(40f, buttonHeight), () => drawMode.ChangeSize(-drawMode.sizeScrollStep * 2f), out Image minus, out _);
            PlaceRight(minus.rectTransform, y, minus.rectTransform.sizeDelta, buttonWidth - 40f);
            HudFactory.CreateButton("Bigger", content, "+", new Vector2(40f, buttonHeight), () => drawMode.ChangeSize(drawMode.sizeScrollStep * 2f), out Image plus, out _);
            PlaceRight(plus.rectTransform, y, plus.rectTransform.sizeDelta);

            // done
            y -= buttonHeight + gap * 3f;
            HudFactory.CreateButton("Done", content, "Done  (Esc)", new Vector2(buttonWidth, buttonHeight + 8f), drawMode.Exit, out Image done, out _);
            done.color = new Color(0.15f, 0.55f, 0.35f, 0.9f);
            PlaceRight(done.rectTransform, y, done.rectTransform.sizeDelta);

            // controls hint along the top
            Text hint = HudFactory.CreateText("Hint", content,
                "Drag to paint the selected object   |   Scroll: brush size   |   1-9 or click a swatch: color   |   Arrows or right-drag: turn it   |   Esc: done",
                20, TextAnchor.MiddleCenter);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            hint.rectTransform.sizeDelta = new Vector2(1400f, 40f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -40f);

            BuildTurnPad();

            // brush-size ring under the cursor
            cursorRingImage = HudFactory.CreateImage("BrushCursor", content, HudFactory.Ring, Color.white, 20f);
            cursorRing = cursorRingImage.rectTransform;
        }

        // anchors to the right-middle of the screen; xFromRight shifts it left from the margin
        static void PlaceRight(RectTransform rect, float y, Vector2 size, float xFromRight = 0f)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(-36f - xFromRight, y);
        }
    }
}
