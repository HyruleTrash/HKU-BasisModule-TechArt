Shader "UI/HDR_Glow_Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [HDR] _EmissionColor ("Glow Color (HDR)", Color) = (1,1,1,1)
        _Intensity ("Glow Intensity", Float) = 2.0
        _OutlineWidth ("Outline Blur Radius", Range(0, 15)) = 4.0

        // Required for UI Masking & Stencils
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // Automatically set by Unity (1/Width, 1/Height)
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _EmissionColor;
            float _Intensity;
            float _OutlineWidth;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Sample main texture color
                half4 mainColor = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                half coreAlpha = mainColor.a;

                // 8-tap sampling offsets for multi-direction radial blur
                float2 radius = _OutlineWidth * _MainTex_TexelSize.xy;
                float2 offsets[8] = {
                    float2(-0.7071, -0.7071),
                    float2( 0.0,    -1.0),
                    float2( 0.7071, -0.7071),
                    float2(-1.0,     0.0),
                    float2( 1.0,     0.0),
                    float2(-0.7071,  0.7071),
                    float2( 0.0,     1.0),
                    float2( 0.7071,  0.7071)
                };

                // Multi-sample alpha to calculate outer blur halo
                half blurAlphaOuter = 0.0;
                half blurAlphaInner = 0.0;

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    blurAlphaOuter += tex2D(_MainTex, IN.texcoord + offsets[i] * radius).a;
                    blurAlphaInner += tex2D(_MainTex, IN.texcoord + offsets[i] * radius * 0.5).a;
                }

                // Average and smooth blur gradient
                half blurAlpha = ((blurAlphaOuter / 8.0) + (blurAlphaInner / 8.0)) * 0.5;

                // Compute Glow HDR RGB
                half3 glowRGB = _EmissionColor.rgb * _Intensity;
                
                // Isolate outer glow factor (where core sprite ends)
                half glowFactor = saturate(blurAlpha - coreAlpha);

                // Blend: Core sprite renders over top, HDR blurred outline sits around it
                half3 finalRGB = lerp(glowRGB * _Intensity, mainColor.rgb, coreAlpha);
                half finalAlpha = saturate(coreAlpha + blurAlpha * _EmissionColor.a);

                half4 finalColor = half4(finalRGB, finalAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}