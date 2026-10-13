// SOLO PARA HORNEAR (lo usa Editor/DoorFlipbookBaker.cs con Graphics.Blit; no va en ningún material del juego).
// Dibuja UN fotograma del campo de densidad de la energía de la puerta: la misma idea que FxDoorEnergy (ruido fbm
// deformado por otro ruido) pero con ruido PERIÓDICO en X e Y (la loseta se repite sin costuras) y con la deformación
// moviéndose en un círculo según _Phase (0..1): el fotograma 0 y el último encajan, el bucle es perfecto.
// El desplazamiento hacia arriba NO se hornea: lo hace el shader de runtime, que sólo mueve el UV.
Shader "Hidden/RedMagic/Baking/DoorEnergyBake"
{
    Properties { _Phase ("Fase del bucle", Range(0, 1)) = 0 }

    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Phase;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // ruido de valor periódico: la retícula se envuelve cada "per" celdas
            float vnoiseP(float2 p, float2 per)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float2 i0 = fmod(fmod(i, per) + per, per), i1 = fmod(i0 + 1.0, per);
                float a = hash21(i0), b = hash21(float2(i1.x, i0.y)), c = hash21(float2(i0.x, i1.y)), d = hash21(i1);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 3 octavas exactas (x2): el periodo se duplica con la frecuencia, así que sigue siendo periódico
            float fbmP(float2 p, float2 per)
            {
                float sum = 0.0, amp = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    sum += amp * vnoiseP(p, per);
                    p *= 2.0;
                    per *= 2.0;
                    amp *= 0.5;
                }
                return sum / 0.875;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                // la loseta cubre 10 celdas en X y 6 en Y: lo mismo que el shader vivo en una ranura de 0,32 × 1 unidad
                float2 p = float2(i.uv.x * 10.0, i.uv.y * 6.0);
                float a = 6.2831853 * _Phase;
                float2 off = float2(cos(a), sin(a)) * 0.7;

                float2 wp = p * 0.5;   // periodo (5, 3)
                float2 warp = float2(fbmP(wp + off, float2(5, 3)), fbmP(wp + 7.0 + off.yx, float2(5, 3))) - 0.5;
                float d = fbmP(p + warp * 2.2, float2(10, 6));
                return fixed4(d, d, d, 1);
            }
            ENDCG
        }
    }
}
