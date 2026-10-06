using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // The old draw board's brushes, now painting onto 3D objects.
    public enum DrawBrush
    {
        Big,
        Medium,
        Small,
        Watercolor,
        Watercolor2,
        GelGlitter,
        Eraser,
    }

    // Entered by clicking an object with the pencil. The camera locks, the cursor is
    // freed, and you paint under the mouse with the draw-board brushes - onto the
    // selected object only (unless paintOnlySelected is turned off).
    //   Drag: paint    Scroll: brush size    1-9 / click swatch: colour    Esc / Done: leave
    public class SelectionDrawMode : MonoBehaviour
    {
        public Camera drawCamera;
        public FirstPersonController player;
        public ToolSwitcher tools;
        public PaintColorSelector colors;
        public OutlineHighlighter highlighter;

        [Tooltip("Only the selected object takes paint. Off = anything under the cursor.")]
        public bool paintOnlySelected = true;

        [Header("Brush")]
        public DrawBrush brush = DrawBrush.Medium;
        public float sizeMultiplier = 1f;
        public float minSizeMultiplier = 0.3f;
        public float maxSizeMultiplier = 4f;
        public float sizeScrollStep = 0.1f;
        [Tooltip("Safety cap on dabs laid per frame during very fast drags.")]
        public int maxDabsPerFrame = 64;

        public bool IsActive { get; private set; }
        public Paintable Target { get; private set; }

        public event Action<bool> ModeChanged;
        public event Action BrushChanged;

        Collider[] targetColliders;
        bool stroking;
        bool waitForRelease;
        Vector2 lastScreenPoint;
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();

        // brush radius in screen pixels (at 1080p, scaled to the current resolution)
        public float BrushPixelRadius => BasePixelRadius(brush) * sizeMultiplier * (Screen.height / 1080f);

        public Color CurrentColor => colors != null ? colors.CurrentColor : Color.black;

        public void Enter(Paintable target)
        {
            if (IsActive || target == null) return;

            Target = target;
            targetColliders = target.GetComponentsInChildren<Collider>();
            IsActive = true;
            stroking = false;
            waitForRelease = true; // the click that selected the object shouldn't paint a dot

            if (player != null) player.enabled = false;   // camera locked in place
            if (tools != null) tools.enabled = false;     // no tool swapping mid-drawing

            // hide the held tool so it doesn't cover what you're drawing on
            hiddenRenderers.Clear();
            if (drawCamera != null)
            {
                foreach (Renderer r in drawCamera.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    r.enabled = false;
                    hiddenRenderers.Add(r);
                }
            }

            if (highlighter != null) highlighter.Highlight(target.Renderer, pulsing: false);
            FirstPersonController.SetCursorCaptured(false);
            ModeChanged?.Invoke(true);
        }

        public void Exit()
        {
            if (!IsActive) return;

            IsActive = false;
            stroking = false;
            Target = null;
            targetColliders = null;

            foreach (Renderer r in hiddenRenderers)
            {
                if (r != null) r.enabled = true;
            }
            hiddenRenderers.Clear();

            if (highlighter != null) highlighter.Clear();
            if (player != null) player.enabled = true;
            if (tools != null) tools.enabled = true;
            FirstPersonController.SetCursorCaptured(true);
            ModeChanged?.Invoke(false);
        }

        public void SetBrush(DrawBrush newBrush)
        {
            brush = newBrush;
            BrushChanged?.Invoke();
        }

        public void ChangeSize(float delta)
        {
            sizeMultiplier = Mathf.Clamp(sizeMultiplier + delta, minSizeMultiplier, maxSizeMultiplier);
            BrushChanged?.Invoke();
        }

        void Update()
        {
            if (!IsActive) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            {
                FirstPersonController.ConsumeEscape();
                Exit();
                return;
            }

            if (Target == null) { Exit(); return; } // selected object was destroyed
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.01f) ChangeSize(sizeScrollStep);
            else if (scroll < -0.01f) ChangeSize(-sizeScrollStep);

            bool held = mouse.leftButton.isPressed;
            Vector2 position = mouse.position.ReadValue();

            if (waitForRelease)
            {
                if (!held) waitForRelease = false;
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // clicks on the toolbar / swatches are UI, not paint
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                stroking = !overUI;
                if (stroking)
                {
                    lastScreenPoint = position;
                    Dab(position);
                }
            }
            else if (held && stroking)
            {
                DragTo(position);
            }

            if (!held) stroking = false;
        }

        // lay dabs at even screen-space spacing between last frame's cursor and this one's,
        // so a fast drag is still a continuous line - and it follows the surface under the cursor
        void DragTo(Vector2 position)
        {
            float spacing = Mathf.Max(1f, BrushPixelRadius * SpacingFactor(brush));
            Vector2 delta = position - lastScreenPoint;
            float distance = delta.magnitude;
            if (distance < spacing) return;

            Vector2 direction = delta / distance;
            int steps = Mathf.Min(Mathf.FloorToInt(distance / spacing), maxDabsPerFrame);
            for (int i = 1; i <= steps; i++)
            {
                Dab(lastScreenPoint + direction * (spacing * i));
            }
            lastScreenPoint = steps == maxDabsPerFrame ? position : lastScreenPoint + direction * (spacing * steps);
        }

        void Dab(Vector2 screenPoint)
        {
            PaintManager manager = PaintManager.Instance;
            if (manager == null || drawCamera == null) return;

            Ray ray = drawCamera.ScreenPointToRay(screenPoint);
            if (!RaycastTarget(ray, out RaycastHit hit)) return;

            // convert the on-screen brush size to world units at the distance being painted
            float worldPerPixel = 2f * hit.distance * Mathf.Tan(drawCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            float radius = BrushPixelRadius * worldPerPixel;
            Paintable only = paintOnlySelected ? Target : null;

            if (brush == DrawBrush.Eraser)
            {
                manager.EraseDab(hit.point, hit.normal, radius, 0.8f, 1f, only);
                return;
            }

            Color color = CurrentColor;
            if (brush == DrawBrush.Watercolor) color = ApplyColorDynamics(color, 0.015f, 0.08f, 0.08f);
            else if (brush == DrawBrush.Watercolor2) color = ApplyColorDynamics(color, 0.03f, 0.15f, 0.15f);

            manager.PaintDab(hit.point, hit.normal, radius, color, StyleFor(brush), only);
        }

        bool RaycastTarget(Ray ray, out RaycastHit closest)
        {
            closest = default;
            if (!paintOnlySelected)
            {
                return Physics.Raycast(ray, out closest, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            }

            bool found = false;
            foreach (Collider c in targetColliders)
            {
                if (c != null && c.enabled && c.Raycast(ray, out RaycastHit hit, 100f) && (!found || hit.distance < closest.distance))
                {
                    closest = hit;
                    found = true;
                }
            }
            return found;
        }

        // Brush presets, tuned to feel like the old 2D draw board's brushes.
        static float BasePixelRadius(DrawBrush b)
        {
            switch (b)
            {
                case DrawBrush.Big: return 26f;
                case DrawBrush.Medium: return 13f;
                case DrawBrush.Small: return 6f;
                case DrawBrush.Watercolor: return 34f;
                case DrawBrush.Watercolor2: return 44f;
                case DrawBrush.GelGlitter: return 22f;
                case DrawBrush.Eraser: return 20f;
                default: return 13f;
            }
        }

        // distance between dabs as a fraction of the brush radius (same idea as the old dabSpacingFactor)
        static float SpacingFactor(DrawBrush b)
        {
            switch (b)
            {
                case DrawBrush.Watercolor: return 0.28f;
                case DrawBrush.Watercolor2: return 0.25f;
                case DrawBrush.GelGlitter: return 0.4f;
                default: return 0.2f;
            }
        }

        static DabStyle StyleFor(DrawBrush b)
        {
            switch (b)
            {
                // soft, translucent, grainy wash - builds up with repeated passes
                case DrawBrush.Watercolor:
                    return new DabStyle { hardness = 0.1f, strength = 0.12f, grain = 0.35f };
                // heavier, bigger wash: much stronger paper grain (colour jitter is stronger too, see Dab)
                case DrawBrush.Watercolor2:
                    return new DabStyle { hardness = 0.05f, strength = 0.12f, grain = 0.65f };
                case DrawBrush.GelGlitter:
                    return new DabStyle { hardness = 0.9f, strength = 1f, glitter = 0.12f };
                default: // Big / Medium / Small pens: clean, hard, opaque
                    return new DabStyle { hardness = 0.95f, strength = 1f };
            }
        }

        // Small random HSV drift off the base colour so consecutive dabs aren't perfectly
        // flat (ported from the old DrawingPad.ApplyColorDynamics).
        static Color ApplyColorDynamics(Color baseColor, float hueJitter, float saturationJitter, float brightnessJitter)
        {
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            h = Mathf.Repeat(h + UnityEngine.Random.Range(-hueJitter, hueJitter), 1f);
            s = Mathf.Clamp01(s + UnityEngine.Random.Range(-saturationJitter, saturationJitter));
            v = Mathf.Clamp01(v + UnityEngine.Random.Range(-brightnessJitter, brightnessJitter));
            Color result = Color.HSVToRGB(h, s, v);
            result.a = baseColor.a;
            return result;
        }
    }
}
