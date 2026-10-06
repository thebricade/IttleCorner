using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    [Serializable]
    public struct PaintTool
    {
        public string name;
        [Tooltip("Short controls reminder shown on the HUD.")]
        public string controls;
        public GameObject root;
    }

    // Holds the player's tools and shows exactly one at a time.
    //   Z / X / C: jump straight to tool 1 / 2 / 3
    //   Tab / middle mouse / gamepad Y: next tool    Shift+Tab: previous tool
    public class ToolSwitcher : MonoBehaviour
    {
        public PaintTool[] tools;

        [Tooltip("Direct-select key for each tool, in the same order as Tools.")]
        public Key[] directKeys = { Key.Z, Key.X, Key.C };

        public int ActiveIndex { get; private set; }
        public PaintTool Active => tools[ActiveIndex];

        public event Action<int> ToolChanged;

        // e.g. "Z" for the HUD, or "" if the tool has no direct key
        public string KeyLabel(int index)
        {
            return directKeys != null && index < directKeys.Length && directKeys[index] != Key.None
                ? directKeys[index].ToString()
                : "";
        }

        void Start()
        {
            for (int i = 0; i < tools.Length; i++)
            {
                if (tools[i].root != null) tools[i].root.SetActive(i == ActiveIndex);
            }
            ToolChanged?.Invoke(ActiveIndex);
        }

        void Update()
        {
            if (tools == null || tools.Length < 2) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;

            if (keyboard != null && directKeys != null)
            {
                for (int i = 0; i < directKeys.Length && i < tools.Length; i++)
                {
                    if (directKeys[i] != Key.None && keyboard[directKeys[i]].wasPressedThisFrame)
                    {
                        Select(i);
                        return;
                    }
                }
            }

            bool shift = keyboard != null && keyboard.shiftKey.isPressed;
            bool tab = keyboard != null && keyboard.tabKey.wasPressedThisFrame;
            bool next = (tab && !shift)
                        || (mouse != null && mouse.middleButton.wasPressedThisFrame && FirstPersonController.InputCaptured)
                        || (pad != null && pad.buttonNorth.wasPressedThisFrame);

            if (next) Select((ActiveIndex + 1) % tools.Length);
            else if (tab && shift) Select((ActiveIndex - 1 + tools.Length) % tools.Length);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= tools.Length || index == ActiveIndex) return;

            if (tools[ActiveIndex].root != null) tools[ActiveIndex].root.SetActive(false);
            ActiveIndex = index;
            if (tools[ActiveIndex].root != null) tools[ActiveIndex].root.SetActive(true);
            ToolChanged?.Invoke(ActiveIndex);
        }
    }
}
