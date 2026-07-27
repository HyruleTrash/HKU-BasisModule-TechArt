Shader "Hidden/ColorCountWheelPreview"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            Texture3D<uint> _VolumeTex;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                uint maxCount = 0;
                float3 displayColor = float3(0, 0, 0);

                // Vertical divider line separating the two panels
                if (abs(uv.x - 0.5f) < 0.005f)
                {
                    return float4(0.3, 0.3, 0.3, 1.0);
                }

                if (uv.x < 0.5f)
                {
                    // Left Panel: Red (X) vs Green (Y), projected across Blue
                    uint r = (uint)(saturate(uv.x * 2.0f) * 255.0f);
                    uint g = (uint)(saturate(uv.y) * 255.0f);

                    uint bestB = 0;
                    [unroll]
                    for (int b = 0; b < 256; b += 4)
                    {
                        uint count = _VolumeTex.Load(int4((int)r, (int)g, b, 0));
                        if (count > maxCount)
                        {
                            maxCount = count;
                            bestB = (uint)b;
                        }
                    }
                    displayColor = float3((float)r / 255.0f, (float)g / 255.0f, (float)bestB / 255.0f);
                }
                else
                {
                    // Right Panel: Red (X) vs Blue (Y), projected across Green
                    uint r = (uint)(saturate((uv.x - 0.5f) * 2.0f) * 255.0f);
                    uint bVal = (uint)(saturate(uv.y) * 255.0f);

                    uint bestG = 0;
                    [unroll]
                    for (int g = 0; g < 256; g += 4)
                    {
                        uint count = _VolumeTex.Load(int4((int)r, g, (int)bVal, 0));
                        if (count > maxCount)
                        {
                            maxCount = count;
                            bestG = (uint)g;
                        }
                    }
                    displayColor = float3((float)r / 255.0f, (float)bestG / 255.0f, (float)bVal / 255.0f);
                }

                // If no pixels exist at this projection coordinate, render pure background (no beige/fallback colors)
                if (maxCount == 0)
                {
                    return float4(0.04f, 0.04f, 0.04f, 1.0f);
                }

                // Logarithmic density scaling
                float density = saturate(log2(1.0 + (float)maxCount) * 0.15f);

                float3 backgroundColor = float3(0.04, 0.04, 0.04);
                float3 finalColor = lerp(backgroundColor, displayColor, density);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}