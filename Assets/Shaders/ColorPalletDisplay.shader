Shader "Custom/ColorPalletDisplay"
{
    Properties
    {
        z_slice("B/Z Slice", Range(0, 255)) = 128
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture3D screen_color_pallet_texture;
            SAMPLER(sampler_screen_color_pallet_texture); // Required for bilinear filtering

            float z_slice;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 uvw = float3(input.uv, z_slice / 255.0);
                float4 color = screen_color_pallet_texture.Sample(sampler_screen_color_pallet_texture, uvw);

                return half4(color.rgb / 255.0f, 1.0);
            }

            ENDHLSL
        }
    }
}