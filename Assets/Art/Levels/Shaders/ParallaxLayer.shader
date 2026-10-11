// Capa de fondo de parallax (RedMagic.Levels.ParallaxBackgroundLayer). Sin luz.
// La repetición horizontal se hace aquí y no con el wrap de la textura, para poder elegir por capa
// entre repetir y espejo: _UV = (u inicial, u por unidad de la malla, modo 0 repetir / 1 espejo / 2 una vez,
// 1 = voltear). _Fog = color de niebla y cuánto se mezcla (Atenuar). _Alpha = opacidad.
Shader "RedMagic/Parallax Layer"
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
            float4 _Fog;
            float _Alpha;

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float raw = _UV.x + input.uv.x * _UV.y;
                float u = raw;
                if (_UV.z < 0.5) u = frac(raw);
                else if (_UV.z < 1.5) { float m = frac(raw * 0.5) * 2.0; u = m < 1.0 ? m : 2.0 - m; }
                else u = saturate(raw);
                if (_UV.w > 0.5) u = 1.0 - u;

                // Derivadas de la u SIN envolver: si no, en cada costura el mip salta y deja una línea.
                float2 uvRaw = float2(raw, input.uv.y);
                half4 c = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, float2(u, input.uv.y), ddx(uvRaw), ddy(uvRaw));
                c.rgb = lerp(c.rgb, _Fog.rgb, _Fog.a);
                c.a *= _Alpha;
                return c;
            }
            ENDHLSL
        }
    }
}
