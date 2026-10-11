// Greenhaven/WindLit — low-cost URP lit shader (Lambert + ambient SH + main-light shadows + fog, like
// Simple Lit without specular) with world-space wind deformation driven by WindManager globals.
//
// Per-vertex wind data in UV2 (TEXCOORD2, float4), baked by the procedural generators:
//   x = trunk bend    0 at the roots → 1 at the top. Whole-plant sway, coherent across the plant.
//   y = canopy weight 0 near the trunk/inner canopy → 1 at the outer canopy (grass: blade tips).
//   z = cluster phase 0..1, constant per foliage cluster (or grass blade) so clusters move as groups,
//                     each slightly out of step with its neighbours.
//   w = flutter       small leaf shimmer (0 for wood).
// Displacement is a function of world position + this data, so coincident vertices stay together
// (no cracks) and static batching / the SRP Batcher keep working. Roots never move (x = y = w = 0).
Shader "Greenhaven/WindLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Header(Whole plant)]
        _TrunkSway("Trunk Sway (m at the top)", Range(0, 1)) = 0.12
        _SwayFrequency("Sway Frequency", Range(0.1, 4)) = 0.9
        [Header(Canopy)]
        _CanopySway("Outer Canopy Movement (m)", Range(0, 1)) = 0.35
        _InnerCanopy("Inner Canopy Movement (relative)", Range(0, 1)) = 0.25
        _GustResponse("Extra Canopy Response to Gusts", Range(0, 2)) = 0.6
        _Variation("Per-Tree Variation", Range(0, 2)) = 1
        [Header(Leaves)]
        _FlutterAmplitude("Flutter Amplitude (m)", Range(0, 0.1)) = 0.02
        _FlutterFrequency("Flutter Frequency", Range(1, 12)) = 5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            float _TrunkSway;
            float _SwayFrequency;
            float _CanopySway;
            float _InnerCanopy;
            float _GustResponse;
            float _Variation;
            float _FlutterAmplitude;
            float _FlutterFrequency;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Set once per frame by WindManager (all zero = no wind, rest pose).
        float4 _GH_WindDirection; // xyz = normalised direction
        float4 _GH_WindParams;    // x = strength 0..1 (incl. gusts), y = phase (s), z = gust 0..1, w = enabled 0/1
        float4 _GH_WindQuality;   // x = flutter scale, y = secondary (cross-wind) scale

        float3 ApplyWind(float3 positionWS, float4 wind)
        {
            float enabled = _GH_WindParams.w;
            float strength = _GH_WindParams.x * enabled;
            float time = _GH_WindParams.y;
            float gust = _GH_WindParams.z * enabled;
            float3 dir = _GH_WindDirection.xyz;
            float3 across = float3(-dir.z, 0.0, dir.x);

            // 1. Whole-plant sway: long wavelength so one tree moves coherently; biased downwind.
            float trunkPhase = time * _SwayFrequency + dot(positionWS.xz, float2(0.07, 0.05)) * _Variation;
            float trunkSway = (sin(trunkPhase) * 0.5 + 0.5) * 0.6 + 0.4;
            float3 offset = dir * (wind.x * _TrunkSway * strength * trunkSway);

            // 2. Canopy clusters: calm inside, lively outside; each cluster has its own phase, with a
            //    small cross-wind component so motion orbits slightly instead of rocking stiffly.
            float canopy = wind.y * lerp(wind.y * _InnerCanopy, 1.0, wind.y);
            float clusterPhase = time * _SwayFrequency * 1.6 + wind.z * 6.2831 + dot(positionWS.xz, float2(0.21, 0.17)) * _Variation;
            float push = sin(clusterPhase) * 0.6 + 0.4;
            float sideways = sin(clusterPhase * 0.73 + 1.3) * 0.35 * _GH_WindQuality.y;
            float amplitude = _CanopySway * canopy * strength * (1.0 + gust * _GustResponse);
            offset += (dir * push + across * sideways) * amplitude;
            offset.y -= abs(amplitude * push) * 0.15; // arc, not stretch

            // 3. Leaf flutter: tiny and quick.
            float f = sin(time * _FlutterFrequency + wind.z * 12.0 + dot(positionWS, float3(3.1, 2.3, 2.7)));
            offset += float3(f, 0.5 * f, -f) * (wind.w * _FlutterAmplitude * (0.25 + strength) * enabled * _GH_WindQuality.x);

            return positionWS + offset;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 wind : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.wind);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff);
                #endif
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = SampleSH(normalWS) + mainLight.color * (ndotl * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 color = MixFog(albedo.rgb * lighting, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 wind : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.wind);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 wind : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformWorldToHClip(ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.wind));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_TARGET
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 wind : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformWorldToHClip(ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.wind));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_TARGET
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
