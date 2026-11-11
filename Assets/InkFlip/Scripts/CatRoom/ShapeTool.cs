using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // The glove's shaping power (it sits on the glove, not as a tool of its own, so it works in every room where the glove does). With the glove out, and not holding anything:
    //   1. Aim at the floor and press (hold) to put down one corner of a square, drag to the opposite corner, let go.
    //   2. Move the mouse (or drag) up to pull the square up into a block of the height you want, then click to place it.
    //      Right mouse / B button cancels at any point.
    // The block is solid (you can stand on it) and is something the glove can pick up, move, and make bigger or smaller.
    public class ShapeTool : MonoBehaviour
    {
        public Camera aimCamera;
        public FirstPersonController player;
        public PaintColorSelector colors;
        [Tooltip("The glove tool on the same object; it is switched off while a square is being drawn so it can't grab things.")]
        public GloveTool glove;
        [Tooltip("An unlit material for the guide lines.")]
        public Material outlineMaterial;
        [Tooltip("A lit material for the finished blocks (they take the current ink colour).")]
        public Material blockMaterial;

        [Header("Shape")]
        [Tooltip("Corners snap to this grid, in metres.")]
        public float grid = 0.25f;
        public float minSide = 0.5f;
        public float maxSide = 6f;
        public float minHeight = 0.25f;
        public float maxHeight = 4f;
        public float reach = 14f;
        [Tooltip("Metres of height for each pixel the mouse moves up.")]
        public float heightPerPixel = 0.008f;
        [Tooltip("When there are more blocks than this, the oldest one goes away.")]
        public int maxBlocks = 12;

        enum Step { Idle, Footprint, Height }

        Step step;
        Vector3 anchor;
        Vector3 corner;
        float height;
        bool disabledPlayer;
        bool disabledGlove;
        LineRenderer[] lines;
        MaterialPropertyBlock block;
        string shownPrompt = "";
        readonly List<GameObject> blocks = new List<GameObject>();

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void OnEnable()
        {
            step = Step.Idle;
        }

        void OnDisable()
        {
            Cancel();
            SetPrompt("");
        }

        void Update()
        {
            if (aimCamera == null) aimCamera = Camera.main;
            if (aimCamera == null) return;
            if (player == null) player = FindAnyObjectByType<FirstPersonController>();

            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            bool pressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
            bool held = (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightTrigger.isPressed);
            bool cancel = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);

            if (!FirstPersonController.InputCaptured || LineRoomUI.Busy)
            {
                if (step != Step.Idle) Cancel();
                SetPrompt("");
                return;
            }
            if (cancel && step != Step.Idle)
            {
                Cancel();
                return;
            }

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            switch (step)
            {
                case Step.Idle: UpdateIdle(ray, pressed); break;
                case Step.Footprint: UpdateFootprint(ray, held); break;
                case Step.Height: UpdateHeight(mouse, pad, pressed); break;
            }
        }

        float Snap(float v) => Mathf.Round(v / grid) * grid;

        void UpdateIdle(Ray ray, bool pressed)
        {
            bool allowed = !GloveTool.IsHolding; // wherever the glove works, so does this
            if (!allowed)
            {
                HideLines();
                SetPrompt("");
                return;
            }
            // the floor, or the top of something - but not a thing the glove would pick up
            if (Physics.Raycast(ray, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f
                && hit.collider.GetComponentInParent<Grabbable>() == null)
            {
                Vector3 p = new Vector3(Snap(hit.point.x), hit.point.y, Snap(hit.point.z));
                ShowBox(p, p + new Vector3(grid, 0f, grid), 0f);
                SetPrompt("Glove power: hold to draw a square here, drag to the opposite corner");
                if (pressed)
                {
                    anchor = p;
                    corner = p;
                    step = Step.Footprint;
                    if (glove != null && glove.enabled)
                    {
                        glove.enabled = false;
                        disabledGlove = true;
                    }
                }
            }
            else
            {
                HideLines();
                SetPrompt("");
            }
        }

        void UpdateFootprint(Ray ray, bool held)
        {
            var plane = new Plane(Vector3.up, anchor);
            if (plane.Raycast(ray, out float distance) && distance < reach * 2f)
            {
                Vector3 q = ray.GetPoint(distance);
                float dx = Mathf.Clamp(Snap(q.x) - anchor.x, -maxSide, maxSide);
                float dz = Mathf.Clamp(Snap(q.z) - anchor.z, -maxSide, maxSide);
                corner = new Vector3(anchor.x + dx, anchor.y, anchor.z + dz);
            }

            float sx = Mathf.Abs(corner.x - anchor.x), sz = Mathf.Abs(corner.z - anchor.z);
            ShowBox(anchor, corner, 0f);
            SetPrompt($"{sx:0.##} x {sz:0.##} m  -  let go to set the square");

            if (!held)
            {
                if (sx >= minSide && sz >= minSide)
                {
                    step = Step.Height;
                    height = 1f;
                    // the mouse now sets the height instead of turning the view
                    if (player != null && player.enabled)
                    {
                        player.enabled = false;
                        disabledPlayer = true;
                    }
                }
                else
                {
                    Cancel();
                }
            }
        }

        void UpdateHeight(Mouse mouse, Gamepad pad, bool pressed)
        {
            if (mouse != null) height += mouse.delta.ReadValue().y * heightPerPixel;
            if (pad != null) height += pad.rightStick.ReadValue().y * 3f * Time.deltaTime;
            height = Mathf.Clamp(height, minHeight, maxHeight);
            float shown = Mathf.Max(minHeight, Snap(height));

            ShowBox(anchor, corner, shown);
            SetPrompt($"Pull up to set the height ({shown:0.##} m) - click to place, right-click to cancel");

            if (pressed) Place(shown);
        }

        void Place(float blockHeight)
        {
            Vector3 min = Vector3.Min(anchor, corner);
            Vector3 max = Vector3.Max(anchor, corner);
            Vector3 size = new Vector3(max.x - min.x, blockHeight, max.z - min.z);
            Vector3 centre = new Vector3(min.x + size.x * 0.5f, anchor.y + size.y * 0.5f, min.z + size.z * 0.5f);

            // don't trap the player inside it
            CharacterController body = player != null ? player.GetComponent<CharacterController>() : null;
            if (body != null && body.bounds.Intersects(new Bounds(centre, size)))
            {
                LineRoomUI.Instance.Say("Step out of the way first!", 1.5f);
                return;
            }

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "ShapedBlock";
            cube.transform.position = centre;
            cube.transform.localScale = size;
            Color color = colors != null ? colors.CurrentColor : Color.white;
            var material = new Material(blockMaterial != null ? blockMaterial : cube.GetComponent<Renderer>().sharedMaterial);
            material.color = color;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            cube.GetComponent<Renderer>().sharedMaterial = material;
            cube.AddComponent<Grabbable>(); // after the scale is set, so the glove resizes from here

            blocks.Add(cube);
            blocks.RemoveAll(b => b == null);
            while (blocks.Count > maxBlocks)
            {
                Destroy(blocks[0]);
                blocks.RemoveAt(0);
            }

            Cancel();
        }

        void Cancel()
        {
            step = Step.Idle;
            if (disabledPlayer)
            {
                if (player != null) player.enabled = true;
                disabledPlayer = false;
            }
            if (disabledGlove)
            {
                if (glove != null) glove.enabled = true;
                disabledGlove = false;
            }
            HideLines();
        }

        // ---------- guide lines ----------

        void EnsureLines()
        {
            if (lines != null) return;
            lines = new LineRenderer[6];
            for (int i = 0; i < lines.Length; i++)
            {
                var go = new GameObject("ShapeLine" + i);
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.widthMultiplier = 0.035f;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (outlineMaterial != null) line.sharedMaterial = outlineMaterial;
                lines[i] = line;
            }
            block = new MaterialPropertyBlock();
        }

        void HideLines()
        {
            if (lines == null) return;
            foreach (LineRenderer line in lines) line.enabled = false;
        }

        // 'height' of 0 draws only the square on the floor
        void ShowBox(Vector3 a, Vector3 b, float boxHeight)
        {
            EnsureLines();
            Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
            float y0 = a.y + 0.01f;
            float y1 = y0 + boxHeight;

            Vector3[] bottom = { new Vector3(min.x, y0, min.z), new Vector3(max.x, y0, min.z), new Vector3(max.x, y0, max.z), new Vector3(min.x, y0, max.z) };
            SetLoop(lines[0], bottom, 0f);
            bool tall = boxHeight > 0.001f;
            Vector3[] top = new Vector3[4];
            for (int i = 0; i < 4; i++) top[i] = bottom[i] + Vector3.up * boxHeight;
            SetLoop(lines[1], top, 0f);
            lines[1].enabled = tall;
            for (int i = 0; i < 4; i++)
            {
                LineRenderer pole = lines[2 + i];
                pole.enabled = tall;
                pole.positionCount = 2;
                pole.SetPosition(0, bottom[i]);
                pole.SetPosition(1, top[i]);
            }

            Color color = colors != null ? Color.Lerp(colors.CurrentColor, Color.white, 0.35f) : Color.white;
            foreach (LineRenderer line in lines)
            {
                line.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                line.SetPropertyBlock(block);
            }
        }

        static void SetLoop(LineRenderer line, Vector3[] corners, float lift)
        {
            line.enabled = true;
            line.positionCount = 5;
            for (int i = 0; i < 5; i++) line.SetPosition(i, corners[i % 4] + Vector3.up * lift);
        }

        void SetPrompt(string text)
        {
            if (shownPrompt == text) return;
            shownPrompt = text;
            LineRoomUI.Instance.SetPrompt(this, text);
        }
    }
}
