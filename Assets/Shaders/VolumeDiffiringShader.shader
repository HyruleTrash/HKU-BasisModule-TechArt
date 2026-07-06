Shader "Custom/VolumeDiffiringShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
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
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
            CBUFFER_END
            
            float4x4 _VolumeWorldToLocalMatrices[8];
            float _VolumeIDs[8];
            float4 _VolumeMins[8];
            float4 _VolumeMaxs[8];
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
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                
                float volumeID = -1.0;
                
                for (int i = 0; i < _VolumeCount; i++)
                {
                    // Convert fragment's world space coordinate into volume's local space, to account for rotation
                    float4 localPos = mul(_VolumeWorldToLocalMatrices[i], float4(IN.positionWS, 1.0));

                    // Check if fragment is within bounds
                    if (localPos.x >= _VolumeMins[i].x && localPos.x <= _VolumeMaxs[i].x &&
                        localPos.y >= _VolumeMins[i].y && localPos.y <= _VolumeMaxs[i].y &&
                        localPos.z >= _VolumeMins[i].z && localPos.z <= _VolumeMaxs[i].z)
                    {
                        volumeID = _VolumeIDs[i];
                        break;
                    }
                }
                
                if (volumeID == -1.0) return color;
                
                // Example for volume 0
                if (volumeID == 0.0) return float4(1,0,0,0);

                return color;
            }
            ENDHLSL
        }
    }
}
