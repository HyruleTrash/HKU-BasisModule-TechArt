Shader "Custom/MeltMatFX"
{
    Properties
    {
        [MainColor] base_color("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] object_snapshot("Object Snapshot", 2D) = "white" {}
        
        [HideInInspector] bounds_center("Bounds Center", Vector) = (0, 0, 0, 0)
        [HideInInspector] bounds_extents("Bounds Extents", Vector) = (0.5, 0.5, 0.5, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct attributes
            {
                float4 position_os : POSITION;
                float2 uv : TEXCOORD0; // Added UVs back to attributes
            };

            struct varyings
            {
                float4 position_hcs : SV_POSITION;
                float2 uv : TEXCOORD0; // Pass local UVs to fragment shader
            };

            TEXTURE2D(object_snapshot);
            SAMPLER(sampler_object_snapshot);

            CBUFFER_START(UnityPerMaterial)
                half4 base_color;
                float4 object_snapshot_ST;
                float4 bounds_center;
                float4 bounds_extents;
            CBUFFER_END

            varyings vert(attributes IN)
            {
                varyings OUT;

                float local_radius = length(bounds_extents.xyz);

                float scale_x = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float scale_y = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
                float scale_z = length(float3(UNITY_MATRIX_M._m02, UNITY_MATRIX_M._m12, UNITY_MATRIX_M._m22));
                float max_scale = max(scale_x, max(scale_y, scale_z));

                float3 center_ws = TransformObjectToWorld(bounds_center.xyz);
                float3 center_vs = TransformWorldToView(center_ws);
                
                // Billboard vertex offset
                float2 offset = IN.position_os.xy * (local_radius * max_scale * 2.2);
                float3 view_pos = center_vs + float3(offset, 0.0);

                OUT.position_hcs = mul(UNITY_MATRIX_P, float4(view_pos, 1.0));
                
                // Pass standard Quad UVs through
                OUT.uv = TRANSFORM_TEX(IN.uv, object_snapshot);

                return OUT;
            }

            half4 frag(varyings IN) : SV_Target
            {
                // Sample using the Quad's local UVs so the texture is glued to the 3D billboard surface
                half4 color = SAMPLE_TEXTURE2D(object_snapshot, sampler_object_snapshot, IN.uv) * base_color;
                
                // Discard empty transparent background pixels around the object
                clip(color.a - 0.01);
                
                return color;
            }
            ENDHLSL
        }
    }
}