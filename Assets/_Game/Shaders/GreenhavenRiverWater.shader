// Greenhaven/RiverWater — stylised flowing water for URP (no Shader Graph, no textures, no screen-space effects).
//
// Mesh data (baked by the river generator):
//   UV0 (TEXCOORD0)  x = metres across the river (0 at the centre line), y = metres along it (downstream = +y).
//   UV2 (TEXCOORD2)  x = foam amount 0..1 (banks, bends, rocks, fast water)
//                    y = signed flow speed factor (segment speed; negative flows upstream along y)
//                    z = depth 0 (edge) .. 1 (centre) for colour, opacity and edge darkening.
// Flow uses the two-phase flow-map technique: two copies of the ripples scroll downstream half a cycle apart
// and cross-fade, so the pattern never stretches no matter how long the game runs or how speeds differ
// between segments. All animation runs from _Time (frame-rate independent); the mesh never changes.
Shader "Greenhaven/RiverWater"
{
    Properties
    {
        [Header(Colour)]
        _ShallowColor("Shallow Colour", Color) = (0.38, 0.86, 0.80, 1)
        _DeepColor("Deep Colour", Color) = (0.10, 0.47, 0.54, 1)
        _EdgeDarken("Shallow Edge Darkening", Range(0, 1)) = 0.22
        _ShallowAlpha("Edge Opacity", Range(0, 1)) = 0.6
        _DeepAlpha("Centre Opacity", Range(0, 1)) = 0.9
        [Header(Flow)]
        _FlowSpeed("Flow Speed (m per s)", Range(0, 4)) = 0.9
        _FlowPeriod("Flow Cycle (s)", Range(0.5, 6)) = 2.2
        [Header(Ripples)]
        _RippleScale("Ripple Scale (per m)", Range(0.1, 4)) = 1.3
        _RippleStrength("Ripple Strength", Range(0, 2)) = 0.8
        _Brightness("Brightness Variation", Range(0, 1)) = 0.3
        _HighlightStrength("Highlights", Range(0, 2)) = 0.9
        [Header(Foam)]
        _FoamColor("Foam Colour", Color) = (0.95, 0.98, 1, 1)
        _FoamAmount("Foam Amount", Range(0, 2)) = 1
        _FoamScale("Foam Scale (per m)", Range(0.2, 6)) = 1.7
        [Toggle(_RIVER_LOW)] _LowQuality("Low Quality (one ripple layer, no specular)", Float) = 0
        [HideInInspector] _TimeOffset("Time Offset (s)", Float) = 0
        [HideInInspector] _SrcBlend("Src Blend", Float) = 5
        [HideInInspector] _DstBlend("Dst Blend", Float) = 10
        [HideInInspector] _ZWrite("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _RIVER_LOW
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _EdgeDarken;
                half _ShallowAlpha;
                half _DeepAlpha;
                float _FlowSpeed;
                float _FlowPeriod;
                float _RippleScale;
                half _RippleStrength;
                half _Brightness;
                half _HighlightStrength;
                half4 _FoamColor;
                half _FoamAmount;
                float _FoamScale;
                half _LowQuality;
                float _TimeOffset;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 river : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 river : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Soft, slightly stretched ripple pattern (stretched along the flow so it reads as moving water).
            float Ripples(float2 p)
            {
                float2 q = float2(p.x, p.y * 0.6);
                #if defined(_RIVER_LOW)
                    return Noise(q);
                #else
                    return Noise(q) * 0.65 + Noise(q * 2.1 + 5.2) * 0.35;
                #endif
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.uv = input.uv;
                output.river = input.river;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float depth = saturate(input.river.z);
                float foamMask = saturate(input.river.x);
                float speed = _FlowSpeed * input.river.y;

                // Two phases half a cycle apart, cross-faded with triangle weights.
                float cycle = (_Time.y + _TimeOffset) / _FlowPeriod;
                float phaseA = frac(cycle), phaseB = frac(cycle + 0.5);
                float weightA = 1.0 - abs(1.0 - 2.0 * phaseA);
                float weightB = 1.0 - weightA;
                float shiftA = phaseA * _FlowPeriod * speed, shiftB = phaseB * _FlowPeriod * speed;

                float2 p = input.uv * _RippleScale;
                float ripple = Ripples(p - float2(0, shiftA * _RippleScale)) * weightA
                             + Ripples(p - float2(0, shiftB * _RippleScale) + float2(0.37, 0.71)) * weightB;
                #if !defined(_RIVER_LOW)
                    // Second, finer layer at an angle and a little faster.
                    float2 p2 = float2(p.x * 2.3 + p.y * 0.4, p.y * 2.1 - p.x * 0.3);
                    float s2 = _RippleScale * 2.1 * 1.35;
                    float ripple2 = Ripples(p2 - float2(0, shiftA * s2) + float2(3.1, 1.7)) * weightA
                                  + Ripples(p2 - float2(0, shiftB * s2) + float2(4.2, 2.9)) * weightB;
                    ripple = ripple * 0.6 + ripple2 * 0.4;
                    float lines = pow(1.0 - abs(2.0 * ripple2 - 1.0), 8.0);
                #else
                    float lines = pow(1.0 - abs(2.0 * ripple - 1.0), 8.0);
                #endif

                // Base colour: turquoise shallows to deep blue-green, darker right at the banks.
                half3 baseColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.0, 1.0, depth));
                baseColor *= lerp(1.0 - _EdgeDarken, 1.0, saturate(depth * 1.6));
                baseColor *= 1.0 + (ripple - 0.5) * _Brightness * 2.0;

                Light mainLight = GetMainLight();
                float3 normalWS = float3(0, 1, 0);
                half3 highlight = 0;
                float sparkle = smoothstep(0.72, 0.93, ripple);
                #if defined(_RIVER_LOW)
                    highlight = mainLight.color * sparkle * 0.3 * _HighlightStrength;
                #else
                    // Normal from screen-space derivatives of the ripple height (cheap, no extra noise samples).
                    float h = ripple * _RippleStrength * 0.12;
                    float3 tx = ddx(input.positionWS) + float3(0, ddx(h), 0);
                    float3 ty = ddy(input.positionWS) + float3(0, ddy(h), 0);
                    float3 n = cross(ty, tx);
                    normalWS = normalize(n * (n.y < 0 ? -1.0 : 1.0) + float3(0, 1e-4, 0));
                    float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                    float3 halfDir = normalize(mainLight.direction + viewDir);
                    float spec = pow(saturate(dot(normalWS, halfDir)), 48.0);
                    highlight = mainLight.color * (spec * 0.8 + sparkle * 0.25) * _HighlightStrength;
                #endif

                half ndotl = saturate(dot(normalWS, mainLight.direction)) * 0.45 + 0.55;
                half3 ambient = SampleSH(normalWS);
                half3 lit = baseColor * (ambient * 0.35 + mainLight.color * ndotl * 0.85);
                lit += mainLight.color * lines * _RippleStrength * 0.2 * (0.5 + 0.5 * depth);

                // Foam streaks drifting with the flow, only where the mesh asks for them.
                float2 fp = float2(input.uv.x * _FoamScale * 2.4, input.uv.y * _FoamScale * 0.22);
                float fs = _FoamScale * 0.22;
                float2 fa = fp - float2(0, shiftA * fs), fb = fp - float2(0, shiftB * fs) + float2(0.53, 0.29);
                float foamNoise = (Noise(fa) * 0.6 + Noise(fa * 2.3 + 1.7) * 0.4) * weightA + (Noise(fb) * 0.6 + Noise(fb * 2.3 + 1.7) * 0.4) * weightB;
                float foam = smoothstep(0.58, 0.88, foamNoise * 0.7 + foamMask * 0.45) * saturate(foamMask * 2.2) * _FoamAmount;
                foam = saturate(foam) * 0.85;

                half3 foamLit = _FoamColor.rgb * (ambient * 0.5 + mainLight.color * 0.65);
                half3 color = lerp(lit + highlight, foamLit, foam);
                half alpha = saturate(lerp(_ShallowAlpha, _DeepAlpha, saturate(depth * 1.4)) + foam * 0.5 + dot(highlight, half3(0.1, 0.1, 0.1)));
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
