using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InkFlip
{
    // How a single round dab looks. See SelectionDrawMode for the draw-board presets.
    public struct DabStyle
    {
        public float hardness;   // 0 = very soft falloff, 1 = hard edge
        public float strength;   // opacity of one dab (dabs build up)
        public float edgeNoise;  // 0 = clean circle, higher = blobby ink splat
        public float grain;      // watercolour paper grain (0-1)
        public float glitter;    // fraction of texels that become bright glitter flakes
    }

    // Single entry point for putting ink on the world:
    //   PaintManager.Instance.PaintSphere(point, normal, radius, color);
    // Every Paintable whose collider touches the sphere gets painted, so one splat
    // can cover a wall, the floor and a chair leg at the same time.
    [DefaultExecutionOrder(-100)]
    public class PaintManager : MonoBehaviour
    {
        static PaintManager instance;
        public static PaintManager Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<PaintManager>();
                return instance;
            }
        }

        [Tooltip("Hidden/InkFlip/PaintSplat - assigned so the shader is included in builds.")]
        public Shader splatShader;
        [Tooltip("Hidden/InkFlip/PaintDilate - assigned so the shader is included in builds.")]
        public Shader dilateShader;

        public LayerMask paintableLayers = Physics.DefaultRaycastLayers;

        [Header("Splat shape")]
        [Range(0f, 0.9f)] public float edgeNoise = 0.45f;
        [Tooltip("Frequency of the noise that makes splats blobby instead of perfect circles.")]
        public float noiseScale = 4f;

        static readonly int SplatPositionId = Shader.PropertyToID("_SplatPosition");
        static readonly int SplatNormalId = Shader.PropertyToID("_SplatNormal");
        static readonly int SplatColorId = Shader.PropertyToID("_SplatColor");
        static readonly int SplatParamsId = Shader.PropertyToID("_SplatParams");
        static readonly int SplatSeedId = Shader.PropertyToID("_SplatSeed");
        static readonly int PaintMaskTexId = Shader.PropertyToID("_PaintMaskTex");
        static readonly int PaintIslandTexId = Shader.PropertyToID("_PaintIslandTex");
        static readonly int PaintTexSizeId = Shader.PropertyToID("_PaintTexSize");

        static readonly int StrokeEndId = Shader.PropertyToID("_StrokeEnd");
        static readonly int StrokeAlongId = Shader.PropertyToID("_StrokeAlong");
        static readonly int StrokeAcrossId = Shader.PropertyToID("_StrokeAcross");
        static readonly int StrokeParamsId = Shader.PropertyToID("_StrokeParams");

        static readonly int SplatStyleId = Shader.PropertyToID("_SplatStyle");

        // pass indices in Hidden/InkFlip/PaintSplat
        const int SplatPass = 0;
        const int IslandPass = 1;
        const int StrokePass = 2;
        const int ErasePass = 3;

        Material splatMaterial;
        Material dilateMaterial;
        CommandBuffer commandBuffer;
        readonly Collider[] overlapResults = new Collider[64];
        readonly HashSet<Paintable> paintedThisCall = new HashSet<Paintable>();

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
        }

        bool EnsureResources()
        {
            if (commandBuffer != null) return true;

            if (splatShader == null) splatShader = Shader.Find("Hidden/InkFlip/PaintSplat");
            if (dilateShader == null) dilateShader = Shader.Find("Hidden/InkFlip/PaintDilate");
            if (splatShader == null || dilateShader == null)
            {
                Debug.LogError("PaintManager: paint shaders are missing.", this);
                return false;
            }

            splatMaterial = new Material(splatShader) { hideFlags = HideFlags.HideAndDontSave };
            dilateMaterial = new Material(dilateShader) { hideFlags = HideFlags.HideAndDontSave };
            commandBuffer = new CommandBuffer { name = "InkFlip Paint" };
            return true;
        }

        // A blobby splat (blaster impacts, roller). Returns how many objects were painted.
        public int PaintSphere(Vector3 center, Vector3 normal, float radius, Color color, float hardness = 0.85f, float strength = 1f)
        {
            var style = new DabStyle { hardness = hardness, strength = strength, edgeNoise = edgeNoise };
            return PaintDab(center, normal, radius, color, style);
        }

        // A single round dab with full control over its look (draw-mode brushes).
        // If 'onlyTarget' is set, nothing else is painted even if it's inside the radius.
        public int PaintDab(Vector3 center, Vector3 normal, float radius, Color color, DabStyle style, Paintable onlyTarget = null)
        {
            if (!EnsureResources()) return 0;

            int hitCount = Physics.OverlapSphereNonAlloc(center, radius, overlapResults, paintableLayers, QueryTriggerInteraction.Ignore);

            SetCommonGlobals(center, radius, normal, color, style.hardness, style.strength, style.edgeNoise);
            commandBuffer.SetGlobalVector(SplatStyleId, new Vector4(style.grain, style.glitter, 0f, 0f));
            commandBuffer.SetGlobalVector(SplatSeedId, Random.insideUnitSphere * 100f);
            return PaintOverlaps(hitCount, SplatPass, onlyTarget);
        }

        // Removes paint in a round area. 'strength' below 1 erases gradually.
        public int EraseDab(Vector3 center, Vector3 normal, float radius, float hardness = 0.8f, float strength = 1f, Paintable onlyTarget = null)
        {
            if (!EnsureResources()) return 0;

            int hitCount = Physics.OverlapSphereNonAlloc(center, radius, overlapResults, paintableLayers, QueryTriggerInteraction.Ignore);

            SetCommonGlobals(center, radius, normal, Color.clear, hardness, strength, 0f);
            return PaintOverlaps(hitCount, ErasePass, onlyTarget);
        }

        // A brush stroke segment from 'from' to 'to'. 'load' (0-1) is how much paint is left on
        // the brush: lower load lets fewer bristles deposit, giving a dry, streaky stroke.
        // Keep 'strokeSeed' the same for every segment of one stroke so the bristle streaks
        // line up into continuous lines. Returns how many objects were painted.
        public int PaintStroke(Vector3 from, Vector3 to, Vector3 normal, Vector3 strokeDirection, float radius, Color color,
                               float load, float bristlesPerMeter, float strokeSeed, float hardness = 0.7f)
        {
            if (!EnsureResources()) return 0;

            int hitCount = Physics.OverlapCapsuleNonAlloc(from, to, radius, overlapResults, paintableLayers, QueryTriggerInteraction.Ignore);

            // bristle streaks run along the stroke, so the "across" axis lies in the surface, perpendicular to travel
            Vector3 n = normal.normalized;
            Vector3 along = Vector3.ProjectOnPlane(strokeDirection, n);
            if (along.sqrMagnitude < 1e-6f) along = Vector3.ProjectOnPlane(Vector3.up, n);
            if (along.sqrMagnitude < 1e-6f) along = Vector3.ProjectOnPlane(Vector3.right, n);
            along.Normalize();
            Vector3 across = Vector3.Cross(along, n).normalized;

            SetCommonGlobals(from, radius, normal, color, hardness, 1f, 0f);
            commandBuffer.SetGlobalVector(StrokeEndId, new Vector4(to.x, to.y, to.z, Mathf.Clamp01(load)));
            commandBuffer.SetGlobalVector(StrokeAlongId, along);
            commandBuffer.SetGlobalVector(StrokeAcrossId, across);
            commandBuffer.SetGlobalVector(StrokeParamsId, new Vector4(bristlesPerMeter, strokeSeed, 0f, 0f));
            return PaintOverlaps(hitCount, StrokePass);
        }

        void SetCommonGlobals(Vector3 position, float radius, Vector3 normal, Color color, float hardness, float strength, float shapeNoise)
        {
            commandBuffer.SetGlobalVector(SplatPositionId, new Vector4(position.x, position.y, position.z, radius));
            commandBuffer.SetGlobalVector(SplatNormalId, normal.normalized);
            // paint textures are sRGB, so the shader must be handed linear values or ink comes out washed-out
            Color shaderColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            commandBuffer.SetGlobalVector(SplatColorId, shaderColor);
            commandBuffer.SetGlobalVector(SplatParamsId, new Vector4(hardness, strength, shapeNoise, noiseScale));
            commandBuffer.SetGlobalVector(SplatStyleId, Vector4.zero);
        }

        int PaintOverlaps(int hitCount, int pass, Paintable onlyTarget = null)
        {
            paintedThisCall.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                Paintable paintable = overlapResults[i].GetComponentInParent<Paintable>();
                if (onlyTarget != null && paintable != onlyTarget) continue;
                if (paintable != null && paintable.isActiveAndEnabled && paintedThisCall.Add(paintable))
                {
                    QueueDraw(paintable, pass);
                }
            }

            // globals were queued even if nothing was hit, so always flush
            Graphics.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
            return paintedThisCall.Count;
        }

        void QueueDraw(Paintable paintable, int pass)
        {
            if (!paintable.IsAllocated)
            {
                paintable.Allocate();
                QueueInitialise(paintable);
            }

            commandBuffer.SetRenderTarget(paintable.MaskTexture);
            int subMeshes = paintable.SubMeshCount;
            for (int sub = 0; sub < subMeshes; sub++)
            {
                commandBuffer.DrawRenderer(paintable.Renderer, splatMaterial, sub, pass);
            }

            // copy mask -> display with seam dilation (a fullscreen triangle, not Blit:
            // CommandBuffer.Blit doesn't reliably bind its source outside a URP render pass)
            commandBuffer.SetGlobalTexture(PaintMaskTexId, paintable.MaskTexture);
            commandBuffer.SetGlobalTexture(PaintIslandTexId, paintable.IslandTexture);
            commandBuffer.SetGlobalInt(PaintTexSizeId, paintable.MaskTexture.width);
            commandBuffer.SetRenderTarget(paintable.DisplayTexture);
            commandBuffer.DrawProcedural(Matrix4x4.identity, dilateMaterial, 0, MeshTopology.Triangles, 3);
            commandBuffer.GenerateMips(paintable.DisplayTexture);
        }

        void QueueInitialise(Paintable paintable)
        {
            commandBuffer.SetRenderTarget(paintable.MaskTexture);
            commandBuffer.ClearRenderTarget(false, true, Color.clear);
            commandBuffer.SetRenderTarget(paintable.DisplayTexture);
            commandBuffer.ClearRenderTarget(false, true, Color.clear);

            commandBuffer.SetRenderTarget(paintable.IslandTexture);
            commandBuffer.ClearRenderTarget(false, true, Color.clear);
            int subMeshes = paintable.SubMeshCount;
            for (int sub = 0; sub < subMeshes; sub++)
            {
                commandBuffer.DrawRenderer(paintable.Renderer, splatMaterial, sub, IslandPass);
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            commandBuffer?.Release();
            if (splatMaterial != null) DestroyImmediate(splatMaterial);
            if (dilateMaterial != null) DestroyImmediate(dilateMaterial);
        }
    }
}
