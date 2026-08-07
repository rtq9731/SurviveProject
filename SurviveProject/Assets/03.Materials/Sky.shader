// 이 행성의 하늘 — 지평선은 뿌옇게 자홍으로 번지고 머리 위는 별이 쏟아진다
// (세계관 §2 · 상세기획서 §7.4).
//
// 왜 커스텀 셰이더가 필요한가:
// Skybox/Procedural은 지구의 레일리 산란을 제 안에 박아 두고 있어 "무엇이 얼마나
// 두껍게 사이에 있는가"라는 이 세계의 규칙을 받지 못한다. 태양 각도와 대기 두께를
// 제 손으로 계산하므로 DepthFog가 정한 값과 반드시 갈라지고, 갈라지면 지평선의
// 안개색과 지평선의 하늘색이 서로 다른 색이 된다.
//
// 규칙은 여기 없다. Domain/Art/DepthFog.cs 하나가 갖고 있다:
//
//     SkyColor(고도, 햇빛) = HorizonColor(햇빛) × SkyCoverage(고도)
//
// 뒤엣것에 햇빛이 들어가지 않는다는 것이 이 셰이더가 값싼 이유다. 시각과 무관한
// SkyCoverage만 실행 중 한 번 표(_Coverage)로 구워 두면, 매 프레임 바뀌는 것은
// 색 하나(_HorizonColor)뿐이다. 화소당 비용은 텍스처 한 번과 곱하기 하나다.
// 대기 적분식을 여기 HLSL로 옮겨 적으면 같은 규칙이 두 곳에 적히고, 한쪽만
// 고치는 날이 온다(DepthFog 클래스 주석의 같은 경고).
//
// 표는 각도가 아니라 시선 벡터의 y(=sin 고도)로 색인한다 — 화소마다 asin을 불러
// 다시 sin으로 되돌릴 이유가 없다.
//
// 별은 텍스처가 아니라 절차적이다. 별 한 장을 그려 넣으면 그것은 diff로 읽히지
// 않는 바이너리가 되고, 밀도를 바꿀 때마다 다시 그려야 한다. 값의 주인은
// Domain/Art/NightSky.cs이고 SkyDome이 그 값을 여기 꽂는다.
//
// 왜 Shader Graph가 아니라 손으로 썼는가: LightShaftBeam.shader·MatteRim.shader와
// 같은 이유다. .shadergraph는 노드 GUID가 뒤섞인 JSON이라 diff로 읽히지 않는다.
Shader "Survive/Sky"
{
    Properties
    {
        // 시선 고도(sin)별로 대기가 배경을 덮는 정도. SkyDome이 실행 중에 굽는다.
        // 표가 없으면(검정) 하늘이 통째로 검게 나온다 — 자홍 오류색보다 안전한 쪽이다.
        _Coverage("Atmosphere Coverage LUT", 2D) = "black" {}

        // 광원 4색 중 매크로늄 자홍(#A12EE0)에서 나온 색. 실제 값은 매 프레임
        // DepthFog.HorizonColor(햇빛)가 정하고, 여기 적힌 것은 표가 아직 없을 때의 기본값이다.
        _HorizonColor("Horizon Color", Color) = (0.6313726, 0.1803922, 0.8784314, 1)

        // 광원 4색 중 지표광 회백(#E8D5A8). NightSky.StarColor와 같아야 한다.
        _StarColor("Star Color", Color) = (0.9098039, 0.8352941, 0.6588235, 1)

        _StarPeak("Star Peak", Range(0, 1)) = 0.35
        _StarDimmest("Star Dimmest", Range(0, 1)) = 0.22
        _StarCells("Star Cells", Float) = 64
        _StarChance("Star Chance", Range(0, 1)) = 0.055
        _StarRadius("Star Radius", Range(0, 0.5)) = 0.14

        // 낮에는 0. DayNightCycle.Daylight를 받아 NightSky.StarVisibility가 정한다.
        _StarVisibility("Star Visibility", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "SurviveSky"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Coverage);
            SAMPLER(sampler_Coverage);

            CBUFFER_START(UnityPerMaterial)
                float4 _Coverage_ST;
                half4 _HorizonColor;
                half4 _StarColor;
                float _StarPeak;
                float _StarDimmest;
                float _StarCells;
                float _StarChance;
                float _StarRadius;
                float _StarVisibility;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir        : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // 스카이박스 상자는 카메라를 따라다니고 회전하지 않는다.
                // 그래서 정점의 오브젝트 좌표가 곧 바라보는 방향이다.
                output.dir = input.positionOS.xyz;
                return output;
            }

            // Dave Hoskins식 해시. 칸 좌표 하나에서 0~1 둘을 뽑는다.
            float2 Hash22(float2 p)
            {
                float3 q = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.xx + q.yz) * q.zy);
            }

            // 별 하나의 세기(0~1). 큐브 여섯 면 위의 격자로 뿌린다 —
            // 방향 벡터를 가장 큰 축으로 나누면 면 안의 좌표(-1~1)가 나오고,
            // 그 위의 칸마다 별을 하나 둘지 말지 정한다. 면을 쓰는 이유는
            // 칸이 정사각형이라 별이 동그랗게 나오기 때문이다.
            float StarMask(float3 dir)
            {
                float3 a = abs(dir);
                float2 uv;
                float faceSeed;

                if (a.x >= a.y && a.x >= a.z)      { uv = dir.zy / a.x; faceSeed = dir.x > 0 ? 131.0 : 262.0; }
                else if (a.y >= a.z)               { uv = dir.xz / a.y; faceSeed = dir.y > 0 ? 393.0 : 524.0; }
                else                               { uv = dir.xy / a.z; faceSeed = dir.z > 0 ? 655.0 : 786.0; }

                float2 p = uv * _StarCells + faceSeed;
                float2 cell = floor(p);
                float2 f = p - cell;

                float2 h1 = Hash22(cell);            // 있는가 · 얼마나 밝은가
                float2 h2 = Hash22(cell + 71.13);    // 칸 안 어디인가

                // 칸 가장자리에는 두지 않는다. 경계를 넘는 별은 반쪽만 그려진다.
                float2 center = 0.5 + (h2 - 0.5) * 0.6;

                float d = length(f - center) / max(_StarRadius, 1e-4);
                float dot2 = saturate(1.0 - d);
                dot2 *= dot2;                        // 가장자리를 부드럽게

                float present = step(1.0 - _StarChance, h1.x);
                float magnitude = lerp(_StarDimmest, 1.0, h1.y);

                return present * dot2 * magnitude;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dir);

                // 지평선 아래는 지평선과 같다. 어차피 지형이 덮는 자리이고,
                // 덮이지 않은 틈은 안개색과 이어지는 편이 맞다.
                float s = saturate(dir.y);
                float coverage = SAMPLE_TEXTURE2D_LOD(_Coverage, sampler_Coverage, float2(0.5, s), 0).r;

                // 대기가 배경을 덮는 만큼 자홍이고 나머지는 우주의 검정이다.
                half3 sky = _HorizonColor.rgb * coverage;

                // 별도 그 대기 너머에 있다. 지평선 쪽에서 별이 사라지는 것은
                // 낮에 사라지는 것과 다른 원인이다 — 저쪽은 산란이고 이쪽은 두께다.
                half3 stars = _StarColor.rgb *
                              (StarMask(dir) * _StarPeak * _StarVisibility * (1.0 - coverage));

                return half4(sky + stars, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
