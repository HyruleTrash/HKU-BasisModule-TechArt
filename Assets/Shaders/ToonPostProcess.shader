Shader "ToonPostProcess"
{
   SubShader
   {
       Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
       ZWrite Off Cull Off
       Pass
       {
           Name "ToonRenderPass"

           HLSLPROGRAM
           #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
           #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

           #pragma vertex Vert
           #pragma fragment frag
           
           Texture3D<float3> screen_color_pallet_texture;
           
           float4 frag(Varyings input) : SV_Target0
           {
               UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
               
               float2 uv = input.texcoord.xy;
               half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
               
               uint3 color255 = (uint3)round(clamp(color.rgb, 0.0f, 1.0f) * 255.0f);
               
               float3 mapped_color = screen_color_pallet_texture[color255];
               if (mapped_color.r < 0.0f)
               {
                   return color;
               }
               
               return half4(mapped_color / 255.0f, color.a);
           }

           ENDHLSL
       }
   }
}