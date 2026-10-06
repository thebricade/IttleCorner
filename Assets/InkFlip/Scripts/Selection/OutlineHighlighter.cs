using UnityEngine;

namespace InkFlip
{
    // Outlines one renderer at a time by temporarily appending an outline material
    // (InkFlip/Outline) to its material list, and restores the original list after.
    public class OutlineHighlighter : MonoBehaviour
    {
        [Tooltip("InkFlip/Outline - assigned so the shader is included in builds.")]
        public Shader outlineShader;
        public Color color = Color.white;
        public float width = 0.025f;
        [Tooltip("Pulses the outline brightness so it reads as 'you can click this'.")]
        public float pulseSpeed = 4f;

        static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
        static readonly int CenterId = Shader.PropertyToID("_OutlineCenter");

        Material outlineMaterial;
        Renderer current;
        Material[] originalMaterials;
        bool pulse;

        public Renderer Current => current;

        void Awake()
        {
            if (outlineShader == null) outlineShader = Shader.Find("InkFlip/Outline");
            if (outlineShader != null) outlineMaterial = new Material(outlineShader) { name = "Outline (runtime)" };
        }

        // pulse = true for "hovering, click to select"; false for a steady "this is selected" outline
        public void Highlight(Renderer target, bool pulsing = true)
        {
            pulse = pulsing;
            if (target == current) return;
            Clear();
            if (target == null || outlineMaterial == null) return;

            current = target;
            originalMaterials = target.sharedMaterials;

            var withOutline = new Material[originalMaterials.Length + 1];
            originalMaterials.CopyTo(withOutline, 0);
            withOutline[withOutline.Length - 1] = outlineMaterial;
            target.sharedMaterials = withOutline;

            // push the rim out from the middle of the mesh (pivots aren't always centred)
            Vector3 center = Vector3.zero;
            if (target.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null) center = filter.sharedMesh.bounds.center;
            outlineMaterial.SetVector(CenterId, center);
            outlineMaterial.SetFloat(WidthId, width);
            outlineMaterial.SetColor(ColorId, color);
        }

        public void Clear()
        {
            if (current != null && originalMaterials != null) current.sharedMaterials = originalMaterials;
            current = null;
            originalMaterials = null;
        }

        void Update()
        {
            if (current == null || outlineMaterial == null) return;
            float brightness = pulse ? Mathf.Lerp(0.55f, 1f, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f) : 1f;
            outlineMaterial.SetColor(ColorId, color * brightness);
        }

        void OnDisable()
        {
            Clear();
        }

        void OnDestroy()
        {
            if (outlineMaterial != null) Destroy(outlineMaterial);
        }
    }
}
