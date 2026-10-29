#ifndef NEFASA_EYE_COMMON_INCLUDED
#define NEFASA_EYE_COMMON_INCLUDED
#define _SPECULAR_SETUP 1
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

TEXTURE2D(_IrisStructure); SAMPLER(sampler_IrisStructure);
TEXTURE2D(_VesselMap); SAMPLER(sampler_VesselMap);
TEXTURE2D(_ScleraTissue); SAMPLER(sampler_ScleraTissue);
CBUFFER_START(UnityPerMaterial)
float4 _IrisColor;
float4 _InnerColor;
float _Region;
float _FiberDepth;
float _Vessels;
float _Pigmentation;
float _LimbalRing;
float _Gloss;
float _Moisture;
CBUFFER_END

struct EyeAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct EyeVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    half fog : TEXCOORD3;
    float4 shadowCoord : TEXCOORD4;
    UNITY_VERTEX_OUTPUT_STEREO
};
EyeVaryings EyeVertex(EyeAttributes input)
{
    EyeVaryings output = (EyeVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = input.uv;
    output.fog = ComputeFogFactor(position.positionCS.z);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
        output.shadowCoord = ComputeScreenPos(position.positionCS);
    #else
        output.shadowCoord = TransformWorldToShadowCoord(position.positionWS);
    #endif
    return output;
}
InputData EyeLighting(EyeVaryings input, float3 normal)
{
    InputData data = (InputData)0;
    data.positionWS = input.positionWS;
    data.positionCS = input.positionCS;
    data.normalWS = normal;
    data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    data.shadowCoord = input.shadowCoord;
    data.fogCoord = input.fog;
    data.vertexLighting = VertexLighting(input.positionWS, normal);
    data.bakedGI = SampleSH(normal);
    data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    data.shadowMask = half4(1,1,1,1);
    return data;
}
float3 EyeMicroNormal(float3 position, float3 normal, float height)
{
    float3 px = ddx(position), py = ddy(position);
    float3 rx = cross(py, normal), ry = cross(normal, px);
    float determinant = dot(px, rx);
    float inverse = abs(determinant) > 1e-15 ? rcp(determinant) : 0;
    float3 gradient = (ddx(height) * rx + ddy(height) * ry) * inverse;
    return normalize(normal - gradient);
}
float NEFASAEyeHash21(float2 p)
{
    p=frac(p*float2(123.34,456.21));
    p+=dot(p,p+45.32);
    return frac(p.x*p.y);
}
float NEFASAEyeSoftNoise(float2 p)
{
    float2 i=floor(p),f=frac(p);
    f=f*f*(3.0-2.0*f);
    return lerp(lerp(NEFASAEyeHash21(i),NEFASAEyeHash21(i+float2(1,0)),f.x),lerp(NEFASAEyeHash21(i+float2(0,1)),NEFASAEyeHash21(i+1),f.x),f.y);
}
#endif
