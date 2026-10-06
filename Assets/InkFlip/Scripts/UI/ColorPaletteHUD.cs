using UnityEngine;
using UnityEngine.UI;

namespace InkFlip
{
    // Builds its own screen-space canvas at runtime: a hotbar of ink swatches along the
    // bottom of the screen (clickable when the cursor is free, e.g. in draw mode), the
    // selected ink's name, a crosshair tinted with the current ink, a tool list
    // (bottom-left) and the brush's paint-load bar. The crosshair, tool list and load bar
    // hide while in draw mode. Nothing to wire up besides the PaintColorSelector;
    // the ToolSwitcher and SelectionDrawMode are optional.
    public class ColorPaletteHUD : MonoBehaviour
    {
        public PaintColorSelector colors;
        public ToolSwitcher tools;
        public SelectionDrawMode drawMode;

        [Header("Layout")]
        public float swatchSize = 64f;
        public float swatchSpacing = 12f;
        public float selectedScale = 1.25f;
        public float bottomMargin = 36f;
        public float nameDisplaySeconds = 1.5f;

        RectTransform[] slots;
        Image[] slotRims;
        Image[] crosshairParts;
        Text nameLabel;
        float nameTimer;

        RectTransform walkingOnlyGroup; // crosshair, tool list, brush load - hidden in draw mode
        Text[] toolNames;
        Text[] toolControls;
        GameObject loadBar;
        RectTransform loadFill;
        Image loadFillImage;
        PaintBrush activeBrush;
        const float LoadBarWidth = 70f;

        static readonly Color RimColor = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color SelectedRimColor = new Color(1f, 1f, 1f, 0.95f);

        void Start()
        {
            if (colors == null) colors = FindAnyObjectByType<PaintColorSelector>();
            if (colors == null)
            {
                Debug.LogError("ColorPaletteHUD needs a PaintColorSelector.", this);
                enabled = false;
                return;
            }
            if (tools == null) tools = FindAnyObjectByType<ToolSwitcher>();
            if (drawMode == null) drawMode = FindAnyObjectByType<SelectionDrawMode>();

            HudFactory.EnsureEventSystem();
            BuildCanvas();

            colors.SelectionChanged += OnSelectionChanged;
            if (tools != null) tools.ToolChanged += OnToolChanged;
            if (drawMode != null) drawMode.ModeChanged += OnDrawModeChanged;
            Refresh(showName: true);
            RefreshTools(showName: false);
        }

        void OnDestroy()
        {
            if (colors != null) colors.SelectionChanged -= OnSelectionChanged;
            if (tools != null) tools.ToolChanged -= OnToolChanged;
            if (drawMode != null) drawMode.ModeChanged -= OnDrawModeChanged;
        }

        void Update()
        {
            if (nameLabel == null) return;
            nameTimer -= Time.deltaTime;
            Color c = nameLabel.color;
            c.a = Mathf.Clamp01(nameTimer / 0.4f);
            nameLabel.color = c;

            // brush paint load: only shown while it isn't full
            bool showLoad = activeBrush != null && activeBrush.Load < 0.999f;
            if (loadBar.activeSelf != showLoad) loadBar.SetActive(showLoad);
            if (showLoad) loadFill.sizeDelta = new Vector2(LoadBarWidth * activeBrush.Load, loadFill.sizeDelta.y);
        }

        void OnSelectionChanged(int index) => Refresh(showName: true);

        void OnToolChanged(int index) => RefreshTools(showName: true);

        void OnDrawModeChanged(bool drawing)
        {
            walkingOnlyGroup.gameObject.SetActive(!drawing);
            if (!drawing) RefreshTools(showName: false);
        }

        void RefreshTools(bool showName)
        {
            if (tools == null || toolNames == null) return;

            for (int i = 0; i < toolNames.Length; i++)
            {
                bool active = i == tools.ActiveIndex;
                toolNames[i].color = active ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                toolNames[i].fontSize = active ? 30 : 22;
                toolControls[i].gameObject.SetActive(active);
            }

            GameObject root = tools.Active.root;
            activeBrush = root != null ? root.GetComponentInChildren<PaintBrush>(true) : null;

            if (showName)
            {
                nameLabel.text = tools.Active.name;
                nameTimer = nameDisplaySeconds;
            }
        }

        void Refresh(bool showName)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                bool selected = i == colors.SelectedIndex;
                slots[i].localScale = Vector3.one * (selected ? selectedScale : 1f);
                slotRims[i].color = selected ? SelectedRimColor : RimColor;
            }

            foreach (Image part in crosshairParts) part.color = colors.CurrentColor;
            loadFillImage.color = colors.CurrentColor;

