using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // Two ways to apply ink:
    //  - Blaster (left mouse / right trigger): Splatoon-style arcing blobs that splat on impact.
    //  - Roller  (right mouse / left trigger): House Flipper-style precise painting at the
    //    crosshair, within arm's reach. Good for edges and finishing a wall cleanly.
    public class InkGun : MonoBehaviour
    {
        public Camera aimCamera;
        public Transform muzzle;
        public PaintColorSelector colors;

        [Tooltip("Renderers tinted with the current ink colour (the gun's ink tank etc).")]
        public Renderer[] tintRenderers;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;

        [Header("Blaster")]
        public float fireRate = 12f;
        public float projectileSpeed = 26f;
        public float projectileGravity = 14f;
        public float spreadDegrees = 2.5f;
        public float splatRadius = 0.55f;
        public float projectileSize = 0.12f;
        public float projectileLifetime = 3f;
        public Mesh projectileMesh;
        public Material projectileMaterial;

        [Header("Roller")]
        public float rollerReach = 4f;
        public float rollerRadius = 0.3f;
        [Tooltip("Distance between roller stamps, as a fraction of the roller radius.")]
        public float rollerSpacing = 0.3f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        float fireCooldown;
        bool waitForRelease;
        bool rollerWasDown;
        Vector3 lastRollerPoint;
        MaterialPropertyBlock tintBlock;

        Color CurrentColor => colors != null ? colors.CurrentColor : Color.magenta;

        void OnEnable()
        {
            if (colors != null) colors.SelectionChanged += OnColorChanged;
            // switching to the gun while a button is held shouldn't fire straight away
            waitForRelease = true;
            ApplyTint(CurrentColor);
        }

        void OnDisable()
        {
            if (colors != null) colors.SelectionChanged -= OnColorChanged;
            rollerWasDown = false;
        }

        void Start()
        {
            if (aimCamera == null) aimCamera = Camera.main;
        }

        void Update()
        {
            fireCooldown -= Time.deltaTime;

            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            bool fireHeld = (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightTrigger.isPressed);
            bool rollHeld = (mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.leftTrigger.isPressed);

            // the click that captures the mouse shouldn't also start shooting
            if (!FirstPersonController.InputCaptured)
            {
                waitForRelease = true;
                rollerWasDown = false;
                return;
            }
            if (waitForRelease)
            {
                if (fireHeld || rollHeld) return;
                waitForRelease = false;
            }

            if (fireHeld && fireCooldown <= 0f)
            {
                FireBlob();
                fireCooldown = 1f / fireRate;
            }

            if (rollHeld) Roll();
            else rollerWasDown = false;
        }

        void FireBlob()
        {
            // aim from the muzzle at whatever is under the crosshair, so blobs land where you look
            Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 target = Physics.Raycast(aimRay, out RaycastHit hit, 200f, hitMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : aimRay.GetPoint(100f);

            Vector3 origin = muzzle != null ? muzzle.position : aimCamera.transform.position;
            Vector3 direction = (target - origin).normalized;
            Vector2 spread = Random.insideUnitCircle * spreadDegrees;
            direction = Quaternion.AngleAxis(spread.x, aimCamera.transform.up) *
                        Quaternion.AngleAxis(spread.y, aimCamera.transform.right) * direction;

            GameObject blob = new GameObject("InkBlob");
            blob.transform.SetPositionAndRotation(origin, Quaternion.identity);
            blob.transform.localScale = Vector3.one * projectileSize;

            if (projectileMesh != null)
            {
                blob.AddComponent<MeshFilter>().sharedMesh = projectileMesh;
                MeshRenderer meshRenderer = blob.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = projectileMaterial;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                SetRendererColor(meshRenderer, CurrentColor);
            }

            float radius = splatRadius * Random.Range(0.85f, 1.15f);
            blob.AddComponent<InkProjectile>().Launch(direction * projectileSpeed, CurrentColor, radius, projectileGravity, projectileLifetime, hitMask);
        }

        void Roll()
        {
            Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(aimRay, out RaycastHit hit, rollerReach, hitMask, QueryTriggerInteraction.Ignore))
            {
                rollerWasDown = false;
                return;
            }

            PaintManager manager = PaintManager.Instance;
            if (manager == null) return;

            float spacing = Mathf.Max(0.01f, rollerRadius * rollerSpacing);
            if (!rollerWasDown)
            {
                manager.PaintSphere(hit.point, hit.normal, rollerRadius, CurrentColor, 0.9f);
                lastRollerPoint = hit.point;
                rollerWasDown = true;
                return;
            }

            // fill the gap between this frame and last frame so quick sweeps stay continuous
            float travelled = Vector3.Distance(lastRollerPoint, hit.point);
            if (travelled < spacing) return;

            if (travelled > rollerReach)
            {
                // jumped to a different surface - don't smear ink through the air between them
                manager.PaintSphere(hit.point, hit.normal, rollerRadius, CurrentColor, 0.9f);
            }
            else
            {
                int steps = Mathf.CeilToInt(travelled / spacing);
                for (int i = 1; i <= steps; i++)
                {
                    Vector3 point = Vector3.Lerp(lastRollerPoint, hit.point, (float)i / steps);
                    manager.PaintSphere(point, hit.normal, rollerRadius, CurrentColor, 0.9f);
                }
            }
            lastRollerPoint = hit.point;
        }

        void OnColorChanged(int index)
        {
            ApplyTint(CurrentColor);
        }

        void ApplyTint(Color color)
        {
            if (tintRenderers == null) return;
            foreach (Renderer tintRenderer in tintRenderers)
            {
                if (tintRenderer != null) SetRendererColor(tintRenderer, color);
            }
        }

        void SetRendererColor(Renderer target, Color color)
        {
            tintBlock ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(tintBlock);
            tintBlock.SetColor(BaseColorId, color);
            target.SetPropertyBlock(tintBlock);
        }
    }
}
