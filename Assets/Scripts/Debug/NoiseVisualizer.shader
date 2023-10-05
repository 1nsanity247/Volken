Shader "Hidden/NoiseVisualizer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            Texture2D<float> Tex;
            SamplerState samplerTex;

            float scale;
            float2 offset;

            fixed4 frag(v2f i) : SV_Target
            {
                float3 col = 0.5f * (Tex.Sample(samplerTex, scale * float2(i.uv * float2(16.0/9.0,1.0)) + offset) + 1.0f);

                return float4(col, 0.0);
            }
            ENDCG
        }
    }
}