            if (showName)
            {
                nameLabel.text = colors.Current.name;
                nameTimer = nameDisplaySeconds;
            }
        }

        void BuildCanvas()
        {
            RectTransform root = HudFactory.CreateCanvas("PaletteCanvas", transform, 10);

            // hotbar - each swatch is also a button for when the cursor is free
            int count = colors.palette.Length;
            slots = new RectTransform[count];
            slotRims = new Image[count];
            float totalWidth = count * swatchSize + (count - 1) * swatchSpacing;

            for (int i = 0; i < count; i++)
            {
                Image rim = HudFactory.CreateImage($"Slot{i + 1}", root, HudFactory.Circle, RimColor, swatchSize + 10f);
                RectTransform slot = rim.rectTransform;
                slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0f);
                slot.anchoredPosition = new Vector2(-totalWidth * 0.5f + swatchSize * 0.5f + i * (swatchSize + swatchSpacing), bottomMargin + swatchSize * 0.5f);
                slots[i] = slot;
                slotRims[i] = rim;

                rim.raycastTarget = true;
                int index = i;
                var button = rim.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => colors.Select(index));

                HudFactory.CreateImage("Ink", slot, HudFactory.Circle, colors.palette[i].color, swatchSize - 4f);

                Text key = HudFactory.CreateText("Key", slot, (i + 1).ToString(), 18, TextAnchor.MiddleCenter);
                key.rectTransform.anchoredPosition = new Vector2(0f, -swatchSize * 0.5f - 14f);
            }

            // selected ink / tool name, above the hotbar
            nameLabel = HudFactory.CreateText("InkName", root, "", 28, TextAnchor.MiddleCenter);
            RectTransform nameRect = nameLabel.rectTransform;
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0f);
            nameRect.sizeDelta = new Vector2(400f, 40f);
            nameRect.anchoredPosition = new Vector2(0f, bottomMargin + swatchSize * selectedScale + 30f);

            walkingOnlyGroup = HudFactory.CreateGroup("WalkingOnly", root);

            // crosshair: a dot with four ticks
            crosshairParts = new Image[5];
            crosshairParts[0] = HudFactory.CreateImage("CrosshairDot", walkingOnlyGroup, HudFactory.Circle, Color.white, 8f);
            Vector2[] tickOffsets = { new Vector2(0, 16), new Vector2(0, -16), new Vector2(16, 0), new Vector2(-16, 0) };
            for (int i = 0; i < 4; i++)
            {
                Image tick = HudFactory.CreateImage("CrosshairTick", walkingOnlyGroup, null, Color.white, 0f);
                bool vertical = tickOffsets[i].x == 0f;
                tick.rectTransform.sizeDelta = vertical ? new Vector2(3f, 10f) : new Vector2(10f, 3f);
                tick.rectTransform.anchoredPosition = tickOffsets[i];
                crosshairParts[i + 1] = tick;
            }
            foreach (Image part in crosshairParts)
            {
                var outline = part.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            }

            // brush paint-load bar, just under the crosshair
            Image barBack = HudFactory.CreateImage("BrushLoad", walkingOnlyGroup, null, new Color(0f, 0f, 0f, 0.5f), 0f);
            barBack.rectTransform.sizeDelta = new Vector2(LoadBarWidth + 4f, 10f);
            barBack.rectTransform.anchoredPosition = new Vector2(0f, -36f);
            loadFillImage = HudFactory.CreateImage("Fill", barBack.rectTransform, null, Color.white, 0f);
            loadFill = loadFillImage.rectTransform;
            loadFill.anchorMin = loadFill.anchorMax = new Vector2(0f, 0.5f);
            loadFill.pivot = new Vector2(0f, 0.5f);
            loadFill.anchoredPosition = new Vector2(2f, 0f);
            loadFill.sizeDelta = new Vector2(LoadBarWidth, 6f);
            loadBar = barBack.gameObject;
            loadBar.SetActive(false);

            BuildToolList(walkingOnlyGroup);
        }

        // bottom-left: every tool, the active one large + its controls, and how to switch
        void BuildToolList(RectTransform root)
        {
            if (tools == null || tools.tools == null) return;

            int count = tools.tools.Length;
            toolNames = new Text[count];
            toolControls = new Text[count];

            Text switchHint = HudFactory.CreateText("SwitchHint", root, "Tab / Middle Mouse: next tool", 18, TextAnchor.LowerLeft);
            PlaceBottomLeft(switchHint.rectTransform, 40f, 420f);

            float y = 40f + 34f;
            for (int i = count - 1; i >= 0; i--)
            {
                Text controls = HudFactory.CreateText("Controls", root, tools.tools[i].controls, 18, TextAnchor.LowerLeft);
                controls.fontStyle = FontStyle.Normal;
                PlaceBottomLeft(controls.rectTransform, y, 420f);
                toolControls[i] = controls;

                string key = tools.KeyLabel(i);
                string label = key.Length > 0 ? $"[{key}]  {tools.tools[i].name}" : tools.tools[i].name;
                Text toolName = HudFactory.CreateText("Tool", root, label, 22, TextAnchor.LowerLeft);
                PlaceBottomLeft(toolName.rectTransform, y + 24f, 420f);
                toolNames[i] = toolName;

                y += 72f;
            }
        }

        static void PlaceBottomLeft(RectTransform rect, float y, float width)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(width, 36f);
            rect.anchoredPosition = new Vector2(36f, y);
        }
    }
}
