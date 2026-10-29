Shader "NEFASA Eyes/URP Eye Surface"
{
    Properties
    {
        [NoScaleOffset] _IrisStructure("Original iris structure (data)", 2D) = "gray" {}
        [NoScaleOffset] _VesselMap("Original vessel branches (data)", 2D) = "black" {}
        [NoScaleOffset] _ScleraTissue("NEFASA scleral tissue (color)", 2D) = "white" {}
        _Region("Region: sclera 0, iris 1, pupil 2", Float) = 0
        _IrisColor("Iris color", Color) = (0.065,0.098,0.115,1)
        _InnerColor("Inner color", Color) = (0.139,0.106,0.063,1)
        _FiberDepth("Fiber depth", Range(0,1)) = 0.24
        _Vessels("Vessels", Range(0,1)) = 0.6
        _Pigmentation("Pigmentation", Range(0,1)) = 0.72
        _LimbalRing("Limbal ring", Range(0,1)) = 0.58
        _Gloss("Gloss", Range(0,1)) = 0.72
        _Moisture("Moisture", Range(0,1)) = 0.65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
        Pass
        {
            Name "EyeSurface"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EyeVertex
            #pragma fragment EyeFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "EyeCommon.hlsl"

            half4 EyeFragment(EyeVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.normalWS);
                SurfaceData surface = (SurfaceData)0;
                surface.alpha = 1;
                surface.occlusion = 1;
                surface.normalTS = half3(0,0,1);
                surface.specular = half3(0.015,0.015,0.015);
                if (_Region < 0.5)
                {
                    float vessel = SAMPLE_TEXTURE2D(_VesselMap, sampler_VesselMap, input.uv).r;
                    float3 tissueColor = SAMPLE_TEXTURE2D(_ScleraTissue, sampler_ScleraTissue, input.uv).rgb;
                    float vesselAmount = saturate(vessel * _Vessels * 1.6);
                    surface.albedo = lerp(tissueColor, half3(0.34,0.063,0.053), vesselAmount * 0.78);
                    surface.smoothness = 0.55 + 0.24 * _Moisture;
                    surface.specular = half3(0.025,0.025,0.025);
                    normal = EyeMicroNormal(input.positionWS,normal,NEFASAEyeSoftNoise(input.uv*183.0)*0.00000065);
                }
                else if (_Region < 1.5)
                {
                    float4 tissue = SAMPLE_TEXTURE2D(_IrisStructure, sampler_IrisStructure, input.uv);
                    float radius = length(input.uv * 2 - 1);
                    float fibers = lerp(0.60,1.38,smoothstep(0.16,0.84,tissue.r));
                    float pigment = saturate(tissue.b * _Pigmentation * 1.2);
                    float center = 1 - smoothstep(0.38,0.77,radius);
                    float3 color = lerp(_IrisColor.rgb, _InnerColor.rgb, saturate(center * 0.40 + pigment * 0.65));
                    color *= fibers;
                    color *= 1 - saturate(tissue.g) * 0.38;
                    float2 radialUV=input.uv*2-1;
                    float angle=atan2(radialUV.y,radialUV.x);
                    float organicRadius=radius+0.005*sin(angle*17.0)+0.003*sin(angle*31.0);
                    float rim = smoothstep(0.79,0.99,organicRadius) * _LimbalRing;
                    color = lerp(color, color * 0.28, rim);
                    float ruff = 1 - smoothstep(0.345,0.40,radius);
                    color = lerp(color, color * 0.25, ruff);
                    surface.albedo = color;
                    surface.smoothness = 0.27 + _Gloss * 0.12;
                    surface.specular = half3(0.014,0.014,0.014);
                    surface.occlusion = lerp(1,0.88,tissue.g);
                    normal = EyeMicroNormal(input.positionWS, normal, tissue.a * _FiberDepth * 0.000008);
                }
                else
                {
                    surface.albedo = half3(0.0005,0.00035,0.00025);
                    surface.specular = 0;
                    surface.smoothness = 0.05;
                }
                half4 result = UniversalFragmentPBR(EyeLighting(input, normal), surface);
                result.rgb = MixFog(result.rgb, input.fog);
                return result;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex EyeVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #include "EyeCommon.hlsl"
            half4 DepthFragment(EyeVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex EyeShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "EyeCommon.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            EyeVaryings EyeShadowVertex(EyeAttributes input)
            {
                EyeVaryings output = EyeVertex(input);
                float3 direction = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    direction = normalize(_LightPosition - output.positionWS);
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }
            half4 DepthFragment(EyeVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EyeVertex
            #pragma fragment EyeDepthNormals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "EyeCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 EyeDepthNormals(EyeVaryings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 encoded = PackNormalOctQuadEncode(normal) * 0.5 + 0.5;
                    return half4(PackFloat2To888(encoded),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
