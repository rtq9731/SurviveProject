using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 무광버섯의 <b>윤곽만</b> 읽히게 하는 림라이트 규칙 (검토회신 ⑤).
    ///
    /// <b>무엇이 문제였나.</b> 무광버섯은 빛을 먹는 물건이라 어두운 것이 맞다. 그런데
    /// 화면에서 <b>순수 검정</b>으로 나와 배경과 전혀 갈리지 않았다. 그러면 플레이어가
    /// "저기 뭔가 있다"가 아니라 "화면이 깨졌나"로 읽는다 — 오브젝트가 아니라
    /// 렌더링 사고로 보인다.
    ///
    /// <b>고치는 방향.</b> 면을 밝히는 것이 아니라 <b>실루엣의 가장자리만</b> 스치게 한다.
    /// 정면으로 마주 본 면은 배경과 거의 같은 검정으로 남고, 시선에 스치는 테두리에서만
    /// 빛이 얹힌다. 그것이 프레넬이다 — <see cref="RimAmount"/>.
    ///
    /// <b>왜 「받은 빛」에 곱하는가.</b> 무광버섯은 매크로늄을 흡수해 <b>가둬 두는</b>
    /// 물건이지(상세기획서 §3.3) 빛을 내는 물건이 아니다. 발광으로 만들면 어둠 속에서
    /// 저 혼자 떠 있어 "빛나지 않는다는 사실 자체로 눈에 띈다"는 정체가 뒤집힌다.
    /// 그래서 림은 <b>지금 받고 있는 빛의 세기에 비례</b>한다 — 랜턴을 끄면 림도 함께
    /// 사라진다. 내는 것이 아니라 <b>튕기는 것</b>이라는 뜻이 형태로 남는다.
    ///
    /// <b>왜 밝은 구역이 되지 않는가.</b> 이것은 셰이더 안에서만 일어나는 일이라
    /// <see cref="UnityEngine.Light"/> 컴포넌트가 없고, 자기를 <c>LitZoneRegistry</c>에
    /// 올리는 컴포넌트도 달고 있지 않다. 이 세계에서 밝은 구역은 <b>주인이 직접
    /// 등록해야</b> 생긴다 — 화톳불·랜턴·발광 군락이 그렇게 한다. 걸리면 버섯 옆이
    /// 안전지대가 되어 설계가 뒤집힌다.
    ///
    /// 값은 셰이더(<c>Assets/03.Materials/MatteRim.shader</c>)와 머티리얼이 함께 쓰고,
    /// <c>MatteRimTests</c>가 셋이 어긋나지 않는지 지킨다.
    /// </summary>
    public static class MatteRimRule
    {
        /// <summary>
        /// 림의 색. <b>새 색을 만들지 않는다</b> — 광원 4색 안이어야 한다.
        ///
        /// 매크로늄 자홍을 고른 이유는 이 버섯이 가둔 것이 바로 그것이기 때문이다.
        /// 등불버섯 청록으로 하면 "안전·무료 충전"을 뜻하는 색이 되어 정반대를 말하고,
        /// 불꽃 주황으로 하면 내 랜턴이 남긴 자국처럼 읽혀 사물의 정체가 사라진다.
        /// </summary>
        public static Color RimColor => ArtPalette.Macronium;

        /// <summary>
        /// 테두리가 얼마나 좁은가. 높을수록 실루엣 끝으로 몰린다.
        /// 2면 면 전체가 은은히 뜨고, 7이면 한 픽셀짜리 선이 되어 곡면에서 끊긴다.
        /// 5로 정한 근거는 실측이다 — 4에서는 갓 윗면의 4분의 1이 자홍으로 물들어
        /// "면을 밝히는 것"으로 보였고, 5에서 그 물듦이 테두리로 물러났다.
        /// </summary>
        public const float RimPower = 5f;

        /// <summary>
        /// 가장 스치는 각에서 얹히는 최대 세기. 배경(거의 검정)과 갈릴 만큼은 되고,
        /// 면을 밝히는 것으로 보일 만큼은 아니어야 한다.
        /// </summary>
        public const float RimStrength = 0.55f;

        /// <summary>
        /// 이 각도에서 얹히는 림의 양. <paramref name="ndotv"/>는 법선과 시선의 내적으로,
        /// 1이면 정면(테두리 없음), 0이면 시선에 완전히 스치는 실루엣 끝이다.
        /// </summary>
        public static float RimAmount(float ndotv)
        {
            float f = 1f - Mathf.Clamp01(ndotv);
            return Mathf.Pow(f, RimPower) * RimStrength;
        }

        /// <summary>
        /// 정면에서 얹히는 양. <b>0이어야 한다</b> — 이것이 "면을 밝히지 않는다"의 정의다.
        /// </summary>
        public static float FaceAmount() => RimAmount(1f);

        /// <summary>
        /// 실루엣 끝에서 얹히는 양. 곧 <see cref="RimStrength"/>다.
        /// </summary>
        public static float EdgeAmount() => RimAmount(0f);

        /// <summary>
        /// 법선이 시선에서 45도 틀어진 자리(<c>ndotv≈0.707</c>)에서 남아 있어도 되는 몫.
        /// 최대치 대비 비율이다. 여기서 이미 죽어 있어야 "면이 아니라 테두리"로 읽힌다.
        /// </summary>
        public const float FalloffRatioAt45 = 0.01f;

        /// <summary>림이 <b>면이 아니라 테두리</b>에 머무는가.</summary>
        public static bool IsSilhouetteOnly()
            => FaceAmount() <= 0f
            && EdgeAmount() > 0f
            && RimAmount(Mathf.Cos(45f * Mathf.Deg2Rad)) <= EdgeAmount() * FalloffRatioAt45;
    }
}
