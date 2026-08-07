Shader "Custom/MeltMatFX" // A shader for triggering a DOOM melt effect
{
    Properties
    {
        [MainColor] base_color("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] [HideInInspector] object_snapshot("Object Snapshot", 2D) = "white" {}
        [Space]
        pixel_size("Pixel size (size of each row and column)", Float) = 16.0
        wave_frequency("Wave frequency", Float) = 0.5
        wave_height("Wave height", Float) = 0.5
        noise_impact("Noise impact", Float) = 0.5
        stretch_impact("Stretch impact", Float) = 0.5
        [Space]
        speed("Speed", Float) = 0.5
        speed_noise_impact("Speed noise, impact", Float) = 0.5
        [space]
        [Range(0, 1)] distort_threshold("Distortion threshold", Float) = 0.5
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
                float2 local_uv : TEXCOORD0;
                float2 screen_uv : TEXCOORD1;
                float elapsed_time : TEXCOORD2;
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
                float rng_seed;
                float pixel_size;
                float wave_frequency;
                float wave_height;
                float noise_impact;
                float stretch_impact;
                float speed;
                float speed_noise_impact;
                float start_time;
                float distort_threshold;
            CBUFFER_END
            
            // generates a value between 0 and 1 based on a seed
            float random_float(float seed)
            {
                float rand_hash = 12.9898;
                float rand_hash2 = 43758.5453;
                float result = cos(frac(sin(seed * rand_hash)) * rand_hash2);
                return result;
            }

            varyings vert(attributes IN)
            {
                varyings OUT;
                
                OUT.elapsed_time = _Time.y - start_time;
                
                float3 cam_right = UNITY_MATRIX_V[0].xyz;
                float3 cam_up = UNITY_MATRIX_V[1].xyz;

                // align quad position to camera. Warning, shader breaks when mesh isn't a standard quad
                float3 world_pos = bounds_center + (IN.position_os.x * cam_right * bounds_size.x) + (IN.position_os.y * cam_up * bounds_size.y);
                
                // stretch downwards effect
                float stretch_mask = 0.5 - IN.position_os.y; // mask, for only lower vertices
                float downward_stretch = OUT.elapsed_time * stretch_impact * stretch_mask * bounds_size.y; // multiplied by bounds to remove quad size dependancy
                world_pos -= cam_up * downward_stretch;

                OUT.position_hcs = TransformWorldToHClip(world_pos); // convert world pos to screen pos
                OUT.screen_uv = lerp(uv_min, uv_max, IN.uv); // align to cropped bounds of object (passed texture is a screen texture)
                OUT.local_uv = IN.uv;

                return OUT;
            }

            half4 frag(varyings IN) : SV_Target
            {
                if (IN.screen_uv.x < 0.0 || IN.screen_uv.x > 1.0 || IN.screen_uv.y < 0.0 || IN.screen_uv.y > 1.0) // dont render out of bounds
                    discard;

                float screen_uv_width = uv_max.x - uv_min.x; // how much width the obj has on the screen, (normalised)
                float screen_uv_height = uv_max.y - uv_min.y;
                float object_pixel_width = screen_uv_width * _ScreenParams.x;
                float object_pixel_height = screen_uv_height * _ScreenParams.y;
                
                // row calc
                float total_rows = max(1.0, floor(object_pixel_height / pixel_size));
                int row_index = (int)floor(IN.local_uv.y * total_rows);
                
                // column calc
                float total_columns = max(1.0, floor(object_pixel_width / pixel_size));
                int col_index = (int)floor(IN.local_uv.x * total_columns);
                float normalized_col_x = (float)col_index / total_columns;
                float col_noise = random_float(rng_seed + (float)col_index);
                
                // wave calc
                float wave_offset;
                if (random_float(rng_seed) > 0.5)
                    wave_offset = sin((normalized_col_x * wave_frequency) + rng_seed) * wave_height;
                else
                    wave_offset = cos((normalized_col_x * wave_frequency) + rng_seed) * wave_height;
                
                // extra noise
                float col_noise_offset = (col_noise - 0.5) * 2.0 * noise_impact;
                
                // final offset
                float speed_noise = (random_float(rng_seed + (float)col_index + 23.4694) + 1) * speed_noise_impact; // add seamingly random number to seed, then bring into positive space
                float column_speed = speed + speed_noise;
                float column_start_offset = wave_offset + col_noise_offset - wave_height; // subtracting wave to move each column into negative space fully
                
                float time_offset = IN.elapsed_time * column_speed;
                float final_offset = column_start_offset;
                
                // only start applying melt offset, if enough time has passed
                if (final_offset < -time_offset)
                    final_offset = -time_offset;
                final_offset += time_offset;
                float2 final_uv = IN.screen_uv + float2(0.0, final_offset);
                
                // color
                half4 color = SAMPLE_TEXTURE2D(object_snapshot, sampler_object_snapshot, final_uv);
                clip(color.a - 0.01);
                
                // calculation on how it roughly takes for the melt of this column to be finished
                float distance_to_travel = screen_uv_height - column_start_offset;
                float column_melt_time = distance_to_travel / max(0.001, column_speed);
                float effect_progress = saturate(IN.elapsed_time / column_melt_time);
                
                float normalized_row_y = ((float)row_index + 0.5) / total_rows;
                float used_distort_threshold = distort_threshold + (col_noise * 0.1);
                
                // use effect progress to distort
                if (effect_progress > used_distort_threshold && normalized_row_y < used_distort_threshold && color.a != 0)
                {
                    float distortion_strength = 0.05; // percentage of uv
                    float2 neighbor_offset = float2(col_noise, random_float(rng_seed + (float)row_index)) * distortion_strength;
                    
                    half4 neighbor_color = SAMPLE_TEXTURE2D(object_snapshot, sampler_object_snapshot, final_uv + neighbor_offset);
                    clip(neighbor_color.a - 0.01);
                    
                    return neighbor_color;
                }
                
                return color;
            }
            ENDHLSL
        }
    }
}