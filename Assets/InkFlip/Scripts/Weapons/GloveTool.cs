using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // A big soft glove for rearranging the room.
    //  - Look at something grabbable (it gets an outline) and hold left mouse / right trigger to pick it up.
    //  - It follows the crosshair and sets itself down on whatever you're looking at - floor, table, wall.
    //    Look at nothing and it floats out in front of you; let go and it drops to the floor.
    //  - While holding, the mouse wheel (or gamepad shoulders) makes it larger or smaller.
    // Only objects with a Grabbable component can be picked up.
    [DefaultExecutionOrder(-50)] // runs before the colour selector, so it can claim the wheel while holding
    public class GloveTool : MonoBehaviour
    {
        public Camera aimCamera;
        public OutlineHighlighter highlighter;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;
        [Tooltip("The glove viewmodel; it reaches forward a little while carrying.")]
        public Transform gloveModel;

        [Header("Reach")]
        public float reach = 8f;
        public float minHoldDistance = 1.2f;
        public float maxHoldDistance = 10f;
        [Tooltip("How quickly a carried object catches up with the crosshair (higher = snappier).")]
        public float followSharpness = 18f;

        [Header("Resize")]
        [Tooltip("Size change per notch of the mouse wheel.")]
        public float scaleStepPerNotch = 1.1f;
        [Tooltip("Growth per second while a gamepad shoulder is held.")]
        public float gamepadScaleRate = 1.2f;

        // true while something is being carried - other scripts use this to leave the wheel alone
        public static bool IsHolding { get; private set; }

        Grabbable hovered;
        Grabbable carried;
        float holdDistance;
        bool floating;
        bool waitForRelease;
        Vector3 restLocalPosition;
        bool restCached;

        void OnEnable()
        {
            // switching to the glove while a button is held shouldn't grab straight away
            waitForRelease = true;
        }

        void OnDisable()
        {
            if (carried != null) Drop();
            ClearHover();
            IsHolding = false;
        }

        void Start()
        {
            if (aimCamera == null) aimCamera = Camera.main;
            CacheRest();
        }

        void CacheRest()
        {
            if (restCached || gloveModel == null) return;
            restLocalPosition = gloveModel.localPosition;
            restCached = true;
        }

        void Update()
        {
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            bool down = (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightTrigger.isPressed);
            bool pressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);

            AnimateGlove();

            // the click that captures the mouse shouldn't also pick something up
            if (!FirstPersonController.InputCaptured)
            {
                if (carried != null) Drop();
                ClearHover();
                waitForRelease = true;
                return;
            }

            if (carried != null)
            {
                if (!down) Drop();
                else Carry(mouse, pad);
                return;
            }

            UpdateHover();

            if (waitForRelease)
            {
                if (!down) waitForRelease = false;
                return;
            }

            if (pressed && hovered != null) PickUp(hovered);
        }

        // ---------- picking ----------

        void UpdateHover()
        {
            Grabbable target = null;
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, reach, hitMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<Grabbable>();
            }

            // re-apply if something else (like the pencil) cleared the outline
            bool outlineMissing = target != null && highlighter != null && highlighter.Current != target.Primary;
            if (target == hovered && !outlineMissing) return;

            hovered = target;
            if (highlighter == null) return;
            if (hovered != null && hovered.Primary != null) highlighter.Highlight(hovered.Primary);
            else highlighter.Clear();
        }

        void ClearHover()
        {
            if (hovered != null && highlighter != null) highlighter.Clear();
            hovered = null;
        }

        void PickUp(Grabbable target)
        {
            carried = target;
            hovered = null;
            floating = false;
            IsHolding = true;

            Bounds bounds = carried.GetBounds();
            holdDistance = Mathf.Clamp(Vector3.Distance(aimCamera.transform.position, bounds.center), minHoldDistance, maxHoldDistance);

            carried.SetCollidersEnabled(false);
            if (highlighter != null && carried.Primary != null) highlighter.Highlight(carried.Primary, pulsing: false);
        }

        // ---------- carrying ----------

        void Carry(Mouse mouse, Gamepad pad)
        {
            float dt = Time.deltaTime;

            // resize
            float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (scroll > 0.01f) carried.ScaleBy(scaleStepPerNotch);
            else if (scroll < -0.01f) carried.ScaleBy(1f / scaleStepPerNotch);
            if (pad != null)
            {
                if (pad.rightShoulder.isPressed) carried.ScaleBy(Mathf.Exp(gamepadScaleRate * dt));
                if (pad.leftShoulder.isPressed) carried.ScaleBy(Mathf.Exp(-gamepadScaleRate * dt));
            }

            // aim: rest on whatever is under the crosshair, or float out in front
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Bounds bounds = carried.GetBounds();
            Vector3 pivotToCentre = bounds.center - carried.transform.position;

            Vector3 targetCentre;
            if (Physics.Raycast(ray, out RaycastHit hit, reach, hitMask, QueryTriggerInteraction.Ignore))
            {
                // how far the object's box reaches along the surface normal, so it sits against the surface
                Vector3 n = hit.normal;
                Vector3 e = bounds.extents;
                float extent = Mathf.Abs(n.x) * e.x + Mathf.Abs(n.y) * e.y + Mathf.Abs(n.z) * e.z;
                targetCentre = hit.point + n * extent;
                floating = false;
            }
            else
            {
                targetCentre = ray.GetPoint(holdDistance);
                floating = true;
            }

            Vector3 target = targetCentre - pivotToCentre;
            carried.transform.position = Vector3.Lerp(carried.transform.position, target, 1f - Mathf.Exp(-followSharpness * dt));
        }

        void Drop()
        {
            if (carried == null) return;

            // floating in mid-air: let it fall to whatever is below (colliders are still off, so it can't hit itself)
            if (floating)
            {
                Bounds bounds = carried.GetBounds();
                if (Physics.Raycast(bounds.center, Vector3.down, out RaycastHit hit, 50f, hitMask, QueryTriggerInteraction.Ignore))
                {
                    carried.transform.position += Vector3.down * (bounds.min.y - hit.point.y);
                }
            }

            carried.SetCollidersEnabled(true);
            carried = null;
            IsHolding = false;
            if (highlighter != null) highlighter.Clear();
        }

        // the glove reaches forward a little while carrying
        void AnimateGlove()
        {
            if (gloveModel == null) return;
            CacheRest();
            Vector3 goal = restLocalPosition + (carried != null ? new Vector3(0f, 0.015f, 0.07f) : Vector3.zero);
            gloveModel.localPosition = Vector3.Lerp(gloveModel.localPosition, goal, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
    }
}
