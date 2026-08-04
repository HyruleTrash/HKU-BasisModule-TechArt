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
                float world_radius;
                float4x4 capture_vp;
            CBUFFER_END

            varyings vert(attributes IN)
            {
                varyings OUT;

                // 1. Extract camera right and up vectors from the live view matrix for billboarding
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;

                // 2. Construct the billboard quad facing the camera using the scaled world radius (prevents cut-offs)
                float3 world_pos = world_center + (IN.position_os.x * right + IN.position_os.y * up) * (world_radius * 2.0);

                // 3. Render position uses the current live camera clip transformation (keeps it billboarded and visible)
                OUT.position_hcs = TransformWorldToHClip(world_pos);

                // 4. UV coordinates use the FROZEN capture VP matrix mapped against the world position
                float4 captured_cs = mul(capture_vp, float4(world_pos, 1.0));
                float2 ndc = captured_cs.xy / captured_cs.w;
                OUT.uv = ndc * 0.5 + 0.5;

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