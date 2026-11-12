using UnityEngine;

namespace InkFlip
{
    // One shape in the sculpture. A sculpture is a handful of these, overlapping like pieces of clay
    // pressed together; each keeps its own mesh, colour and paint.
    public class ClayPart
    {
        public readonly ClayShape Shape;
        public readonly ClayMesh Clay;
        public readonly GameObject Object;
        public readonly Material Material;

        public Transform Transform => Object.transform;
        public bool Alive { get; private set; } = true;
        public Color Color { get; private set; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public ClayPart(ClayShape shape, int resolution, float clayRadius, Transform parent, Material template, Color color)
        {
            Shape = shape;
            Clay = new ClayMesh(shape, resolution, clayRadius);

            Object = new GameObject("Part_" + shape);
            Object.transform.SetParent(parent, false);
            Object.AddComponent<MeshFilter>().sharedMesh = Clay.Mesh;
            Material = new Material(template) { name = "Clay_" + shape };
            Object.AddComponent<MeshRenderer>().sharedMaterial = Material;
            SetColor(color);
        }

        public void SetColor(Color color)
        {
            Color = color;
            Material.SetColor(BaseColorId, color);
        }

        // hidden but kept, so Undo can bring it back
        public void SetAlive(bool alive)
        {
            Alive = alive;
            Object.SetActive(alive);
        }

        public void Destroy()
        {
            if (Clay.Mesh != null) UnityEngine.Object.Destroy(Clay.Mesh);
            if (Material != null) UnityEngine.Object.Destroy(Material);
            if (Object != null) UnityEngine.Object.Destroy(Object);
        }
    }
}
