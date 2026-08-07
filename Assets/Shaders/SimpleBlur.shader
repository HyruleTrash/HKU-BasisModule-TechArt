Shader "Hidden/SimpleBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Offset", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct attributes
            {
                float4 position_os : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct varyings
            {
                float4 position_cs : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            float4 _MainTex_TexelSize;
            float2 _Offset;

            varyings vert(attributes input)
            {
                varyings output;
                output.position_cs = TransformObjectToHClip(input.position_os.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(varyings input) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy * _Offset;
                float2 uv = input.uv;

                // Sample neighbors
                half4 c1 = _MainTex.Sample(sampler_MainTex, uv + float2(-texel.x, -texel.y));
                half4 c2 = _MainTex.Sample(sampler_MainTex, uv + float2( texel.x, -texel.y));
                half4 c3 = _MainTex.Sample(sampler_MainTex, uv + float2(-texel.x,  texel.y));
                half4 c4 = _MainTex.Sample(sampler_MainTex, uv + float2( texel.x,  texel.y));

                half3 color_sum = (c1.rgb * c1.a) + (c2.rgb * c2.a) + (c3.rgb * c3.a) + (c4.rgb * c4.a);
                half alpha_sum = c1.a + c2.a + c3.a + c4.a;

                half3 final_color = color_sum / max(alpha_sum, 0.0001);
                half final_alpha = alpha_sum * 0.25;

                return half4(final_color, final_alpha);
            }
            ENDHLSL
        }
    }
}