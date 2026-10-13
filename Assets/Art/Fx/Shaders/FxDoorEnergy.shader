// Energía que fluye hacia arriba dentro de la ranura de una puerta (LevelDoor). Aditivo, sin textura ni luz:
// todo sale de ruido procedural, así que se adapta a cualquier tamaño de ranura (_Size en unidades).
//   _Progress: 0 = vacía, 1 = llena; el frente de la energía baja desde arriba (la puerta se "cierra").
//   _Intensity: brillo general (0 = apagada).
Shader "RedMagic/Fx Door Energy"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.8, 1, 1)
        _Core ("Núcleo", Color) = (0.85, 1, 1, 1)
        _Size ("Tamaño de la ranura (unidades)", Vector) = (0.3, 3, 0, 0)
        _Progress ("Progreso", Range(0, 1)) = 1
        _Intensity ("Intensidad", Range(0, 2)) = 1
        _Speed ("Velocidad", Float) = 1.4
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

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Core;
                float4 _Size;
                float _Progress;
                float _Intensity;
                float _Speed;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y * _Speed;
                float2 world = float2(uv.x * _Size.x, uv.y * _Size.y);

                // hebras verticales que suben: ruido alargado en vertical, dos capas a distinta velocidad
                float n1 = vnoise(float2(world.x * 7.0, world.y * 1.3 - t));
                float n2 = vnoise(float2(world.x * 13.0 + 5.0, world.y * 2.1 - t * 1.7));
                float density = saturate(n1 * 0.6 + n2 * 0.5);
                float strands = smoothstep(0.35, 0.85, density);

                // más claro en el centro, se funde con el marco por los lados
                float side = 1.0 - abs(uv.x - 0.5) * 2.0;
                float edge = smoothstep(0.0, 0.22, side);
                float centre = pow(saturate(side), 1.6);

                // chispas: puntos que suben
                float2 cell = float2(world.x * 9.0, world.y * 3.0 - t * 1.6);
                float spark = step(0.965, hash21(floor(cell))) * smoothstep(0.5, 0.0, length(frac(cell) - 0.5));

                // el frente baja desde arriba según el progreso
                float fromTop = 1.0 - uv.y;
                float reach = _Progress * 1.08;
                float mask = saturate((reach - fromTop) / 0.08);
                float front = exp(-abs(reach - 0.04 - fromTop) * 28.0) * step(0.001, _Progress) * (1.0 - step(0.999, _Progress));

                float body = 0.32 + 0.5 * strands + 0.35 * centre;
                float a = (body * mask + front * 0.9 + spark * mask) * edge * _Intensity;
                float glow = saturate(strands * strands + centre * 0.5 + spark + front);
                half3 rgb = lerp(_Color.rgb, _Core.rgb, glow);
                return half4(rgb, saturate(a));
            }
            ENDHLSL
        }
    }
}
