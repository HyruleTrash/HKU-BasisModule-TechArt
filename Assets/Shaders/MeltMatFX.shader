Shader "Custom/MeltMatFX"
{
    Properties
    {
        [MainColor] base_color("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] object_snapshot("Object Snapshot", 2D) = "white" {}
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

            TEXTURE2D(object_snapshot);
            SAMPLER(sampler_object_snapshot);

            CBUFFER_START(UnityPerMaterial)
                half4 base_color;
                float4 object_snapshot_ST;
                float3 world_center;
                float2 world_size;
                float2 uv_min;
                float2 uv_max;
            CBUFFER_END

            varyings vert(attributes IN)
            {
                varyings OUT;

                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;

                float3 world_pos = world_center + (IN.position_os.x * right * world_size.x) + (IN.position_os.y * up * world_size.y);

                OUT.position_hcs = TransformWorldToHClip(world_pos);
                OUT.uv = lerp(uv_min, uv_max, IN.uv);

                return OUT;
            }

            half4 frag(varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(object_snapshot, sampler_object_snapshot, IN.uv) * base_color;
                clip(color.a - 0.01);
                
                return color;
            }
            ENDHLSL
        }
    }
}