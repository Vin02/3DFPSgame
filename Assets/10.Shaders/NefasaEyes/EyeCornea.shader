Shader "Custom/NefasaEyeCornea"
{
    Properties
    {
        _IrisColor("Iris color", Color) = (0.065,0.098,0.115,1)
        _InnerColor("Inner color", Color) = (0.139,0.106,0.063,1)
        _Region("Region", Float) = 3
        _FiberDepth("Fiber depth", Range(0,1)) = 0.24
        _Vessels("Vessels", Range(0,1)) = 0.3
        _Pigmentation("Pigmentation", Range(0,1)) = 0.72
        _LimbalRing("Limbal ring", Range(0,1)) = 0.58
        _Gloss("Gloss", Range(0,1)) = 0.72
        _Moisture("Moisture", Range(0,1)) = 0.65
        _FogAmount("Fog Amount", Range(0,1)) = 1
        _SelfLit("Self Lit", Range(0,2)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "CornealReflection"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One One ZWrite Off Cull Back ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EyeVertex
            #pragma fragment CorneaFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "EyeCommon.hlsl"
            half4 CorneaFragment(EyeVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = 0;
                surface.specular = half3(0.02497,0.02497,0.02497);
                surface.smoothness = clamp(0.77 + _Gloss * 0.20 + _Moisture * 0.045,0.77,0.985);
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                surface.alpha = 1;
                half4 result = UniversalFragmentPBR(EyeLighting(input, normalize(input.normalWS)), surface);
                result.rgb *= lerp(0.70,1.0,_Moisture);
                result.rgb = lerp(result.rgb, MixFogColor(result.rgb, half3(0,0,0), input.fog), _FogAmount);
                return half4(result.rgb,0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
