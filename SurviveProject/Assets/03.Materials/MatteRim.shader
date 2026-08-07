// 무광버섯 전용 손작성 URP 셰이더 (검토회신 ⑤).
//
// 왜 커스텀 셰이더가 필요한가:
// URP/Lit에는 프레넬 항을 노출하는 프로퍼티가 없다. 램버트 확산과 GGX 반사만 있고,
// 스무스니스 0.1(무광 밴드)에서 GGX 로브는 완전히 퍼져 실루엣 테두리에 아무것도
// 남기지 않는다. 이 물건에 필요한 것은 정확히 그 반대다 — 면은 배경과 같은 검정으로
// 두고 시선에 스치는 테두리에만 빛을 얹는 것. 이맘맵이나 에미션 맵으로 흉내 내면
// UV에 고정되어 카메라가 돌 때 따라오지 않는다(실루엣은 시점이 정한다).
//
// 왜 Shader Graph가 아니라 손으로 썼는가: LightShaftBeam.shader와 같은 이유다.
// .shadergraph는 노드 GUID가 뒤섞인 JSON이라 diff로 읽히지 않는다.
//
// 값의 주인은 Domain/Art/MatteRimRule.cs다. MatteRimTests가 셋(규칙·셰이더 기본값·
// 머티리얼)이 어긋나지 않는지 지킨다.
Shader "Survive/MatteRim"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.125, 0.115, 0.12, 1)
        // ArtRuleChecker가 이 이름으로 스무스니스 3단을 검사한다. 무광 밴드(0.1)다.
        _Smoothness("Smoothness", Range(0, 1)) = 0.1
        _Metallic("Metallic", Range(0, 1)) = 0.0

        // 광원 4색 중 매크로늄 자홍(#A12EE0). MatteRimRule.RimColor와 같아야 한다.
        _RimColor("Rim Color", Color) = (0.6313726, 0.1803922, 0.8784314, 1)
        _RimPower("Rim Power", Float) = 5.0
        _RimStrength("Rim Strength", Float) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Name "MatteRimForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Smoothness;
                float _Metallic;
                half4 _RimColor;
                float _RimPower;
                float _RimStrength;
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
                float  fogCoord   : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(input.positionWS));

                // 이 자리가 「받고 있는 빛」을 모은다. 확산과 따로 세는 이유는
                // 림이 각도가 아니라 빛의 있고 없음에 달려야 하기 때문이다 —
                // 등을 돌린 면도 랜턴 앞에 서 있으면 테두리가 산다.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 received = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                float3 diffuse = received * saturate(dot(N, mainLight.direction));

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint k = 0u; k < count; ++k)
                {
                    Light extra = GetAdditionalLight(k, input.positionWS);
                    float3 c = extra.color * extra.distanceAttenuation * extra.shadowAttenuation;
                    received += c;
                    diffuse += c * saturate(dot(N, extra.direction));
                }
                #endif

                float3 ambient = SampleSH(N);

                // 빛을 먹는 물건이다. 알베도가 낮은 것이 그 표현이고, 여기에
                // 별도의 감쇠를 더 걸지 않는다 — 두 군데서 어둡게 하면 값이 갈린다.
                half3 lit = _BaseColor.rgb * (diffuse + ambient);

                // 프레넬. 정면(ndotv=1)은 정확히 0이고, 시선에 스치는 끝에서만 1로 간다.
                float ndotv = saturate(dot(N, V));
                float fresnel = pow(1.0 - ndotv, max(_RimPower, 0.0001));

                // 내는 것이 아니라 튕기는 것이므로 받은 빛에 비례한다.
                // 랜턴을 끄면 테두리도 함께 사라진다.
                float bounce = saturate(Luminance(received + ambient));
                half3 rim = _RimColor.rgb * (fresnel * _RimStrength * bounce);

                half3 color = lit + rim;
                color = MixFog(color, input.fogCoord);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // 그림자를 계속 드리운다. 프리팹의 MeshRenderer가 m_CastShadows: 1이므로
        // 이 패스를 빼면 무광버섯만 그림자를 잃어 주변과 어긋난다.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Smoothness;
                float _Metallic;
                half4 _RimColor;
                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Smoothness;
                float _Metallic;
                half4 _RimColor;
                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
