// Selection outline, added to a renderer as an extra material. Draws an enlarged
// copy of the mesh with front faces culled, so only a rim sticks out around the
// object's silhouette.
//
// Vertices are pushed out along the object's local axes (away from _OutlineCenter)
// rather than along their normals. Every vertex at the same position moves the same
// way, so hard-edged meshes like boxes don't crack open at the corners.
Shader "InkFlip/Outline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Width (metres)", Float) = 0.025
        _OutlineCenter ("Center (object space, set at runtime)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float4 _OutlineCenter;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            float4 vert (Attributes input) : SV_POSITION
            {
                float3 fromCenter = input.positionOS.xyz - _OutlineCenter.xyz;
                float3 direction = sign(fromCenter) * step(1e-4, abs(fromCenter));

                // convert the world-space width into object space so scaled objects get the same rim
                float4x4 toWorld = GetObjectToWorldMatrix();
                float3 scale = float3(length(toWorld._m00_m10_m20), length(toWorld._m01_m11_m21), length(toWorld._m02_m12_m22));
                float3 offset = direction * _OutlineWidth / max(scale, 1e-4);

                return TransformObjectToHClip(input.positionOS.xyz + offset);
            }

            half4 frag () : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
