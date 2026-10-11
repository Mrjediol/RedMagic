// Sombras de una capa de fondo (RedMagic.Levels.ParallaxBackgroundLayer), sin luz.
// _Mode 0: silueta de la imagen difuminada (9 muestras del alfa, radio _Blur en UV) → sombra proyectada en la pared.
// _Mode 1: elipse suave procedural (sin textura) → sombra de contacto en el suelo.
// _UV como en "RedMagic/Parallax Layer" (u inicial, u por unidad, modo, voltear). _Color = color y opacidad.
Shader "RedMagic/Parallax Shadow"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _UV;
            float4 _Color;
            float2 _Blur;
            float _Mode;

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                return o;
            }

            float AlphaAt(float2 uv)
            {
                float u = saturate(uv.x);
                if (_UV.w > 0.5) u = 1.0 - u;
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0.0;
                return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(u, uv.y), 2).a;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float a;
                if (_Mode > 0.5)
                {
                    float2 d = (input.uv - 0.5) * 2.0;
                    a = saturate(1.0 - dot(d, d));
                    a *= a;
                }
                else
                {
                    // El quad de la sombra es más grande que la imagen (margen = _Blur): se reescala a la UV de la imagen.
                    float2 uv = (input.uv - _Blur) / (1.0 - 2.0 * _Blur);
                    a = 0.0;
                    [unroll] for (int y = -1; y <= 1; y++)
                    [unroll] for (int x = -1; x <= 1; x++)
                        a += AlphaAt(uv + float2(x, y) * _Blur * 0.5);
                    a /= 9.0;
                }
                return half4(_Color.rgb, _Color.a * a);
            }
            ENDHLSL
        }
    }
}
