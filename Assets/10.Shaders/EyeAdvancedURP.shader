// EYE ADVANCED 에셋의 눈 셰이더를 URP에서 쓸 수 있게 옮긴 것. (원본은 내장 파이프라인 서피스 셰이더라 URP에서 분홍색으로 나온다)
// 원본과 같은 텍스처·UV 계산으로 흰자, 홍채, 동공 크기, 각막/홍채 노말맵을 그린다.
// 시차(parallax)·피하 산란은 옮기지 않았다. 대신 흰자·홍채 뒤에서 비치는 빛(Glow Color)은 방출로 낸다.
// URP 조명(달빛·손전등·섬광탄)은 그대로 받는다.
Shader "Custom/EyeAdvancedURP"
{
    Properties
    {
        _scleraColor("Sclera Color", Color) = (0.95, 0.95, 0.95, 1)
        _irisColor("Iris Color", Color) = (1, 1, 1, 1)
        [HDR] _illumColor("Glow Color", Color) = (0, 0, 0, 0)
        _FogAmount("Fog Amount", Range(0, 1)) = 1

        _pupilSize("Pupil Dilation", Range(0, 1)) = 0.27
        _irisSize("Eye Iris Size", Range(1.5, 5)) = 1.88
        _scleraSize("Eye Sclera Size", Range(0.85, 2.2)) = 0.968
        _smoothness("Smoothness", Range(0, 1)) = 0.75
        _specsize("Specular", Range(0, 1)) = 0.9

        _IrisColorTex("Iris Color", 2D) = "white" {}
        _IrisTex("Iris Mask", 2D) = "white" {}
        _CorneaBump("Cornea Normal Map", 2D) = "bump" {}
        _EyeBump("Eye Normal Map", 2D) = "bump" {}
        _MainTex("Sclera Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);      SAMPLER(sampler_MainTex);
            TEXTURE2D(_IrisColorTex); SAMPLER(sampler_IrisColorTex);
            TEXTURE2D(_IrisTex);      SAMPLER(sampler_IrisTex);
            TEXTURE2D(_CorneaBump);   SAMPLER(sampler_CorneaBump);
            TEXTURE2D(_EyeBump);      SAMPLER(sampler_EyeBump);

            CBUFFER_START(UnityPerMaterial)
                half4 _scleraColor;
                half4 _irisColor;
                half4 _illumColor;
                half _FogAmount;
                half _pupilSize;
                half _irisSize;
                half _scleraSize;
                half _smoothness;
                half _specsize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 중심을 기준으로 UV를 size배 키운다. (원본 셰이더의 uv*size - (size-1)/2)
            float2 ScaleUV(float2 uv, float size)
            {
                return uv * size - (size - 1.0) * 0.5;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.uv;
                float2 scleraUV = ScaleUV(uv, _scleraSize);
                float2 irisMaskUV = ScaleUV(uv, _irisSize);

                // 흰자와 홍채 영역(흰자 텍스처의 알파가 홍채 자리)
                half4 sclera = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scleraUV);
                sclera.rgb = lerp(sclera.rgb, sclera.rgb * _scleraColor.rgb, _scleraColor.a);
                half irisMask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, irisMaskUV).a;

                // 노말: 흰자는 눈 전체 노말, 홍채 자리는 각막 노말
                half3 eyeBump = UnpackNormal(SAMPLE_TEXTURE2D(_EyeBump, sampler_EyeBump, scleraUV));
                half3 corneaBump = UnpackNormal(SAMPLE_TEXTURE2D(_CorneaBump, sampler_CorneaBump, irisMaskUV));
                half3 normalTS = normalize(lerp(eyeBump, corneaBump, irisMask));

                // 동공 크기에 맞춰 홍채 텍스처 UV를 늘이거나 줄인다. (원본과 같은 식)
                half uvMask = 1.0 - SAMPLE_TEXTURE2D(_IrisTex, sampler_IrisTex, uv).b;
                half iSize = _irisSize * 0.6;
                float2 irisUV = ScaleUV(uv, iSize);
                half pupil = lerp(lerp(0.5, 0.2, iSize / 5.0), lerp(1.2, 0.75, iSize / 5.0), _pupilSize);
                irisUV = irisUV * (-1.0 + uvMask * pupil) - 0.5 * uvMask * pupil;

                half4 iris = SAMPLE_TEXTURE2D(_IrisColorTex, sampler_IrisColorTex, irisUV);
                iris.rgb = lerp(iris.rgb, iris.rgb * _irisColor.rgb, _irisColor.a);

                half3 albedo = lerp(sclera.rgb, iris.rgb, irisMask);
                // 홍채 뒤에서 비치는 빛. 동공(알파가 높은 곳)에서는 새지 않는다.
                half3 emission = albedo * (2.0 * _illumColor.a) * _illumColor.rgb * irisMask * (1.0 - iris.a);
                emission += albedo * _illumColor.rgb * 0.5;

                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = normalize(TransformTangentToWorld(normalTS,
                    half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.specular = half3(_specsize, _specsize, _specsize) * 0.5;
                surfaceData.smoothness = _smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.emission = emission;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;

                half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
                color.rgb = lerp(color.rgb, MixFog(color.rgb, inputData.fogCoord), _FogAmount);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
