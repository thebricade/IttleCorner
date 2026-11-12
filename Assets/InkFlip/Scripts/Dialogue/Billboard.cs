using UnityEngine;

namespace InkFlip
{
    // Keeps a flat sprite turned toward the player's camera, so 2D character art works in the 3D room.
    public class Billboard : MonoBehaviour
    {
        [Tooltip("Only turn around the vertical axis, so the character stays upright.")]
        public bool yAxisOnly = true;

        void LateUpdate()
        {
            Camera view = Camera.main;
            if (view == null) return;

            Vector3 away = transform.position - view.transform.position;
            if (yAxisOnly) away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) return;

            transform.rotation = Quaternion.LookRotation(away);
        }
    }
}
