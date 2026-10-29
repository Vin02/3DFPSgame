// 코스믹 호러 좀비 피부. (Pallor로 핏기 없는 창백한 살갗을 만든다)
// 살갗 군데군데가 찢어져 그 안으로 우주(별과 성운)가 보이고, 가장자리는 이질적인 빛으로 번들거린다.
//  - 우주는 화면 공간에 그려서 몸이 움직여도 "창문 너머의 공간"처럼 고정되어 보인다.
//  - 살갗 부분은 URP 조명(달빛·손전등·섬광탄)을 그대로 받는다.
//  - 그림자·깊이 패스는 URP Lit의 패스를 그대로 쓴다.
Shader "Custom/CosmicHorror"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.42, 0.38, 0.45, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.35
        _FogAmount("Fog Amount", Range(0, 1)) = 1
        _Glow("Self Glow", Range(0, 1)) = 0
        _Pallor("Pallor", Range(0, 1)) = 0

        [HDR] _RimColor("Rim Color", Color) = (0.9, 0.2, 1.6, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.2

        [HDR] _NebulaColorA("Nebula Color A", Color) = (0.1, 0.01, 0.22, 1)
        [HDR] _NebulaColorB("Nebula Color B", Color) = (0.0, 0.1, 0.15, 1)
        [HDR] _StarColor("Star Color", Color) = (5, 5, 6, 1)
        _StarDensity("Star Density", Float) = 45

        _VoidAmount("Void Amount", Range(0, 1)) = 0.58
        _VoidScale("Void Scale", Float) = 3
        _PulseSpeed("Pulse Speed", Float) = 0.7

        // URP Lit의 그림자·깊이 패스가 읽는 값
        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Pallor;
                half _FogAmount;
                half _Glow;
                half4 _RimColor;
                half _RimPower;
                half4 _NebulaColorA;
                half4 _NebulaColorB;
                half4 _StarColor;
                float _StarDensity;
                half _VoidAmount;
                float _VoidScale;
                float _PulseSpeed;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 localPosition : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i);
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);
                return lerp(lerp(nx00, nx10, u.y), lerp(nx01, nx11, u.y), u.z);
            }

            float Fbm3(float3 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                for (int octave = 0; octave < 4; octave++)
                {
                    sum += ValueNoise3(p) * amplitude;
                    p *= 2.03;
                    amplitude *= 0.5;
                }
                return sum;
            }

            // 화면 공간 별 한 층. density가 클수록 촘촘하다.
            float StarLayer(float2 uv, float density, float time)
            {
                float2 cellPosition = uv * density;
                float2 cell = floor(cellPosition);
                float2 local = frac(cellPosition) - 0.5;

                float random = Hash21(cell);
                float2 offset = float2(Hash21(cell + 17.0), Hash21(cell + 41.0)) - 0.5;
                float distanceToStar = length(local - offset * 0.7);

                float isStar = step(0.9, random);
                float twinkle = 0.55 + 0.45 * sin(time * (2.0 + random * 5.0) + random * 40.0);
                return isStar * twinkle * smoothstep(0.12, 0.0, distanceToStar);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.localPosition = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float time = _Time.y;
                float3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // 살갗이 찢어진 자리: 몸에 붙어 천천히 꿈틀거리는 노이즈
                float voidNoise = Fbm3(input.localPosition * _VoidScale + float3(0.0, time * 0.15, time * 0.05));
                half voidMask = smoothstep(_VoidAmount - 0.06, _VoidAmount + 0.06, voidNoise);

                // 찢어진 틈 너머의 우주: 화면 공간에 고정된 성운 + 반짝이는 별 두 층
                float2 spaceUV = screenUV * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float nebula = Fbm3(float3(spaceUV * 2.5, time * 0.03));
                half3 space = lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, smoothstep(0.35, 0.7, nebula)) * (nebula * nebula * 1.6);
                float stars = StarLayer(spaceUV + time * 0.004, _StarDensity, time) + StarLayer(spaceUV * 1.7 - time * 0.002, _StarDensity * 1.6, time) * 0.6;
                space += _StarColor.rgb * stars;

                // 가장자리 빛: 천천히 맥동한다.
                half pulse = 0.65 + 0.35 * sin(time * _PulseSpeed * 6.2831853 + input.localPosition.y * 3.0);
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), _RimPower);
                half3 rim = _RimColor.rgb * fresnel * pulse;

                // 창백함: 원래 피부색을 빼고 밝기(무늬)만 남겨 핏기 없는 살갗으로 만든다.
                half3 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half luminance = dot(texel, half3(0.299, 0.587, 0.114));
                half3 skin = lerp(texel, luminance.xxx * 1.6, _Pallor) * _BaseColor.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirectionWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = skin * (1.0 - voidMask);
                surfaceData.specular = half3(0.15, 0.1, 0.2) * (1.0 - voidMask);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = space * voidMask + rim + skin * (1.0 - voidMask) * _Glow;
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
