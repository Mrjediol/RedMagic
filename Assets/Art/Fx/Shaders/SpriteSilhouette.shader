// Silueta plana de un sprite para URP (2D incluido): pinta el color del SpriteRenderer donde el
// sprite tiene alfa, ignorando sus colores. Sirve para bordes (varias copias desplazadas detrás del
// cuerpo) y destellos de cuerpo entero — el borde rojo del parry (PlayerParry).
// Mezcla alfa normal. Igual que SpriteAdditive, el color del SpriteRenderer llega en unity_SpriteColor.
Shader "RedMagic/Sprite Silhouette"
{
    Properties
    {
        [MainTexture] [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            // Sin LightMode = SRPDefaultUnlit: lo pintan tanto el Renderer 2D como el Universal.
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();

                Varyings output;
                float3 position = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(position);
                output.uv = input.uv;   // sin _MainTex_ST: el SRP Batcher 2D no admite _ST/_TexelSize
                output.color = input.color * _Color * (half4)unity_SpriteColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                return half4(input.color.rgb, alpha * input.color.a);
            }
            ENDHLSL
        }
    }
}
