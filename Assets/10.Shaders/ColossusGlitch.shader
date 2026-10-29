// 거대 존재를 바라볼 때 화면 전체에 입히는 글리치 효과. (전체 화면 패스)
// 세기는 전역 값 _ColossusGlitch(0~1)로 받는다. 0이면 원본 화면을 그대로 돌려준다.
//  - 일렁임: 화면이 물결치듯 흔들린다.
//  - 찢김: 가로 띠 단위로 화면이 어긋난다.
//  - 색 번짐: 빨강·파랑이 좌우로 갈라진다.
//  - 치직거림: 노이즈 알갱이와 주사선, 가끔 번쩍이는 색 반전 띠.
Shader "Custom/ColossusGlitch"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "ColossusGlitch"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ColossusGlitch;

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half3 SampleScreen(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 original = SampleScreen(uv);

                float intensity = _ColossusGlitch;
                if (intensity < 0.001)
                {
                    return half4(original, 1);
                }

                float t = _Time.y;
                float stepTime = floor(t * 12.0);

                // 일렁임: 느린 물결 + 빠른 떨림
                float2 warped = uv;
                warped.x += (sin(uv.y * 16.0 + t * 3.5) * 0.006 + sin(uv.y * 61.0 - t * 11.0) * 0.0025) * intensity;
                warped.y += sin(uv.x * 11.0 + t * 2.7) * 0.005 * intensity;

                // 찢김: 가로 띠마다 어긋난다. 띠의 위치와 크기는 몇 번씩 뚝뚝 바뀐다.
                float bandCount = lerp(14.0, 40.0, Hash11(stepTime));
                float band = floor(uv.y * bandCount);
                float bandRandom = Hash21(float2(band, stepTime));
                if (bandRandom > 1.0 - 0.2 * intensity)
                {
                    warped.x += (Hash21(float2(band, stepTime + 7.0)) - 0.5) * 0.22 * intensity;
                }

                // 가끔 화면 한 줄이 크게 찢긴다.
                float tearTime = floor(t * 5.0);
                float tearY = Hash11(tearTime + 3.0);
                float tear = step(abs(uv.y - tearY), 0.03 * intensity) * step(0.55, Hash11(tearTime));
                warped.x += tear * (Hash11(tearTime + 9.0) - 0.5) * 0.5 * intensity;

                // 색 번짐
                float split = (0.003 + 0.008 * Hash11(stepTime + 1.0)) * intensity;
                half3 color;
                color.r = SampleScreen(warped + float2(split, 0)).r;
                color.g = SampleScreen(warped).g;
                color.b = SampleScreen(warped - float2(split, -split * 0.5)).b;

                // 치직거림: 노이즈 + 주사선
                float2 pixel = uv * _ScreenParams.xy;
                float grain = Hash21(floor(pixel * 0.5) + frac(t) * 311.0) - 0.5;
                color += grain * 0.12 * intensity;
                color *= 1.0 - 0.14 * intensity * (0.5 + 0.5 * sin(pixel.y * 3.14159));

                // 번쩍이는 반전 띠 (강할 때만)
                float flashTime = floor(t * 9.0);
                float flashY = Hash11(flashTime + 21.0);
                float flash = step(abs(uv.y - flashY), 0.018) * step(0.72, Hash11(flashTime)) * step(0.45, intensity);
                color = lerp(color, 1.0 - color, flash);

                // 가장자리가 어둡고 붉게 물든다.
                float2 centered = uv - 0.5;
                float vignette = smoothstep(0.25, 0.85, dot(centered, centered) * 2.2);
                color = lerp(color, color * half3(0.55, 0.12, 0.14), vignette * 0.7 * intensity);

                return half4(lerp(original, saturate(color), saturate(intensity * 1.4)), 1);
            }
            ENDHLSL
        }
    }
}
