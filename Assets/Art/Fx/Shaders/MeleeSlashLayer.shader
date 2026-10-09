// Una capa de un tajo melee, dibujada por procedimiento sobre un quad (UV 0..1, radio 1 = borde).
// La forma es una media luna a lo largo de un arco: gruesa cerca de la cabeza, afilada en la cola.
// MeleeSlashFx mueve _Head/_Tail (0..1 a lo largo del arco) cada frame; varias capas apiladas con
// retardo y radio distintos dan el abanico de lenguas. Tonos planos, bordes nítidos (AA por fwidth).
// _Fray deshilacha la cola en lenguas tangenciales (cada banda radial acaba a distinta altura).
Shader "RedMagic/Melee Slash Layer"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Radius ("Outer radius", Range(0.1, 1)) = 0.85
        _Thickness ("Max thickness", Range(0, 0.8)) = 0.25
        _ArcStart ("Arc start (rad)", Float) = 0
        _ArcSpan ("Arc span (rad)", Float) = 3.8
        _Head ("Head (0..1)", Float) = 1
        _Tail ("Tail (0..1)", Float) = 0
        _HeadTaper ("Head taper", Range(0.05, 3)) = 0.35
        _TailTaper ("Tail taper", Range(0.05, 4)) = 1.6
        _FraySpread ("Fray spread", Range(0, 1)) = 0.25
        _FrayFreq ("Fray bands", Range(1, 20)) = 6
        _OuterJag ("Outer jag", Range(0, 0.5)) = 0.05
        _InnerJag ("Inner jag", Range(0, 1)) = 0.2
        _Breakup ("Tail breakup", Range(0, 1)) = 0.3
        _Shards ("Shards per arc", Range(0, 40)) = 0
        _ShardDrift ("Shard drift", Range(0, 1)) = 0.25
        _ShardStyle ("Shard style (0 rombo, 1 rayas, 2 chispas, 3 trozos)", Float) = 0
        _Seed ("Seed", Float) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
        _ClipY ("Clip below world Y", Float) = -100000
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Radius, _Thickness, _ArcStart, _ArcSpan, _Head, _Tail;
                float _HeadTaper, _TailTaper, _FraySpread, _FrayFreq, _OuterJag, _InnerJag;
                float _Breakup, _Shards, _ShardDrift, _ShardStyle, _Seed, _Alpha, _ClipY;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float worldY : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.worldY = TransformObjectToWorld(input.positionOS).y;
                return o;
            }

            float Hash11(float n) { return frac(sin(n * 127.1 + _Seed * 311.7) * 43758.5453); }
            float Hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7)) + _Seed * 74.7) * 43758.5453); }

            float Noise1(float x)
            {
                float i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(Hash11(i), Hash11(i + 1), f);
            }

            float Noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }


            // Parte exterior: una partícula por celda del arco. dx/dy en unidades del quad (tangente/radial).
            // 0 rombos que se alejan · 1 rayas de velocidad pegadas al borde · 2 chispas que salen
            // despedidas desde la cabeza · 3 trozos que se desprenden de la cola al deshacerse.
            float Particle(float c, float t, float r, float aa)
            {
                if (Hash11(c + 3.1) < 0.4) return 0;
                float tc = (c + 0.5 + (Hash11(c + 1.3) - 0.5) * 0.6) / _Shards;
                float h1 = Hash11(c + 9.3), h2 = Hash11(c + 5.7);
                float arcR = _Radius * _ArcSpan;                      // longitud de arco por unidad de t
                float sd = -1;

                if (_ShardStyle < 0.5)                                // rombos
                {
                    float age = saturate((_Head - tc) * 3);
                    if (tc > _Head || tc < _Tail) return 0;
                    float rc = _Radius + _Thickness * (0.15 + h1 * 0.4) + age * _ShardDrift;
                    float len = (0.35 + h2 * 0.5) / _Shards * arcR, wid = _Thickness * 0.12 * (1 - age * 0.6);
                    float dx = (t - tc) * _ArcSpan * r, dy = r - rc;
                    sd = (1 - (abs(dx) / len + abs(dy) / wid)) * min(len, wid) * (1 - age);
                }
                else if (_ShardStyle < 1.5)                           // rayas de velocidad
                {
                    if (tc > _Head) return 0;
                    float age = saturate((_Head - tc) * 2.2 + _Tail * 1.5);
                    float lenT = (0.18 + h2 * 0.22) * (1 - age * 0.8);      // en fracción de arco, se acorta
                    float along = (tc - t) * _ArcSpan * r, L = lenT * arcR;
                    float u = along / max(L, 1e-4);
                    float rc = _Radius + _Thickness * (0.12 + h1 * 0.35) + age * _ShardDrift * 0.3;
                    float halfW = _Thickness * 0.1 * (1 - u) * saturate(u * 5);
                    sd = min(halfW - abs(r - rc), min(along, L - along));
                }
                else if (_ShardStyle < 2.5)                           // chispas
                {
                    if (tc > _Head) return 0;
                    float age = saturate((_Head - tc) * 2.5 + _Tail);
                    float pt = tc + age * (0.05 + h2 * 0.06);         // siguen hacia delante
                    float rc = _Radius + _Thickness * 0.05 + age * _ShardDrift * (0.5 + h1);
                    float len = _Thickness * (0.3 + h2 * 0.25) * (1 - age), wid = len * 0.32;
                    float dx = (t - pt) * _ArcSpan * r, dy = r - rc;
                    float q = sqrt((dx / max(len, 1e-4)) * (dx / max(len, 1e-4)) + (dy / max(wid, 1e-4)) * (dy / max(wid, 1e-4)));
                    sd = (1 - q) * wid;
                }
                else                                                  // trozos de la cola
                {
                    float age = saturate((_Tail - tc) * 4);
                    if (age <= 0 || age >= 1) return 0;
                    float pt = tc - age * 0.04;
                    float rc = _Radius - _Thickness * (0.1 + h1 * 0.3) + age * _ShardDrift * (0.4 + h2 * 0.6);
                    float size = _Thickness * (0.2 + h2 * 0.15) * (1 - age * 0.7);
                    float dx = (t - pt) * _ArcSpan * r, dy = r - rc;
                    float q = length(float2(dx, dy)) / max(size, 1e-4);
                    q += (Noise2(float2(dx, dy) / max(size, 1e-4) * 1.5 + c * 7.1) - 0.5) * 0.8;
                    sd = (1 - q) * size;
                }
                return saturate(sd / aa);
            }
            // w^a·(1-w)^b normalizado a pico 1: 0 en la cola y en la cabeza, máximo cerca de la cabeza.
            float Profile(float w)
            {
                if (w <= 0 || w >= 1) return 0;
                float a = _TailTaper, b = _HeadTaper;
                float peak = a / (a + b);
                float norm = pow(peak, a) * pow(1 - peak, b);
                return pow(w, a) * pow(1 - w, b) / norm;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv * 2 - 1) * 1.5;               // el quad mide ±1.5: margen para la parte exterior
                float r = length(p);
                float ang = atan2(p.y, p.x) - _ArcStart;
                ang = ang - TWO_PI * floor(ang / TWO_PI);      // 0..2π, sentido antihorario
                float t = ang / _ArcSpan;                      // 0..1 dentro del arco
                float aa = max(fwidth(r), 1e-4) * 1.2;

                // Posición radial normalizada respecto al grosor máximo: 0 borde exterior, 1 interior.
                float rn = (_Radius - r) / max(_Thickness, 1e-4);

                // Cola deshilachada: cada banda radial acaba a distinta altura → lenguas tangenciales.
                float fray = Noise1(rn * _FrayFreq);
                float tail = _Tail - fray * _FraySpread * saturate(_Tail * 4);   // el inicio del arco acaba en punta
                float head = _Head - Noise1(rn * _FrayFreq * 0.5 + 17) * _FraySpread * 0.15;
                float w = (t - tail) / max(head - tail, 1e-3);
                float prof = Profile(w);

                float rOut = _Radius + (Noise1(t * 14) - 0.5) * _OuterJag * _Thickness;
                // Borde interior en lóbulos (lenguas que asoman hacia dentro), más marcados hacia la cola.
                float lobes = Noise1(t * _FrayFreq * 1.5 + 5.3);
                lobes = lobes * lobes * 2 - 0.6;
                float rIn = _Radius - _Thickness * prof * (1 + lobes * _InnerJag * (1.2 - w))
                          + (Noise2(float2(t * 30, rn * 4)) - 0.5) * 0.15 * _InnerJag * _Thickness * prof;
                float body = saturate(min(rOut - r, r - rIn) / aa);
                body *= step(t, 1.0);

                // Rotura hacia la cola: huecos que crecen donde la capa ya ha pasado.
                float hole = Noise2(float2(t * 24, rn * 5));
                body *= step(_Breakup * saturate(1 - w * 1.4), hole);

                float shard = 0;
                if (_Shards > 0.5)
                {
                    float cell = floor(t * _Shards);
                    for (int j = -2; j <= 1; j++) shard = max(shard, Particle(cell + j, t, r, aa));
                }

                // Suelo: lo que queda por debajo de _ClipY no se dibuja (el tajo "choca" con el suelo).
                float ground = saturate((input.worldY - _ClipY) / 0.12);
                half a = saturate(max(body, shard)) * _Color.a * _Alpha * ground;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
