// VARIANTE HORNEADA de FxDoorEnergy (prueba de "flipbook"). Mismas propiedades que el original (_Color, _Core, _Size,
// _Progress, _Tail, _Intensity, _Speed) para que LevelDoor la use sin cambios; en vez de calcular el ruido en cada píxel
// (9 evaluaciones de ruido), lee 2 muestras de un Texture2DArray horneado (_Flip) y mezcla los dos fotogramas vecinos.
// Todo lo demás (bordes, frente, cola, chispas) sigue siendo cálculo barato en vivo.
Shader "RedMagic/Fx Door Energy Flipbook"
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
        [NoScaleOffset] _Flip ("Flipbook (Texture2DArray)", 2DArray) = "" {}
        _Frames ("Fotogramas", Float) = 16
        _LoopSeconds ("Segundos del bucle", Float) = 6
        _TileSize ("Tamaño de la loseta horneada (unidades)", Vector) = (0.32, 1, 0, 0)
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
            #pragma target 3.5
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_Flip);
            SAMPLER(sampler_Flip);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Core;
                float4 _Size;
                float _Progress;
                float _Tail;
                float _Intensity;
                float _Speed;
                float _Frames;
                float _LoopSeconds;
                float4 _TileSize;
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

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y * _Speed;
                float2 world = float2(uv.x * _Size.x, uv.y * _Size.y);

                // densidad horneada: el ruido sube desplazando el UV (0,1833 u/s a velocidad 1, como el shader vivo) y la
                // deformación lenta sale del fotograma; se mezclan los dos vecinos para que no salte
                float2 tuv = float2(world.x / _TileSize.x, world.y / _TileSize.y - t * 0.1833);
                float fi = frac(_Time.y / _LoopSeconds) * _Frames;
                float i0 = floor(fi);
                float i1 = fmod(i0 + 1.0, _Frames);
                float d0 = SAMPLE_TEXTURE2D_ARRAY(_Flip, sampler_Flip, tuv, i0).r;
                float d1 = SAMPLE_TEXTURE2D_ARRAY(_Flip, sampler_Flip, tuv, i1).r;
                float density = lerp(d0, d1, fi - i0);
                float strands = smoothstep(0.28, 0.82, density);

                float side = 1.0 - abs(uv.x - 0.5) * 2.0;
                float edge = smoothstep(0.0, 0.22, side);
                float centre = pow(saturate(side), 1.6);

                // chispas (en vivo: son baratas)
                float2 cell = float2(world.x * 16.0, world.y * 5.0 - t * 1.8);
                float2 id = floor(cell);
                float2 offset = float2(hash21(id + 3.1), hash21(id + 8.7)) * 0.5 + 0.25;
                float spark = step(0.9, hash21(id)) * smoothstep(0.32, 0.0, length(frac(cell) - offset));

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
