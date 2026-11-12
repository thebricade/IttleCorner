using UnityEngine;

namespace InkFlip
{
    // Put this on anything the glove is allowed to pick up and resize: a crate, a sofa, a sculpture.
    // On a group (a sofa made of several boxes) the whole group moves and scales together.
    // Nothing needs to be set up beyond the component - sizes and colliders are found from the children.
    public class Grabbable : MonoBehaviour
    {
        [Tooltip("Smallest size, as a multiple of the size it started at.")]
        public float minScale = 0.25f;
        [Tooltip("Largest size, as a multiple of the size it started at.")]
        public float maxScale = 4f;

        Renderer[] renderers;
        Collider[] colliders;
        Vector3 baseScale;
        float factor = 1f;

        public float ScaleFactor => factor;

        // the part that gets the selection outline (one renderer is enough to show what's selected)
        public Renderer Primary => renderers != null && renderers.Length > 0 ? renderers[0] : null;

        void Awake()
        {
            baseScale = transform.localScale;
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
        }

        // world-space box around everything visible in this object
        public Bounds GetBounds()
        {
            if (renderers == null || renderers.Length == 0) return new Bounds(transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        // colliders are switched off while carried, so the object can't shove the player or block its own placement
        public void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null) return;
            foreach (Collider c in colliders)
            {
                if (c != null) c.enabled = enabled;
            }
        }

        // Grows or shrinks about the middle of the object's base, so it stays standing where it was
        // instead of swelling out from some distant pivot (a group's pivot is often the room's origin).
        public void ScaleBy(float multiplier)
        {
            float target = Mathf.Clamp(factor * multiplier, minScale, maxScale);
            if (Mathf.Approximately(target, factor)) return;

            Bounds before = GetBounds();
            Vector3 anchor = new Vector3(before.center.x, before.min.y, before.center.z);

            factor = target;
            transform.localScale = baseScale * factor;

            Bounds after = GetBounds();
            Vector3 newAnchor = new Vector3(after.center.x, after.min.y, after.center.z);
            transform.position += anchor - newAnchor;
        }
    }
}
