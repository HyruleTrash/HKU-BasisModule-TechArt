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
           
           float4 frag(Varyings input) : SV_Target0
           {
               UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
               
               float2 uv = input.texcoord.xy;
               half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
               
               // return half4(_SinTime.x * -1, 0, _SinTime.x * 1, 1) * color;
               return color;
           }

           ENDHLSL
       }
   }
}