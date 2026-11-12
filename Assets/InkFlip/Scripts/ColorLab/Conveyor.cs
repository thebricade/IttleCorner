using UnityEngine;

namespace InkFlip
{
    // Carries its child objects along a belt and loops them round, for the candy factory. Pure decoration.
    public class Conveyor : MonoBehaviour
    {
        [Tooltip("Metres per second along world X.")]
        public float speed = 0.35f;
        public float minX = 7.2f;
        public float maxX = 11.6f;

        void Update()
        {
            float step = speed * Time.deltaTime;
            foreach (Transform child in transform)
            {
                Vector3 p = child.position;
                p.x += step;
                if (p.x > maxX) p.x -= (maxX - minX);
                child.position = p;
            }
        }
    }
}
