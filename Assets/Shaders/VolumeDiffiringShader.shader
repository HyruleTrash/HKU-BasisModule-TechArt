Shader "Custom/VolumeDiffiringShader"
{
    Properties
    {
        [Header(Base visual minus 1)]
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [Header(Base visual 0)]
        [MainColor] _BaseColor0("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap0("Base Map", 2D) = "white" {}
        [Header(Base visual 1)]
        [MainColor] _BaseColor1("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap1("Base Map", 2D) = "white" {}
        [Header(Base visual 2)]
        [MainColor] _BaseColor2("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap2("Base Map", 2D) = "white" {}
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

            struct VolumeData
            {
                float4x4 worldToLocal;
                float4 minBounds;
                float4 maxBounds;
                float id;
                float3 padding;
            };
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            TEXTURE2D(_BaseMap0);
            TEXTURE2D(_BaseMap1);
            TEXTURE2D(_BaseMap2);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _BaseColor0;
                half4 _BaseColor1;
                half4 _BaseColor2;
                float4 _BaseMap_ST;
            CBUFFER_END
            
            StructuredBuffer<VolumeData> _Volumes;
            int _VolumeCount;
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float volume_id = -1.0;
                
                for (int i = 0; i < _VolumeCount; i++)
                {
                    VolumeData vol = _Volumes[i];
                    float4 localPos = mul(vol.worldToLocal, float4(IN.positionWS, 1.0));

                    // Check if fragment is within bounds
                    if (localPos.x >= vol.minBounds.x && localPos.x <= vol.maxBounds.x &&
                        localPos.y >= vol.minBounds.y && localPos.y <= vol.maxBounds.y &&
                        localPos.z >= vol.minBounds.z && localPos.z <= vol.maxBounds.z)
                    {
                        volume_id = vol.id;
                        break;
                    }
                }
                
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                if (volume_id == -1.0) return color;
                
                if (volume_id == 0.0) color = SAMPLE_TEXTURE2D(_BaseMap0, sampler_BaseMap, IN.uv) * _BaseColor0;
                if (volume_id == 1.0) color = SAMPLE_TEXTURE2D(_BaseMap1, sampler_BaseMap, IN.uv) * _BaseColor1;
                if (volume_id == 2.0) color = SAMPLE_TEXTURE2D(_BaseMap2, sampler_BaseMap, IN.uv) * _BaseColor2;

                return color;
            }
            ENDHLSL
        }
    }
}
