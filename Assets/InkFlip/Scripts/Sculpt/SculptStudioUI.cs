using System;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlip
{
    // The sculpt studio's buttons, built in code like the other HUDs. Touch-sized so it works on a phone or iPad.
    // Two steps share the screen: the pottery wheel (shape the base) and the sculpt bench (everything else).
    public class SculptStudioUI
    {
        public Action<ClayTool> ToolSelected;
        public Action<ClayShape> ShapeAdded;
        public Action<int> ColorSelected;
        public Action<int> WheelPresetSelected;
        public Action SizeSmaller, SizeBigger, MirrorToggled, SnapToggled, Undo, Reset, DeletePart, Done, Cancel, OpenClicked;
        public Action WheelTaller, WheelShorter, WheelNext, WheelSkip;
        public Action FitAll, FocusShape;

        static readonly Color ButtonColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
        static readonly Color SelectedColor = new Color(1f, 0.92f, 0.55f, 0.97f);

        static readonly (ClayTool tool, string label)[] Tools =
        {
            (ClayTool.Add, "Build"),
            (ClayTool.Dent, "Press"),
            (ClayTool.Smooth, "Smooth"),
            (ClayTool.Grab, "Pull"),
            (ClayTool.Move, "Move"),
            (ClayTool.Rotate, "Rotate"),
            (ClayTool.Resize, "Size"),
            (ClayTool.Spin, "Spin"),
        };

        static readonly ClayShape[] Shapes =
        {
            ClayShape.Sphere, ClayShape.Cube, ClayShape.Cylinder, ClayShape.Cone, ClayShape.Pyramid,
        };

        public static readonly string[] WheelPresets = { "Cylinder", "Vase", "Bowl", "Ball", "Cone" };

        const string SculptHint = "Drag a shape to use the tool  -  drag empty space or use two fingers to turn  -  double-tap to zoom";
        const string RotateHint = "Tap a shape to select it, then drag a ring to turn it - red, green and blue are the three axes";
        const string WheelHint = "Press the spinning clay with your finger to shape it - pull out to widen, push in to narrow";
        const float MessageSeconds = 3f;

        readonly RectTransform studio;
        readonly RectTransform wheelGroup;
        readonly RectTransform sculptGroup;
        readonly Button openButton;
        readonly Image[] toolBackgrounds = new Image[Tools.Length];
        readonly Text[] toolLabels = new Text[Tools.Length];
        Image mirrorBackground;
        Text mirrorLabel;
        Image snapBackground;
        Text snapLabel;
        Text sizeLabel;
        Text infoLabel;
        Text hintLabel;
        Text messageLabel;
        float messageTimer;

        public SculptStudioUI(Transform parent, Color[] clayColors)
        {
            HudFactory.EnsureEventSystem();
            RectTransform root = HudFactory.CreateCanvas("SculptCanvas", parent, 40);

            // the way in: shown in the world, hidden while sculpting
            openButton = HudFactory.CreateButton("SculptOpen", root, "Sculpt", new Vector2(190f, 80f), () => OpenClicked?.Invoke(), out _, out _);
            Place(openButton.transform, new Vector2(0f, 0f), new Vector2(30f, 30f));

            studio = HudFactory.CreateGroup("Studio", root);
            wheelGroup = HudFactory.CreateGroup("Wheel", studio);
            sculptGroup = HudFactory.CreateGroup("Sculpt", studio);

            BuildCommon(clayColors);
            BuildWheel();
            BuildSculpt();

            SetStudioVisible(false);
        }

        // shown in both steps: cancel, colours, zoom, notes
        void BuildCommon(Color[] clayColors)
        {
            Button cancel = HudFactory.CreateButton("Cancel", studio, "Cancel", new Vector2(200f, 72f), () => Cancel?.Invoke(), out _, out _);
            Place(cancel.transform, new Vector2(1f, 1f), new Vector2(-30f, -130f));

            // quick zoom
            Button fit = HudFactory.CreateButton("FitAll", studio, "Zoom all", new Vector2(170f, 72f), () => FitAll?.Invoke(), out _, out _);
            Place(fit.transform, new Vector2(0f, 0f), new Vector2(30f, 30f));
            Button focus = HudFactory.CreateButton("FocusShape", studio, "Zoom shape", new Vector2(170f, 72f), () => FocusShape?.Invoke(), out _, out _);
            Place(focus.transform, new Vector2(0f, 0f), new Vector2(30f, 112f));

            // clay colours (applies to the selected shape)
            for (int i = 0; i < clayColors.Length; i++)
            {
                int index = i;
                Image swatch = HudFactory.CreateImage("Clay_" + i, studio, HudFactory.Circle, clayColors[i], 76f);
                swatch.raycastTarget = true;
                Button swatchButton = swatch.gameObject.AddComponent<Button>();
                swatchButton.targetGraphic = swatch;
                swatchButton.onClick.AddListener(() => ColorSelected?.Invoke(index));
                Place(swatch.transform, new Vector2(0f, 1f), new Vector2(30f, -30f - i * 90f));
            }

            hintLabel = HudFactory.CreateText("Hint", studio, SculptHint, 24, TextAnchor.MiddleCenter);
            hintLabel.rectTransform.sizeDelta = new Vector2(1200f, 40f);
            Place(hintLabel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -40f));

            infoLabel = HudFactory.CreateText("Info", studio, "", 28, TextAnchor.MiddleCenter);
            infoLabel.rectTransform.sizeDelta = new Vector2(600f, 44f);
            Place(infoLabel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -90f));

            messageLabel = HudFactory.CreateText("Message", studio, "", 32, TextAnchor.MiddleCenter);
            messageLabel.color = new Color(1f, 0.85f, 0.4f);
            messageLabel.rectTransform.sizeDelta = new Vector2(1100f, 50f);
            Place(messageLabel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -150f));
        }

        // the pottery wheel step: pick a starting profile, then press the spinning clay to shape it
        void BuildWheel()
        {
            float presetWidth = 170f;
            float gap = 10f;
            float startX = -(WheelPresets.Length - 1) * (presetWidth + gap) * 0.5f;
            for (int i = 0; i < WheelPresets.Length; i++)
            {
                int index = i;
                Button button = HudFactory.CreateButton("Preset_" + WheelPresets[i], wheelGroup, WheelPresets[i], new Vector2(presetWidth, 84f),
                    () => WheelPresetSelected?.Invoke(index), out _, out Text label);
                label.fontSize = 28;
                Place(button.transform, new Vector2(0.5f, 0f), new Vector2(startX + i * (presetWidth + gap), 190f));
            }

            Button shorter = HudFactory.CreateButton("Shorter", wheelGroup, "Shorter", new Vector2(170f, 72f), () => WheelShorter?.Invoke(), out _, out _);
            Place(shorter.transform, new Vector2(0.5f, 0f), new Vector2(-300f, 80f));
            Button taller = HudFactory.CreateButton("Taller", wheelGroup, "Taller", new Vector2(170f, 72f), () => WheelTaller?.Invoke(), out _, out _);
            Place(taller.transform, new Vector2(0.5f, 0f), new Vector2(-110f, 80f));

            Button skip = HudFactory.CreateButton("Skip", wheelGroup, "Just a ball", new Vector2(190f, 72f), () => WheelSkip?.Invoke(), out _, out _);
            Place(skip.transform, new Vector2(0.5f, 0f), new Vector2(110f, 80f));

            Button next = HudFactory.CreateButton("Next", wheelGroup, "Next: Sculpt", new Vector2(250f, 84f), () => WheelNext?.Invoke(), out Image nextBackground, out Text nextLabel);
            nextBackground.color = new Color(0.25f, 0.7f, 0.4f, 0.95f);
            nextLabel.fontSize = 30;
            Place(next.transform, new Vector2(0.5f, 0f), new Vector2(380f, 78f));
        }

        // the sculpt bench: surface tools, arranging shapes, adding shapes
        void BuildSculpt()
        {
            // tools: sculpt the surface (Build..Pull), then arrange shapes (Move..Spin)
            float toolWidth = 150f;
            float gap = 10f;
            float startX = -(Tools.Length - 1) * (toolWidth + gap) * 0.5f;
            for (int i = 0; i < Tools.Length; i++)
            {
                ClayTool tool = Tools[i].tool;
                Button button = HudFactory.CreateButton("Tool_" + Tools[i].label, sculptGroup, Tools[i].label, new Vector2(toolWidth, 84f),
                    () => ToolSelected?.Invoke(tool), out toolBackgrounds[i], out toolLabels[i]);
                toolLabels[i].fontSize = 28;
                Place(button.transform, new Vector2(0.5f, 0f), new Vector2(startX + i * (toolWidth + gap), 190f));
            }

            // size, mirror, undo, reset
            Button smaller = HudFactory.CreateButton("SizeDown", sculptGroup, "-", new Vector2(84f, 72f), () => SizeSmaller?.Invoke(), out _, out Text minus);
            minus.fontSize = 36;
            Place(smaller.transform, new Vector2(0.5f, 0f), new Vector2(-380f, 80f));

            sizeLabel = HudFactory.CreateText("SizeLabel", sculptGroup, "Brush", 26, TextAnchor.MiddleCenter);
            sizeLabel.rectTransform.sizeDelta = new Vector2(150f, 50f);
            Place(sizeLabel.transform, new Vector2(0.5f, 0f), new Vector2(-285f, 80f));

            Button bigger = HudFactory.CreateButton("SizeUp", sculptGroup, "+", new Vector2(84f, 72f), () => SizeBigger?.Invoke(), out _, out Text plus);
            plus.fontSize = 36;
            Place(bigger.transform, new Vector2(0.5f, 0f), new Vector2(-190f, 80f));

            Button mirror = HudFactory.CreateButton("Mirror", sculptGroup, "Mirror", new Vector2(170f, 72f), () => MirrorToggled?.Invoke(), out mirrorBackground, out mirrorLabel);
            Place(mirror.transform, new Vector2(0.5f, 0f), new Vector2(0f, 80f));

            Button snap = HudFactory.CreateButton("AngleSnap", sculptGroup, "Snap 15", new Vector2(150f, 72f), () => SnapToggled?.Invoke(), out snapBackground, out snapLabel);
            Place(snap.transform, new Vector2(0.5f, 0f), new Vector2(-560f, 80f));

            Button undo = HudFactory.CreateButton("Undo", sculptGroup, "Undo", new Vector2(150f, 72f), () => Undo?.Invoke(), out _, out _);
            Place(undo.transform, new Vector2(0.5f, 0f), new Vector2(180f, 80f));

            Button reset = HudFactory.CreateButton("Reset", sculptGroup, "Start over", new Vector2(170f, 72f), () => Reset?.Invoke(), out _, out _);
            Place(reset.transform, new Vector2(0.5f, 0f), new Vector2(355f, 80f));

            // finish
            Button done = HudFactory.CreateButton("Done", sculptGroup, "Done", new Vector2(200f, 84f), () => Done?.Invoke(), out Image doneBackground, out Text doneLabel);
            doneBackground.color = new Color(0.25f, 0.7f, 0.4f, 0.95f);
            doneLabel.fontSize = 32;
            Place(done.transform, new Vector2(1f, 1f), new Vector2(-30f, -30f));

            // add a shape (it lands on the clay, ready to move / size / sculpt)
            Text addTitle = HudFactory.CreateText("AddTitle", sculptGroup, "Add shape", 26, TextAnchor.MiddleCenter);
            addTitle.rectTransform.sizeDelta = new Vector2(200f, 40f);
            Place(addTitle.transform, new Vector2(1f, 1f), new Vector2(-30f, -230f));

            for (int i = 0; i < Shapes.Length; i++)
            {
                ClayShape shape = Shapes[i];
                Button button = HudFactory.CreateButton("Add_" + shape, sculptGroup, "+ " + shape, new Vector2(200f, 66f), () => ShapeAdded?.Invoke(shape), out _, out _);
                Place(button.transform, new Vector2(1f, 1f), new Vector2(-30f, -280f - i * 76f));
            }

            Button delete = HudFactory.CreateButton("DeletePart", sculptGroup, "Delete shape", new Vector2(200f, 66f), () => DeletePart?.Invoke(), out Image deleteBackground, out _);
            deleteBackground.color = new Color(0.65f, 0.2f, 0.2f, 0.9f);
            Place(delete.transform, new Vector2(1f, 1f), new Vector2(-30f, -280f - Shapes.Length * 76f - 14f));
        }

        static void Place(Transform t, Vector2 anchor, Vector2 position)
        {
            var rect = (RectTransform)t;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
        }

        public void SetStudioVisible(bool visible)
        {
            studio.gameObject.SetActive(visible);
            if (visible)
            {
                openButton.gameObject.SetActive(false);
                messageLabel.text = "";
                messageTimer = 0f;
            }
        }

        // wheel = true shows the pottery wheel controls, false the sculpt bench
        public void SetStep(bool wheel)
        {
            wheelGroup.gameObject.SetActive(wheel);
            sculptGroup.gameObject.SetActive(!wheel);
            hintLabel.text = wheel ? WheelHint : SculptHint;
        }

        public void SetOpenButtonVisible(bool visible)
        {
            openButton.gameObject.SetActive(visible);
        }

        // a short note near the top of the screen, e.g. why Done didn't work
        public void ShowMessage(string message)
        {
            messageLabel.text = message;
            messageTimer = MessageSeconds;
        }

        public void Tick(float deltaTime)
        {
            if (messageTimer <= 0f) return;
            messageTimer -= deltaTime;
            if (messageTimer <= 0f) messageLabel.text = "";
        }

        public void Refresh(ClayTool tool, bool mirror, float brushRadius, string info, bool angleSnap, bool wheel)
        {
            for (int i = 0; i < Tools.Length; i++)
            {
                bool selected = Tools[i].tool == tool;
                toolBackgrounds[i].color = selected ? SelectedColor : ButtonColor;
                toolLabels[i].color = selected ? Color.black : Color.white;
                toolLabels[i].GetComponent<Outline>().enabled = !selected;
            }

            mirrorBackground.color = mirror ? SelectedColor : ButtonColor;
            mirrorLabel.color = mirror ? Color.black : Color.white;
            mirrorLabel.GetComponent<Outline>().enabled = !mirror;
            snapBackground.color = angleSnap ? SelectedColor : ButtonColor;
            snapLabel.color = angleSnap ? Color.black : Color.white;
            snapLabel.GetComponent<Outline>().enabled = !angleSnap;
            sizeLabel.text = $"Brush {brushRadius:0.00}";
            infoLabel.text = info;
            if (!wheel) hintLabel.text = tool == ClayTool.Rotate ? RotateHint : SculptHint;
        }
    }
}
