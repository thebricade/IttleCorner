// Copies the paint mask into the texture that's actually displayed, pushing paint
// a few texels OUTWARD past the edge of each UV island. Without this, bilinear
// filtering and mipmaps sample the empty space between islands and you get thin
// unpainted lines along every UV seam (cube edges, etc).
//
// Drawn as a single fullscreen triangle (no vertex buffer) and reads texels by
// integer index, so source and destination line up exactly on every graphics API.
Shader "Hidden/InkFlip/PaintDilate"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            Texture2D _PaintMaskTex;
            Texture2D _PaintIslandTex;
            int _PaintTexSize;

            float4 vert (uint id : SV_VertexID) : SV_POSITION
            {
                float2 corner = float2((id << 1) & 2, id & 2);
                return float4(corner * 2.0 - 1.0, 0.5, 1.0);
            }

            float4 frag (float4 position : SV_POSITION) : SV_Target
            {
                int2 texel = int2(position.xy);
                float4 c = _PaintMaskTex.Load(int3(texel, 0));
                if (_PaintIslandTex.Load(int3(texel, 0)).r > 0.5)
                    return c;

                // outside every island: borrow the colour of the nearest texel that is inside one
                const int2 dirs[8] =
                {
                    int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1),
                    int2(1, 1), int2(-1, 1), int2(1, -1), int2(-1, -1)
                };

                [unroll]
                for (int r = 1; r <= 4; r++)
                {
                    [unroll]
                    for (int d = 0; d < 8; d++)
                    {
                        int2 neighbour = clamp(texel + dirs[d] * r, 0, _PaintTexSize - 1);
                        if (_PaintIslandTex.Load(int3(neighbour, 0)).r > 0.5)
                            return _PaintMaskTex.Load(int3(neighbour, 0));
                    }
                }
                return c;
            }
            ENDCG
        }
    }
}
