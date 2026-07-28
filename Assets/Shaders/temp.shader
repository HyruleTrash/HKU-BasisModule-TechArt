Shader "Custom/ColorCountDisplay"
{
    Properties
    {
        _DepthSlice("Depth Slice", Range(0, 255)) = 128
        _AlphaMultiplier("Alpha Multiplier", Float) = 1000
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture3D<uint> _ColorCountTexture;

            float _DepthSlice;
            float _AlphaMultiplier;

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

                output.positionCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz
                    );

                output.uv = input.uv;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                uint x = min((uint)(input.uv.x * 255.0 + 0.5), 255u);
                uint y = min((uint)(input.uv.y * 255.0 + 0.5), 255u);
                uint z = (uint)clamp(round(_DepthSlice), 0.0, 255.0);
                
                // uint x = 255;
                // uint y = 0;
                // uint z = 0;

                uint count = _ColorCountTexture.Load(int4(x, y, z, 0));

                float3 found_color = float3(x, y, z) / 255.0;

                float total_pixels = _ScreenParams.x * _ScreenParams.y;
                float alpha = saturate((float)count / total_pixels * _AlphaMultiplier);

                return half4(found_color, alpha);
            }

            ENDHLSL
        }
    }
}