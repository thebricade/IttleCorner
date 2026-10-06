using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    [Serializable]
    public struct InkColor
    {
        public string name;
        public Color color;

        public InkColor(string name, string hex)
        {
            this.name = name;
            ColorUtility.TryParseHtmlString(hex, out color);
        }
    }

    // Owns the palette and which ink is selected.
    //   1-9: pick a colour    Mouse wheel / Q E / gamepad shoulders: previous / next
    public class PaintColorSelector : MonoBehaviour
    {
        public InkColor[] palette =
        {
            new InkColor("Bubblegum", "#FF3D8B"),
            new InkColor("Tangerine", "#FF8A1F"),
            new InkColor("Sunflower", "#FFD21A"),
            new InkColor("Lime", "#7ED321"),
            new InkColor("Teal", "#16C6B0"),
            new InkColor("Sky", "#2D8CFF"),
            new InkColor("Violet", "#8A4DFF"),
            new InkColor("Chalk", "#F4F1EA"),
            new InkColor("Charcoal", "#2B2B30"),
        };

        public int SelectedIndex { get; private set; }
        public InkColor Current => palette[SelectedIndex];
        public Color CurrentColor => palette[SelectedIndex].color;

        public event Action<int> SelectionChanged;

        static readonly Key[] NumberKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        void Update()
        {
            if (palette == null || palette.Length == 0) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int i = 0; i < NumberKeys.Length && i < palette.Length; i++)
                {
                    if (keyboard[NumberKeys[i]].wasPressedThisFrame) Select(i);
                }
                if (keyboard.qKey.wasPressedThisFrame) Step(-1);
                if (keyboard.eKey.wasPressedThisFrame) Step(1);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && FirstPersonController.InputCaptured)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.01f) Step(-1);
                else if (scroll < -0.01f) Step(1);
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.leftShoulder.wasPressedThisFrame) Step(-1);
                if (pad.rightShoulder.wasPressedThisFrame) Step(1);
            }
        }

        public void Step(int direction)
        {
            int count = palette.Length;
            Select(((SelectedIndex + direction) % count + count) % count);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= palette.Length || index == SelectedIndex) return;
            SelectedIndex = index;
            SelectionChanged?.Invoke(index);
        }
    }
}
