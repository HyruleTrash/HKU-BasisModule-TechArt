Shader "Custom/MeltMatFX" // A shader for triggering a DOOM melt effect
{
    Properties
    {
        [MainColor] base_color("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] [HideInInspector] object_snapshot("Object Snapshot", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct attributes
            {
                float4 position_os : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct varyings
            {
                float4 position_hcs : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(object_snapshot); // snapshot of entire screen, where only the target object is visible
            SAMPLER(sampler_object_snapshot);

            CBUFFER_START(UnityPerMaterial)
                half4 base_color;
                float4 object_snapshot_ST;
                float3 bounds_center;
                float2 bounds_size;
                float2 uv_min;
                float2 uv_max;
            CBUFFER_END

            varyings vert(attributes IN)
            {
                varyings OUT;

                float3 cam_right = UNITY_MATRIX_V[0].xyz;
                float3 cam_up = UNITY_MATRIX_V[1].xyz;

                // allign quad position to camera. Warning, shader breaks when mesh isn't a standard quad
                float3 world_pos = bounds_center + (IN.position_os.x * cam_right * bounds_size.x) + (IN.position_os.y * cam_up * bounds_size.y);

                OUT.position_hcs = TransformWorldToHClip(world_pos); // convert world pos to screen pos
                OUT.uv = lerp(uv_min, uv_max, IN.uv); // align to cropped bounds of object (passed texture is a screen texture)

                return OUT;
            }

            half4 frag(varyings IN) : SV_Target
            {
                if (IN.uv.x < 0.0 || IN.uv.x > 1.0 || IN.uv.y < 0.0 || IN.uv.y > 1.0) // dont render out of bounds
                {
                    discard;
                }
                
                half4 color = SAMPLE_TEXTURE2D(object_snapshot, sampler_object_snapshot, IN.uv) * base_color;
                clip(color.a - 0.01);
                
                return color;
            }
            ENDHLSL
        }
    }
}