// Assets/Shaders/UIShaderDisplacement_Simple.shader
// Phase 0 검증용 간이 변위 셰이더
// 깊이 맵(_DepthTex)의 밝기 값을 기반으로 정점을 법선 방향으로 변위한다.
// 색상 텍스처(_MainTex)를 그대로 출력한다.
//
// Phase 3에서 전체 HLSL 셰이더로 교체:
// - 법선 재계산 (중심 차분)
// - 경계 페더링
// - 그림자 캐스터 패스
// - HDRP Lit 기반 조명 상속
// - 에미션 출력

Shader "UIShader/DisplacedScreen_Simple"
{
    Properties
    {
        _MainTex ("Web Page Color", 2D) = "white" {}
        _DepthTex ("Depth Map", 2D) = "black" {}
        _DisplacementScale ("Displacement Scale", Range(0, 2)) = 0.5
        _DisplacementBias ("Displacement Bias", Range(-1, 1)) = 0
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            // ─── 텍스처 선언 ───
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_DepthTex);
            SAMPLER(sampler_DepthTex);

            // ─── 유니폼 ───
            float _DisplacementScale;
            float _DisplacementBias;
            float _EmissionIntensity;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // 깊이 맵 샘플링 (LOD 0, 정점 셰이더이므로 밉맵 레벨 명시)
                float depth = SAMPLE_TEXTURE2D_LOD(
                    _DepthTex, sampler_DepthTex, input.uv, 0
                ).r;

                // 변위 계산: 깊이값 * 스케일 + 바이어스
                float displacement = depth * _DisplacementScale + _DisplacementBias;

                // 법선 방향으로 정점 변위
                float3 displacedPos = input.positionOS + input.normalOS * displacement;

                output.positionCS = TransformObjectToHClip(displacedPos);
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(displacedPos);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 색상 텍스처 샘플링
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // 에미션으로 출력 (스크린이 자체 발광하는 효과)
                // Phase 3에서 HDRP Lit 기반으로 교체하면 적절한 조명 계산이 적용됨
                return color * _EmissionIntensity;
            }
            ENDHLSL
        }

        // ─── Depth Only 패스 ───
        // HDRP가 깊이 버퍼를 올바르게 채우기 위해 필요
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode" = "DepthForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_DepthTex);
            SAMPLER(sampler_DepthTex);
            float _DisplacementScale;
            float _DisplacementBias;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertDepth(Attributes input)
            {
                Varyings output;

                float depth = SAMPLE_TEXTURE2D_LOD(
                    _DepthTex, sampler_DepthTex, input.uv, 0
                ).r;

                float displacement = depth * _DisplacementScale + _DisplacementBias;
                float3 displacedPos = input.positionOS + input.normalOS * displacement;

                output.positionCS = TransformObjectToHClip(displacedPos);
                return output;
            }

            float4 fragDepth(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
