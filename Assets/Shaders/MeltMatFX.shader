Shader "Custom/MeltMatFX"
{
    Properties
    {
        [MainColor] base_color("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] base_map("Base Map", 2D) = "white" {}
        
        [HideInInspector] bounds_center("Bounds Center", Vector) = (0, 0, 0, 0)
        [HideInInspector] bounds_extents("Bounds Extents", Vector) = (0.5, 0.5, 0.5, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

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

            TEXTURE2D(base_map);
            SAMPLER(sampler_base_map);

            CBUFFER_START(UnityPerMaterial)
                half4 base_color;
                float4 base_map_ST;
                float4 bounds_center;
                float4 bounds_extents;
            CBUFFER_END

            varyings vert(attributes IN)
            {
                varyings OUT;

                float3 center = bounds_center.xyz;
                float3 extents = bounds_extents.xyz;

                // 1. Define the 8 corners of the original mesh's local bounding box
                float3 corners[8];
                corners[0] = center + float3(-extents.x, -extents.y, -extents.z);
                corners[1] = center + float3( extents.x, -extents.y, -extents.z);
                corners[2] = center + float3(-extents.x,  extents.y, -extents.z);
                corners[3] = center + float3( extents.x,  extents.y, -extents.z);
                corners[4] = center + float3(-extents.x, -extents.y,  extents.z);
                corners[5] = center + float3( extents.x, -extents.y,  extents.z);
                corners[6] = center + float3(-extents.x,  extents.y,  extents.z);
                corners[7] = center + float3( extents.x,  extents.y,  extents.z);

                // 2. Transform all 8 corners into View Space to find the 2D min/max footprint
                float min_x = 999999.0;
                float max_x = -999999.0;
                float min_y = 999999.0;
                float max_y = -999999.0;

                for(int i = 0; i < 8; i++)
                {
                    float3 world_pos = TransformObjectToWorld(corners[i]);
                    float3 view_pos = TransformWorldToView(world_pos);

                    min_x = min(min_x, view_pos.x);
                    max_x = max(max_x, view_pos.x);
                    min_y = min(min_y, view_pos.y);
                    max_y = max(max_y, view_pos.y);
                }

                // 3. Anchor the billboard depth to the center of the original object
                float3 center_world = TransformObjectToWorld(center);
                float3 center_view = TransformWorldToView(center_world);
                float target_z = center_view.z;

                // 4. Map the Quad's standard bounds (-0.5 to 0.5) to a 0.0 to 1.0 range
                float2 normalized_quad_pos = IN.position_os.xy + 0.5;

                // 5. Stretch the Quad's vertices exactly to the View-Space bounding box
                float final_x = lerp(min_x, max_x, normalized_quad_pos.x);
                float final_y = lerp(min_y, max_y, normalized_quad_pos.y);
                float3 final_view_pos = float3(final_x, final_y, target_z);

                // 6. Transform direct from View Space to Clip Space
                OUT.position_hcs = mul(UNITY_MATRIX_P, float4(final_view_pos, 1.0));
                OUT.uv = TRANSFORM_TEX(IN.uv, base_map);

                return OUT;
            }

            half4 frag(varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(base_map, sampler_base_map, IN.uv) * base_color;
                return color;
            }
            ENDHLSL
        }
    }
}