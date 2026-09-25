// Sprite/partícula aditiva sin luz para URP (2D incluido): brillos, auras, destellos.
// Color × textura, suma sobre lo de detrás (Blend SrcAlpha One).
// SpriteRenderer: en Unity 6 su color llega en unity_SpriteColor (no en el color de vértice), así
// que se multiplica por él con _UseSpriteColor = 1 (por defecto). ParticleSystemRenderer manda el
// color en el vértice: su material pone _UseSpriteColor = 0.
Shader "RedMagic/Sprite Additive"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        [Toggle] _UseSpriteColor ("Use SpriteRenderer color", Float) = 1
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

        Blend SrcAlpha One
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
                float4 _MainTex_ST;
                half4 _Color;
                half _UseSpriteColor;
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
                float3 position = _UseSpriteColor > 0.5 ? UnityFlipSprite(input.positionOS, unity_SpriteProps.xy) : input.positionOS;
                output.positionCS = TransformObjectToHClip(position);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                half4 rendererColor = _UseSpriteColor > 0.5 ? (half4)unity_SpriteColor : half4(1, 1, 1, 1);
                output.color = input.color * _Color * rendererColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            }
            ENDHLSL
        }
    }
}
