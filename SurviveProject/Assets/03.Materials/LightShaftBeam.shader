// 빛기둥(LightShaft) 전용 손작성 URP Unlit 셰이더.
//
// 왜 Shader Graph가 아니라 손으로 썼는가: .shadergraph는 노드 GUID·좌표·타입이
// 뒤섞인 JSON이라 스크립트로 조립하기엔 부서지기 쉽고 느리다. 손으로 쓴 .shader는
// 평범한 텍스트라 diff로 보이고, Shader Graph가 만들어냈을 것과 같은 HLSL로
// 컴파일된다 — 런타임 성능 차이가 없다. 잃는 것은 노드 편집 UI뿐인데 아무도 쓰지 않는다.
//
// 왜 커스텀 셰이더가 필요한가(Domain/Art/MaterialRule.cs의 예외 사유와 동일):
// URP/Lit은 거리 안개를 강제로 적용하며 끌 방법이 없다. 화면을 가득 채우는
// 반투명 빛기둥이 안개색(#0C0F15, 거의 검정)으로 당겨져, 랜드마크로 보여야 할
// 먼 거리에서 정확히 사라지는 원인이었다. 이 셰이더는 안개 매크로(MixFog 등)를
// 아예 호출하지 않는다 — 그래서 안개가 적용되지 않는다.
Shader "Survive/LightShaft"
{
    Properties
    {
        _Color("Color", Color) = (0.9098039, 0.8352941, 0.65882355, 1)
        _Intensity("Intensity", Float) = 1.3
        _EdgePower("Edge Power", Float) = 2.0
        _Height("Height", Float) = 40.0
        _GradientPower("Gradient Power", Float) = 1.2
        _DepthFadeDistance("Depth Fade Distance", Float) = 3.0
        // URP 에셋의 Depth Texture가 꺼져 있으면(이 프로젝트의 현재 상태) Scene Depth가
        // 유효하지 않은 값을 읽어 뎁스 차이가 항상 크게 음수로 나오고, saturate가 0으로
        // 뭉개 빛기둥 전체가 사라진다. 프로젝트 전역 렌더러 설정을 강제로 켜는 대신,
        // 이 값을 0으로 두어 안전하게 끌 수 있게 노출한다. Depth Texture를 켜면 1로 올린다.
        _DepthFadeInfluence("Depth Fade Influence", Range(0, 1)) = 0.0
        _Opacity("Opacity", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "LightShaftUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // 3겹 셸 대신 한 장의 콘 — 안팎(위/아래 안쪽)에서 모두 보여야 하므로 Cull Off.
            // 원래 Beam0/1/2 머티리얼과 동일하게 그림자를 드리우지도, 깊이를 쓰지도 않는다
            // (별도 ShadowCaster/DepthOnly 패스를 두지 않은 것이 곧 그 의미다).
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _EdgePower;
                float _Height;
                float _GradientPower;
                float _DepthFadeDistance;
                float _DepthFadeInfluence;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  objectPosY : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // 메시는 로컬(오브젝트) Y가 정점(천장 구멍, 밀도 1)에서 -_Height(바닥, 밀도 0)로
                // 뻗도록 만들었다 — Assets/10.Generated/LightShaftBeam.asset 참조.
                output.objectPosY = input.positionOS.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Fresnel 가장자리 감쇠: 카메라를 정면으로 보는 면(가운데)은 1(안 바램),
                // 시야에 스치는 실루엣 가장자리는 0(바램) — 3겹 셸의 하드 밴딩을 대신한다.
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 normalWS = normalize(input.normalWS);
                float ndotv = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - ndotv, max(_EdgePower, 0.0001));
                float edgeMask = 1.0 - fresnel;

                // 수직 그라디언트: 천장 구멍(오브젝트 Y=0)에서 촘촘, 바닥(오브젝트 Y=-_Height)
                // 에서 saturate로 0까지 자연스럽게 준다. 지수로 대비를 준다(선형은 너무 평평했다).
                float gradient = saturate((input.objectPosY + _Height) / max(_Height, 0.0001));
                gradient = pow(gradient, max(_GradientPower, 0.0001));

                // 소프트 파티클(지형과 만나는 곳의 하드 컷 방지): Scene Depth로 이 픽셀과
                // 그 뒤 불투명 표면 사이 거리를 재고, 가까울수록 알파를 줄인다.
                // Depth Texture가 꺼져 있으면 _DepthFadeInfluence=0으로 이 항을 무시한다
                // (see Properties 주석) — 강제로 프로젝트 렌더러 설정을 켜지 않는다.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float pixelEyeDepth = -TransformWorldToView(input.positionWS).z;
                float depthDiff = sceneEyeDepth - pixelEyeDepth;
                float depthFadeRaw = saturate(depthDiff / max(_DepthFadeDistance, 0.0001));
                float depthFade = lerp(1.0, depthFadeRaw, _DepthFadeInfluence);

                float alpha = saturate(gradient * edgeMask * depthFade * _Opacity);
                half3 color = _Color.rgb * _Intensity;

                // 안개를 의도적으로 적용하지 않는다 — MixFog를 호출하지 않는 것 자체가
                // 그 결정이다. URP/Lit이 강제하는 거리 안개가 빛기둥을 안개색으로
                // 끌어당겨 먼 거리에서 사라지게 만들던 원인이었다.
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
