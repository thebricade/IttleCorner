using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // Look at a paintable object within reach and it gets an outline; click it
    // (left mouse / right trigger) to enter draw mode on that object.
    public class PencilTool : MonoBehaviour
    {
        public Camera aimCamera;
        public OutlineHighlighter highlighter;
        public SelectionDrawMode drawMode;
        public float reach = 4f;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;

        Paintable hovered;
        bool waitForRelease;

        void OnEnable()
        {
            waitForRelease = true;
        }

        void OnDisable()
        {
            ClearHover();
        }

        void Start()
        {
            if (aimCamera == null) aimCamera = Camera.main;
        }

        void Update()
        {
            if (drawMode != null && drawMode.IsActive) return;

            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            bool held = (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightTrigger.isPressed);

            if (!FirstPersonController.InputCaptured)
            {
                ClearHover();
                waitForRelease = true;
                return;
            }

            UpdateHover();

            if (waitForRelease)
            {
                if (!held) waitForRelease = false;
                return;
            }

            bool clicked = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
            if (clicked && hovered != null && drawMode != null)
            {
                Paintable target = hovered;
                ClearHover();
                drawMode.Enter(target);
            }
        }

        void UpdateHover()
        {
            Paintable target = null;
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, reach, hitMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<Paintable>();
            }

            // re-apply if something else (like leaving draw mode) cleared the outline
            bool outlineMissing = target != null && highlighter != null && highlighter.Current != target.Renderer;
            if (target == hovered && !outlineMissing) return;

            hovered = target;
            if (highlighter == null) return;
            if (hovered != null) highlighter.Highlight(hovered.Renderer);
            else highlighter.Clear();
        }

        void ClearHover()
        {
            if (hovered != null && highlighter != null && !(drawMode != null && drawMode.IsActive)) highlighter.Clear();
            hovered = null;
        }
    }
}
