// Energía que fluye hacia arriba dentro de la ranura de una puerta (LevelDoor). Aditivo, sin textura ni luz:
// todo sale de ruido procedural, así que se adapta a cualquier tamaño de ranura (_Size en unidades).
//   _Progress: 0 = vacía, 1 = llena; el frente de la energía SUBE desde abajo (como fluye) al cerrarse.
//   _Tail: 0 = sin cola; al abrirse sube de 0 a 1 y vacía la energía de abajo hacia arriba.
//   _Intensity: brillo general (0 = apagada).
Shader "RedMagic/Fx Door Energy"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.8, 1, 1)
        _Core ("Núcleo", Color) = (0.85, 1, 1, 1)
        _Size ("Tamaño de la ranura (unidades)", Vector) = (0.3, 3, 0, 0)
        _Progress ("Progreso", Range(0, 1)) = 1
        _Tail ("Cola", Range(0, 1)) = 0
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
                float _Tail;
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
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);   // quíntica: sin la costura de cuadrícula de la cúbica
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 3 octavas, cada una girada: esconde los ejes de la cuadrícula del ruido
            float fbm(float2 p)
            {
                const float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
                float sum = 0.0, amp = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    sum += amp * vnoise(p);
                    p = mul(rot, p) * 2.03 + 17.1;
                    amp *= 0.5;
                }
                return sum / 0.875;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y * _Speed;
                float2 world = float2(uv.x * _Size.x, uv.y * _Size.y);

                // hebras que suben: ruido fino (en la ranura de ~0,3 de ancho caben varias hebras), alargado en vertical y
                // deformado por otro ruido para que serpentee en vez de verse como bloques
                float2 flow = float2(world.x * 30.0, world.y * 6.0 - t * 1.1);
                float2 warp = float2(fbm(flow * 0.45 + float2(0.0, -t * 0.35)), fbm(flow * 0.45 + 7.7)) - 0.5;
                float density = fbm(flow + warp * 2.2);
                float strands = smoothstep(0.28, 0.82, density);

                // más claro en el centro, se funde con el marco por los lados
                float side = 1.0 - abs(uv.x - 0.5) * 2.0;
                float edge = smoothstep(0.0, 0.22, side);
                float centre = pow(saturate(side), 1.6);

                // chispas: puntos suaves que suben, con posición propia dentro de su celda
                float2 cell = float2(world.x * 16.0, world.y * 5.0 - t * 1.8);
                float2 id = floor(cell);
                float2 offset = float2(hash21(id + 3.1), hash21(id + 8.7)) * 0.5 + 0.25;
                float spark = step(0.9, hash21(id)) * smoothstep(0.32, 0.0, length(frac(cell) - offset));

                // el frente sube desde abajo al cerrarse; al abrirse la cola sube y vacía la energía de abajo hacia arriba
                float reach = _Progress * 1.08;
                float mask = saturate((reach - uv.y) / 0.08);
                float tailMask = _Tail <= 0.001 ? 1.0 : saturate((uv.y - (_Tail * 1.08 - 0.08)) / 0.08);
                mask *= tailMask;
                float front = exp(-abs(reach - 0.04 - uv.y) * 28.0) * step(0.001, _Progress) * (1.0 - step(0.999, _Progress));

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
