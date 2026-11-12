using UnityEngine;
using UnityEngine.Rendering;

namespace InkFlip
{
    // Put this on any 3D object that should take ink. It needs:
    //  - a material using the "InkFlip/PaintableLit" shader
    //  - a collider (that's how splats find it)
    //  - non-overlapping UV2 / lightmap UVs on the mesh. For imported models tick
    //    "Generate Lightmap UVs" in the model's import settings.
    // Paint textures are only allocated the first time the object is actually hit,
    // so unpainted objects cost no texture memory.
    [RequireComponent(typeof(Renderer))]
    public class Paintable : MonoBehaviour
    {
        [Tooltip("Paint texture resolution. 0 = pick automatically from the object's surface area.")]
        public int textureSize = 0;
        [Tooltip("Used when Texture Size is 0: how many paint texels per metre of surface.")]
        public float texelsPerMeter = 160f;
        public int minTextureSize = 256;
        public int maxTextureSize = 2048;

        static readonly int PaintTexId = Shader.PropertyToID("_PaintTex");
        static readonly int PaintAmountId = Shader.PropertyToID("_PaintAmount");

        Renderer cachedRenderer;
        MaterialPropertyBlock propertyBlock;

        public Renderer Renderer => cachedRenderer != null ? cachedRenderer : (cachedRenderer = GetComponent<Renderer>());

        // accumulated paint (what splats draw into)
        public RenderTexture MaskTexture { get; private set; }
        // dilated copy of the mask with mipmaps - this is what the material samples
        public RenderTexture DisplayTexture { get; private set; }
        // 1 wherever the mesh has UVs, used to push paint past UV seams
        public RenderTexture IslandTexture { get; private set; }

        public bool IsAllocated => MaskTexture != null;

        public int SubMeshCount
        {
            get
            {
                Mesh mesh = GetMesh();
                return mesh != null ? mesh.subMeshCount : 1;
            }
        }

        void Start()
        {
            Mesh mesh = GetMesh();
            if (mesh != null && !mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
            {
                Debug.LogWarning($"Paintable '{name}': mesh '{mesh.name}' has no UV2/lightmap UVs, so paint will land in the wrong places. " +
                                 "Enable 'Generate Lightmap UVs' in the model import settings.", this);
            }
        }

        // called by PaintManager on the first hit; it queues the clears + island render into its command buffer
        internal void Allocate()
        {
            int size = ResolveTextureSize();

            MaskTexture = CreateTexture(size, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, false, FilterMode.Bilinear, "Mask");
            DisplayTexture = CreateTexture(size, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, true, FilterMode.Trilinear, "Display");
            IslandTexture = CreateTexture(size, RenderTextureFormat.R8, RenderTextureReadWrite.Linear, false, FilterMode.Point, "Islands");

            propertyBlock ??= new MaterialPropertyBlock();
            Renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetTexture(PaintTexId, DisplayTexture);
            propertyBlock.SetFloat(PaintAmountId, 1f);
            Renderer.SetPropertyBlock(propertyBlock);
        }

        public void ClearPaint()
        {
            if (!IsAllocated) return;
            ReleaseTextures();
            if (propertyBlock != null)
            {
                propertyBlock.SetFloat(PaintAmountId, 0f);
                Renderer.SetPropertyBlock(propertyBlock);
            }
        }

        int ResolveTextureSize()
        {
            if (textureSize > 0) return textureSize;

            float area = EstimateWorldSurfaceArea();
            int size = Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(area) * texelsPerMeter));
            return Mathf.Clamp(size, minTextureSize, maxTextureSize);
        }

        float EstimateWorldSurfaceArea()
        {
            Mesh mesh = GetMesh();
            if (mesh != null && mesh.isReadable)
            {
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                Matrix4x4 toWorld = transform.localToWorldMatrix;
                float area = 0f;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = toWorld.MultiplyPoint3x4(vertices[triangles[i]]);
                    Vector3 b = toWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                    Vector3 c = toWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                    area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                }
                return area;
            }

            // mesh isn't CPU-readable (default for imported models) - approximate with the bounding box
            Vector3 s = Renderer.bounds.size;
            return 2f * (s.x * s.y + s.y * s.z + s.x * s.z);
        }

        Mesh GetMesh()
        {
            if (TryGetComponent(out MeshFilter filter)) return filter.sharedMesh;
            if (TryGetComponent(out SkinnedMeshRenderer skinned)) return skinned.sharedMesh;
            return null;
        }

        RenderTexture CreateTexture(int size, RenderTextureFormat format, RenderTextureReadWrite readWrite, bool mips, FilterMode filter, string label)
        {
            var rt = new RenderTexture(size, size, 0, format, readWrite)
            {
                name = $"{name}_Paint{label}",
                useMipMap = mips,
                autoGenerateMips = false,
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = mips ? 4 : 0,
            };
            rt.Create();
            return rt;
        }

        void ReleaseTextures()
        {
            if (MaskTexture != null) { MaskTexture.Release(); DestroyImmediateSafe(MaskTexture); }
            if (DisplayTexture != null) { DisplayTexture.Release(); DestroyImmediateSafe(DisplayTexture); }
            if (IslandTexture != null) { IslandTexture.Release(); DestroyImmediateSafe(IslandTexture); }
            MaskTexture = DisplayTexture = IslandTexture = null;
        }

        static void DestroyImmediateSafe(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        void OnDestroy()
        {
            ReleaseTextures();
        }
    }
}
