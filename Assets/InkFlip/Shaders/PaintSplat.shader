// Draws a mesh "unwrapped" into its paint texture: every vertex is placed at its
// UV2 (lightmap UV) position instead of its screen position, while the fragment
// still knows the real world position. That lets a splat be a shape in WORLD
// space - it wraps across corners, edges and neighbouring objects naturally.
//
// Pass 0: blobby splat (sphere)            - blaster impacts, roller
// Pass 1: write 1 everywhere the mesh has UVs (the "island" mask used for seam fixing)
// Pass 2: brush stroke segment (capsule)   - streaky bristle marks along the stroke
// Pass 3: erase (sphere)                   - removes paint
// Passes 0 and 2 use a premultiplied-alpha "over" blend into the mask texture.
Shader "Hidden/InkFlip/PaintSplat"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 uv1 : TEXCOORD1;
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float3 worldPos : TEXCOORD0;
            float3 worldNormal : TEXCOORD1;
        };

        float4 _SplatPosition;   // xyz = centre (stroke: start), w = radius
        float4 _SplatNormal;     // xyz = surface normal at the hit
        float4 _SplatColor;
        float4 _SplatParams;     // x = hardness, y = strength, z = edge noise, w = noise scale
        float4 _SplatSeed;
        float4 _SplatStyle;      // x = paper grain, y = glitter flake density

        v2f vertUnwrap (appdata v)
        {
            v2f o;
            float2 clip = v.uv1 * 2.0 - 1.0;
            #if UNITY_UV_STARTS_AT_TOP
            clip.y = -clip.y;
            #endif
            o.pos = float4(clip, 0.5, 1.0);
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.worldNormal = UnityObjectToWorldNormal(v.normal);
            return o;
        }

        // don't bleed through to the far side of thin walls - surfaces facing
        // away from the hit surface are skipped, but corners (90 degrees) still paint
        bool FacesAway (v2f i)
        {
            return dot(normalize(i.worldNormal), _SplatNormal.xyz) < -0.25;
        }

        float hash13(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float valueNoise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(
                lerp(lerp(hash13(i + float3(0,0,0)), hash13(i + float3(1,0,0)), f.x),
                     lerp(hash13(i + float3(0,1,0)), hash13(i + float3(1,1,0)), f.x), f.y),
                lerp(lerp(hash13(i + float3(0,0,1)), hash13(i + float3(1,0,1)), f.x),
                     lerp(hash13(i + float3(0,1,1)), hash13(i + float3(1,1,1)), f.x), f.y), f.z);
        }

        // round dab with a noisy edge; 'd' returns the distance from the centre
        float SphereMask (v2f i, out float d)
        {
            float radius = _SplatPosition.w;
            float3 p = i.worldPos * _SplatParams.w + _SplatSeed.xyz;
            float n = valueNoise(p) * 0.65 + valueNoise(p * 2.7) * 0.35;

            // noise only ever shrinks the blob, so the CPU-side overlap test can use the plain radius
            float r = radius * (1.0 - _SplatParams.z * n);
            d = distance(i.worldPos, _SplatPosition.xyz);
            float m = 1.0 - smoothstep(r * _SplatParams.x, r, d);
            return saturate(m * _SplatParams.y);
        }
        ENDCG

        Pass
        {
            Name "Splat"
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vertUnwrap
            #pragma fragment frag

            float4 frag (v2f i) : SV_Target
            {
                if (FacesAway(i)) return 0;

                float d;
                float m = SphereMask(i, d);
                float3 color = _SplatColor.rgb;

                // watercolour paper grain - fixed in world space so layered washes line up
                if (_SplatStyle.x > 0.0)
                {
                    float grain = valueNoise(i.worldPos * 28.0) * 0.6 + valueNoise(i.worldPos * 71.0) * 0.4;
                    m *= lerp(1.0 - _SplatStyle.x, 1.0, grain);
                }

                // gel glitter: a fixed world-space scatter of bright flakes inside a glossy gel colour
                if (_SplatStyle.y > 0.0)
                {
                    float flake = hash13(floor(i.worldPos * 150.0));
                    color = flake < _SplatStyle.y ? lerp(color, 1.0, 0.6) : color * 0.9;
                }

                m = saturate(m);
                return float4(color * m, m);
            }
            ENDCG
        }

        Pass
        {
            Name "Islands"
            Blend Off

            CGPROGRAM
            #pragma vertex vertUnwrap
            #pragma fragment frag

            float4 frag (v2f i) : SV_Target
            {
                return 1;
            }
            ENDCG
        }

        Pass
        {
            Name "Stroke"
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vertUnwrap
            #pragma fragment frag

            float4 _StrokeEnd;     // xyz = segment end, w = paint load (0-1)
            float4 _StrokeAlong;   // unit vector along the stroke, in the surface
            float4 _StrokeAcross;  // unit vector across the stroke, in the surface
            float4 _StrokeParams;  // x = bristles per metre, y = stroke seed

            float4 frag (v2f i) : SV_Target
            {
                if (FacesAway(i)) return 0;

                // distance to the segment -> a capsule-shaped band as wide as the brush
                float3 a = _SplatPosition.xyz;
                float3 ab = _StrokeEnd.xyz - a;
                float lengthSq = dot(ab, ab);
                float t = lengthSq > 1e-8 ? saturate(dot(i.worldPos - a, ab) / lengthSq) : 0.0;
                float d = distance(i.worldPos, a + ab * t);

                float radius = _SplatPosition.w;
                float m = 1.0 - smoothstep(radius * _SplatParams.x, radius, d);
                if (m <= 0.0) return 0;

                // bristle streaks: noise that changes fast ACROSS the stroke and slowly ALONG it.
                // Measured in world space so consecutive segments of one stroke line up.
                float across = dot(i.worldPos, _StrokeAcross.xyz) * _StrokeParams.x;
                float along = dot(i.worldPos, _StrokeAlong.xyz) * 1.2;
                float seed = _StrokeParams.y;
                float streak = valueNoise(float3(across, along, seed)) * 0.7
                             + valueNoise(float3(across * 2.3, along * 0.4, seed + 17.0)) * 0.3;

                // a loaded brush still leaves thin gaps between bristle clumps; a dry one only the heaviest bristles
                float load = _StrokeEnd.w;
                float threshold = lerp(0.82, 0.24, load);
                float bristles = smoothstep(threshold, threshold + 0.08, streak);

                // frayed edges: outer bristles drop out first
                float edge = saturate(d / max(radius, 1e-4));
                bristles *= 1.0 - smoothstep(0.55, 1.0, edge) * (1.0 - streak);

                // heavier bristles lay down slightly darker paint -> visible brush texture inside the stroke
                float3 color = _SplatColor.rgb * lerp(1.08, 0.82, streak);

                m = saturate(m * bristles);
                return float4(color * m, m);
            }
            ENDCG
        }

        Pass
        {
            Name "Erase"
            // dst * (1 - m): fades out both the premultiplied colour and the coverage
            Blend Zero OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vertUnwrap
            #pragma fragment frag

            float4 frag (v2f i) : SV_Target
            {
                if (FacesAway(i)) return 0;
                float d;
                return float4(0, 0, 0, SphereMask(i, d));
            }
            ENDCG
        }
    }
}
