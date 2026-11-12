using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // A wide paint brush you sweep across surfaces instead of shooting.
    //  - Hold left mouse / right trigger with a surface in reach, then sweep the mouse
    //    (or strafe) to drag the brush. Strokes leave bristle streaks along the sweep.
    //  - The brush carries a limited load of paint: long strokes go dry and streaky.
    //    Lift the brush (release) to reload.
    //  - The brush head trails behind fast mouse movement, so quick flicks paint
    //    sweeping arcs and the viewmodel leans into the motion.
    public class PaintBrush : MonoBehaviour
    {
        public Camera aimCamera;
        public PaintColorSelector colors;
        [Tooltip("The part of the viewmodel that tilts and sways while brushing.")]
        public Transform brushModel;
        [Tooltip("Renderers tinted with the ink (the bristles). They fade toward dry as the load runs out.")]
        public Renderer[] bristleRenderers;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;

        [Header("Stroke")]
        public float reach = 2.6f;
        public float brushWidth = 0.4f;
        public float bristlesPerMeter = 45f;
        [Tooltip("Minimum distance the brush must travel before laying down the next stroke segment.")]
        public float segmentSpacing = 0.03f;
        [Tooltip("If the brush contact jumps further than this in one frame (e.g. onto another surface), a new stroke starts instead of smearing across the gap.")]
        public float maxSegmentLength = 0.6f;

        [Header("Paint load")]
        [Tooltip("Metres of stroke a full brush can paint before it runs dry.")]
        public float metersPerLoad = 6f;
        [Tooltip("Seconds to fully reload once the brush is lifted.")]
        public float refillSeconds = 0.5f;
        public Color dryBristleColor = new Color(0.55f, 0.5f, 0.45f);

        [Header("Sweep feel")]
        [Tooltip("How far (in screen fractions) the brush head trails per pixel of mouse movement.")]
        public float swayPerPixel = 0.0007f;
        public float maxSway = 0.12f;
        [Tooltip("How quickly the trailing brush head catches back up to the crosshair.")]
        public float swayReturnSpeed = 7f;
        [Tooltip("Degrees the viewmodel rotates per screen fraction of sway.")]
        public float swayRotation = 180f;
        public float pressTilt = 18f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        float load = 1f;
        bool strokeActive;
        bool waitForRelease;
        Vector3 lastPoint;
        Vector3 lastDirection;
        float strokeSeed;
        Vector2 sway;
        float press;
        Quaternion restRotation;
        Vector3 restPosition;
        MaterialPropertyBlock tintBlock;
        Color lastTint;

        Color CurrentColor => colors != null ? colors.CurrentColor : Color.magenta;

        // 0-1, how much paint is left on the brush (for HUD use)
        public float Load => load;

        void Awake()
        {
            if (brushModel != null)
            {
                restRotation = brushModel.localRotation;
                restPosition = brushModel.localPosition;
            }
        }

        void OnEnable()
        {
            // switching to the brush while a button is held shouldn't start a stroke
            waitForRelease = true;
            strokeActive = false;
            UpdateTint(force: true);
        }

        void OnDisable()
        {
            strokeActive = false;
        }

        void Start()
        {
            if (aimCamera == null) aimCamera = Camera.main;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            bool held = (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightTrigger.isPressed);
            bool captured = FirstPersonController.InputCaptured;

            UpdateSway(mouse, pad, captured, dt);

            bool painting = false;
            if (!captured)
            {
                waitForRelease = true;
            }
            else if (waitForRelease)
            {
                if (!held) waitForRelease = false;
            }
            else if (held)
            {
                painting = Brush();
            }

            if (!held)
            {
                strokeActive = false;
                load = Mathf.MoveTowards(load, 1f, dt / Mathf.Max(0.01f, refillSeconds));
            }

            AnimateModel(painting, dt);
            UpdateTint(force: false);
        }

        // the brush head trails behind look movement, then springs back to the crosshair
        void UpdateSway(Mouse mouse, Gamepad pad, bool captured, float dt)
        {
            if (captured)
            {
                Vector2 lookDelta = Vector2.zero;
                if (mouse != null) lookDelta += mouse.delta.ReadValue();
                if (pad != null) lookDelta += pad.rightStick.ReadValue() * 25f;
                sway -= lookDelta * swayPerPixel;
            }
            sway = Vector2.ClampMagnitude(sway, maxSway);
            sway = Vector2.Lerp(sway, Vector2.zero, 1f - Mathf.Exp(-swayReturnSpeed * dt));
        }

        bool Brush()
        {
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f + sway.x, 0.5f + sway.y, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, hitMask, QueryTriggerInteraction.Ignore))
            {
                strokeActive = false;
                return false;
            }

            PaintManager manager = PaintManager.Instance;
            if (manager == null) return false;

            float radius = brushWidth * 0.5f;

            if (!strokeActive)
            {
                strokeActive = true;
                strokeSeed = Random.Range(0f, 1000f);
                lastPoint = hit.point;
                // until the brush moves, assume a horizontal sweep
                lastDirection = Vector3.ProjectOnPlane(aimCamera.transform.right, hit.normal).normalized;
                manager.PaintStroke(hit.point, hit.point, hit.normal, lastDirection, radius, CurrentColor, load, bristlesPerMeter, strokeSeed);
                return true;
            }

            Vector3 delta = hit.point - lastPoint;
            float distance = delta.magnitude;
            if (distance < segmentSpacing) return true;

            if (distance > maxSegmentLength)
            {
                // jumped to a different surface - start a fresh stroke there rather than bridging the gap
                lastPoint = hit.point;
                strokeSeed = Random.Range(0f, 1000f);
                return true;
            }

            // smooth the direction a little so bristle streaks don't kink on every jittery sample
            lastDirection = Vector3.Slerp(lastDirection, delta / distance, 0.6f);
            load = Mathf.Max(0f, load - distance / Mathf.Max(0.01f, metersPerLoad));

            manager.PaintStroke(lastPoint, hit.point, hit.normal, lastDirection, radius, CurrentColor, load, bristlesPerMeter, strokeSeed);
            lastPoint = hit.point;
            return true;
        }

        void AnimateModel(bool painting, float dt)
        {
            if (brushModel == null) return;

            press = Mathf.MoveTowards(press, painting ? 1f : 0f, dt * 8f);

            Quaternion swayTilt = Quaternion.Euler(-sway.y * swayRotation, sway.x * swayRotation, -sway.x * swayRotation * 0.5f);
            Quaternion pressLean = Quaternion.Euler(-press * pressTilt, 0f, 0f);
            brushModel.localRotation = restRotation * swayTilt * pressLean;
            brushModel.localPosition = restPosition
                                       + new Vector3(sway.x, sway.y, 0f) * 0.35f
                                       + Vector3.forward * (press * 0.06f);
        }

        void UpdateTint(bool force)
        {
            if (bristleRenderers == null) return;

            // bristles look less saturated as the paint runs out
            Color tint = Color.Lerp(dryBristleColor, CurrentColor, Mathf.Lerp(0.35f, 1f, load));
            if (!force && Mathf.Abs(tint.r - lastTint.r) + Mathf.Abs(tint.g - lastTint.g) + Mathf.Abs(tint.b - lastTint.b) < 0.01f) return;
            lastTint = tint;

            tintBlock ??= new MaterialPropertyBlock();
            foreach (Renderer bristles in bristleRenderers)
            {
                if (bristles == null) continue;
                bristles.GetPropertyBlock(tintBlock);
                tintBlock.SetColor(BaseColorId, tint);
                bristles.SetPropertyBlock(tintBlock);
            }
        }
    }
}
