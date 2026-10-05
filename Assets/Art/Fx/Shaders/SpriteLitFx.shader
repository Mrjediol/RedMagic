// Sprite iluminado por Light2D (como Sprite-Lit-Default) + efectos de aspecto, todos opcionales:
// tono/saturación, borde interior, brillo propio pulsante en las zonas claras y destello diagonal.
// Con todos los valores en su defecto se ve igual que el shader normal.
Shader "RedMagic/Sprite Lit FX"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(Color)]
        _Hue("Tono (giro, 0 = igual)", Range(-0.5, 0.5)) = 0
        _Saturation("Saturación", Range(0, 2)) = 1
        _Brightness("Brillo", Range(0, 2)) = 1

        [Header(Borde interior)]
        [HDR] _RimColor("Color", Color) = (0, 0, 0, 0)
        _RimWidth("Grosor (texels)", Range(1, 40)) = 8

        [Header(Brillo propio en zonas claras)]
        [HDR] _GlowColor("Color", Color) = (0, 0, 0, 0)
        _GlowThreshold("Umbral de luminosidad", Range(0, 1)) = 0.6
        _GlowPulse("Velocidad del pulso (0 = fijo)", Range(0, 8)) = 2

        [Header(Destello diagonal)]
        [HDR] _SheenColor("Color", Color) = (0, 0, 0, 0)
        _SheenSpeed("Velocidad", Range(0.05, 4)) = 0.6
        _SheenWidth("Ancho", Range(0.05, 1)) = 0.25
        _SheenPause("Pausa entre pasadas (s)", Range(0, 6)) = 1.5

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
                float2 worldXY     : TEXCOORD7;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Hue, _Saturation, _Brightness;
                half4 _RimColor;
                half _RimWidth;
                half4 _GlowColor;
                half _GlowThreshold, _GlowPulse;
                half4 _SheenColor;
                half _SheenSpeed, _SheenWidth, _SheenPause;
            CBUFFER_END

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.worldXY = TransformObjectToWorld(input.positionOS).xy;
                return o;
            }

            half3 HueShift(half3 c, half h)
            {
                // Giro en el espacio YIQ: barato y sin ramas.
                const half3x3 toYIQ = half3x3(0.299, 0.587, 0.114, 0.596, -0.274, -0.322, 0.211, -0.523, 0.312);
                const half3x3 toRGB = half3x3(1.0, 0.956, 0.621, 1.0, -0.272, -0.647, 1.0, -1.107, 1.705);
                half3 yiq = mul(toYIQ, c);
                half a = h * 6.2831853;
                half s = sin(a), co = cos(a);
                yiq.yz = half2(yiq.y * co - yiq.z * s, yiq.y * s + yiq.z * co);
                return mul(toRGB, yiq);
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                const half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 main = input.color * tex;

                // Color
                main.rgb = HueShift(main.rgb, _Hue);
                half lum = dot(main.rgb, half3(0.299, 0.587, 0.114));
                main.rgb = lerp(lum.xxx, main.rgb, _Saturation) * _Brightness;

                // Borde interior: alfa propio menos el del vecino más transparente.
                half rim = 0;
                if (_RimColor.a > 0.001)
                {
                    float2 o = _MainTex_TexelSize.xy * _RimWidth;
                    half n = min(min(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(o.x, 0)).a,
                                     SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(o.x, 0)).a),
                                 min(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, o.y)).a,
                                     SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, o.y)).a));
                    rim = saturate(tex.a - n) * _RimColor.a;
                }

                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));

                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);
#if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);
                surfaceData.normalWS = input.normalWS;
#endif
                half4 result = CombinedShapeLightShared(surfaceData, inputData);

                // Lo que sigue se suma después de la luz: brilla también en zonas oscuras.
                half3 emit = rim * _RimColor.rgb;

                if (_GlowColor.a > 0.001)
                {
                    half pulse = _GlowPulse > 0 ? 0.65 + 0.35 * sin(_Time.y * _GlowPulse) : 1;
                    half bright = smoothstep(_GlowThreshold, saturate(_GlowThreshold + 0.25), lum);
                    emit += bright * pulse * _GlowColor.rgb * _GlowColor.a;
                }

                if (_SheenColor.a > 0.001)
                {
                    // Una banda diagonal que cruza el cuerpo (en coordenadas de mundo, continua entre partes) y descansa.
                    half travel = 1.0 / _SheenSpeed;
                    half t = fmod(_Time.y, travel + _SheenPause) / travel;          // >1 = pausa
                    half pos = (input.worldXY.x + input.worldXY.y * 0.6) * 0.5;
                    half d = abs(frac(pos) - (t * 1.6 - 0.3));
                    emit += smoothstep(_SheenWidth, 0, d) * _SheenColor.rgb * _SheenColor.a * step(t, 1.0);
                }

                result.rgb += emit * tex.a * input.color.a;
                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Hue, _Saturation, _Brightness;
                half4 _RimColor;
                half _RimWidth;
                half4 _GlowColor;
                half _GlowThreshold, _GlowPulse;
                half4 _SheenColor;
                half _SheenSpeed, _SheenWidth, _SheenPause;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
