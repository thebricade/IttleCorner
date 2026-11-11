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
        [Tooltip("The ability a room must allow for this tool to be usable. None = always usable.")]
        public Ability requires;
    }

    // Holds the player's tools and shows exactly one at a time.
    //   Z / X / C / V: jump straight to tool 1 / 2 / 3 / 4
    //   Tab / middle mouse / gamepad Y: next tool    Shift+Tab: previous tool
    // Tools the current room doesn't allow are skipped, can't be selected, and show as locked on the HUD.
    // If the active tool becomes locked (the player walks into a stricter room) it swaps to one that is allowed,
    // or to empty hands if none are.
    public class ToolSwitcher : MonoBehaviour
    {
        public PaintTool[] tools;

        [Tooltip("Direct-select key for each tool, in the same order as Tools.")]
        public Key[] directKeys = { Key.Z, Key.X, Key.C, Key.V };

        public int ActiveIndex { get; private set; }
        public PaintTool Active => tools[ActiveIndex];

        // true when no tool is allowed here, so nothing is held
        public bool HandsEmpty { get; private set; }

        public event Action<int> ToolChanged;
        // fired when the player tries to pick a tool this room doesn't allow
        public event Action<int> ToolBlocked;

        public bool IsAllowed(int index)
        {
            return index >= 0 && index < tools.Length && Abilities.Can(tools[index].requires);
        }

        // e.g. "Z" for the HUD, or "" if the tool has no direct key
        public string KeyLabel(int index)
        {
            return directKeys != null && index < directKeys.Length && directKeys[index] != Key.None
                ? directKeys[index].ToString()
                : "";
        }

        void Start()
        {
            Abilities.Changed += OnAbilitiesChanged;
            ApplyAllowed();
        }

        void OnDestroy()
        {
            Abilities.Changed -= OnAbilitiesChanged;
        }

        void OnAbilitiesChanged()
        {
            ApplyAllowed();
        }

        // make the held tool match what's allowed right now
        void ApplyAllowed()
        {
            if (tools == null || tools.Length == 0) return;

            int target = IsAllowed(ActiveIndex) ? ActiveIndex : NextAllowed(ActiveIndex, 1);
            HandsEmpty = target < 0;
            if (!HandsEmpty) ActiveIndex = target;

            for (int i = 0; i < tools.Length; i++)
            {
                if (tools[i].root != null) tools[i].root.SetActive(!HandsEmpty && i == ActiveIndex);
            }
            ToolChanged?.Invoke(ActiveIndex);
        }

        // the next allowed tool in this direction, or -1 if none (the current one counts last)
        int NextAllowed(int from, int direction)
        {
            for (int step = 1; step <= tools.Length; step++)
            {
                int index = ((from + direction * step) % tools.Length + tools.Length) % tools.Length;
                if (IsAllowed(index)) return index;
            }
            return -1;
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

            if (next) SelectNeighbour(1);
            else if (tab && shift) SelectNeighbour(-1);
        }

        void SelectNeighbour(int direction)
        {
            int target = NextAllowed(ActiveIndex, direction);
            if (target >= 0 && target != ActiveIndex) Select(target);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= tools.Length) return;
            if (!IsAllowed(index))
            {
                ToolBlocked?.Invoke(index);
                return;
            }
            if (index == ActiveIndex && !HandsEmpty) return;

            if (tools[ActiveIndex].root != null) tools[ActiveIndex].root.SetActive(false);
            ActiveIndex = index;
            HandsEmpty = false;
            if (tools[ActiveIndex].root != null) tools[ActiveIndex].root.SetActive(true);
            ToolChanged?.Invoke(ActiveIndex);
        }
    }
}
