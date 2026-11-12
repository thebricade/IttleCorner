using UnityEngine;

namespace InkFlip
{
    // A blob of ink in flight. Moves itself (no Rigidbody) and raycasts along each
    // frame's movement, so fast blobs can't tunnel through thin walls.
    public class InkProjectile : MonoBehaviour
    {
        Vector3 velocity;
        Color color;
        float splatRadius;
        float gravity;
        float lifeRemaining;
        LayerMask hitMask;

        public void Launch(Vector3 startVelocity, Color inkColor, float radius, float projectileGravity, float lifetime, LayerMask mask)
        {
            velocity = startVelocity;
            color = inkColor;
            splatRadius = radius;
            gravity = projectileGravity;
            lifeRemaining = lifetime;
            hitMask = mask;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            lifeRemaining -= dt;
            if (lifeRemaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            velocity += Vector3.down * gravity * dt;
            Vector3 step = velocity * dt;
            float distance = step.magnitude;

            if (distance > 0f && Physics.Raycast(transform.position, step / distance, out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
            {
                PaintManager manager = PaintManager.Instance;
                if (manager != null) manager.PaintSphere(hit.point, hit.normal, splatRadius, color);
                Destroy(gameObject);
                return;
            }

            transform.position += step;
        }
    }
}
