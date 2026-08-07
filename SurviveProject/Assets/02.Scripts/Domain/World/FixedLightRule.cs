using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 씬에 박혀 있는 광원 하나가 <b>밝은 구역</b>이 되는가, 된다면 얼마나 넓은가.
    ///
    /// <b>왜 이 규칙이 필요한가.</b> 시작 지점의 빛기둥은 화면을 환하게 밝히는데
    /// <see cref="LitZoneRegistry"/>에는 등록돼 있지 않았다. 화톳불·랜턴·발광 군락은
    /// 저마다 자기를 등록하는 컴포넌트를 달고 있지만, 그냥 씬에 놓인 Light에는
    /// 그런 주인이 없기 때문이다. 그래서 <b>화면은 밝은데 규칙은 어둡다고 답하는</b>
    /// 자리가 생겼고, 그 위에서 어둠 감각을 재면 잰 값이 통째로 거짓이 된다.
    ///
    /// <b>왜 빛기둥 하나를 집어 고치지 않는가.</b> 이름으로 집으면 다음에 놓는 빛기둥은
    /// 또 빠진다. 대신 <b>"이만큼 센 고정 광원은 밝은 구역이다"</b>라는 규칙 하나를 두고,
    /// 그 규칙이 무엇을 <b>집지 않는지</b>를 상수로 못 박는다.
    ///
    /// <b>무엇을 집지 않는가 — 이쪽이 본문이다.</b> 이 세계에는 <b>밝은 구역이면 안 되는
    /// 발광체</b>가 여럿이다. 낙하물 표식(1.1/3.5), 매크로늄 석영(0.6/2.2), 장식용 발광
    /// 버섯(3.2/5.5)은 전부 <b>눈에 보이라고</b> 놓은 것이지 안전 지대가 아니다.
    /// 그것들이 포식자를 막기 시작하면 플레이어는 아무것도 하지 않고 버섯 옆에 서서
    /// 밤을 넘긴다. 위 셋 중 가장 멀리 뻗는 것이 <see cref="Reach"/> 기준 3.3m이므로
    /// <see cref="MinLitRadius"/>를 8m로 두면 <b>2.4배의 여유</b>로 갈린다.
    /// </summary>
    public static class FixedLightRule
    {
        /// <summary>
        /// 구역의 가장자리에서 이만큼은 남아 있어야 한다.
        /// 역제곱으로 떨어지는 세기의 하한이고, 곧 "여기까지는 밝다고 부른다"는 선이다.
        /// </summary>
        public const float MinEdgeIlluminance = 0.5f;

        /// <summary>
        /// 이보다 작은 웅덩이는 <b>구역이 아니라 장식</b>이다.
        /// 발광 버섯 군락(반경 11m)이 이 세계에서 가장 작은 「거점」이므로
        /// 그보다 한 뼘 아래에 선을 그었다.
        /// </summary>
        public const float MinLitRadius = 8f;

        /// <summary>
        /// 이 세기의 광원이 <see cref="MinEdgeIlluminance"/>를 지킨 채 뻗는 거리.
        /// 역제곱의 역함수다 — 세기를 네 배로 올려야 반경이 두 배가 된다.
        /// </summary>
        public static float Reach(float intensity) =>
            intensity <= 0f ? 0f : Mathf.Sqrt(intensity / MinEdgeIlluminance);

        /// <summary>
        /// 이 광원이 밝은 구역을 낼 만큼 센가.
        /// <b>세기와 사거리를 둘 다 본다</b> — 세기만 보면 사거리를 1m로 줄여 둔
        /// 연출용 광원이 통과하고, 사거리만 보면 거의 꺼진 광원이 통과한다.
        /// </summary>
        public static bool IsZoneWorthy(float intensity, float range) =>
            range >= MinLitRadius && Reach(intensity) >= MinLitRadius;

        /// <summary>
        /// 이 광원을 밝은 구역으로 세워야 하는가 — <b>거르는 네 층 전부</b>.
        ///
        /// 판정을 여기 둔 이유는 씬 없이 전수로 확인할 수 있어야 하기 때문이다.
        /// 광원을 찾아다니고 컴포넌트를 붙이는 일은 몸(<c>FixedLightZoneService</c>)이 한다.
        /// </summary>
        /// <param name="type">Directional은 자리가 없는 전역광이라 구역이 될 수 없다.</param>
        /// <param name="on">지금 빛을 내고 있는가(컴포넌트와 오브젝트가 둘 다 살아 있는가).</param>
        /// <param name="hasOwner">
        /// 조상 어딘가에 이미 밝은 구역을 내는 것이 있는가. 화톳불·랜턴·발광 군락은
        /// 자기 연료와 전원을 알고 있으므로 여기서 덮으면 꺼진 불이 계속 밝은 구역으로 남는다.
        /// </param>
        public static bool ShouldRegister(LightType type, bool on, bool hasOwner,
                                          float intensity, float range)
        {
            if (!on) return false;
            if (type == LightType.Directional) return false;
            if (hasOwner) return false;
            return IsZoneWorthy(intensity, range);
        }

        /// <summary>
        /// 스폿이 <paramref name="distance"/>만큼 떨어진 면에 그리는 원의 반경.
        ///
        /// <b>스폿을 구로 모델링하면 안 된다.</b> 빛기둥은 천장 구멍(y=92)에 달려 있고
        /// 바닥은 40m 아래다. 광원 자리를 중심으로 도달 거리(49m)짜리 구를 두면
        /// 바닥에서 반경 28m가 밝아지는데, 화면에서 실제로 밝은 원은 9m다.
        /// 규칙이 화면보다 세 배 넓어지면 고치려던 어긋남이 반대 방향으로 되살아난다.
        /// </summary>
        public static float ConeRadius(float distance, float spotAngleDegrees)
        {
            if (distance <= 0f) return 0f;
            float half = Mathf.Clamp(spotAngleDegrees, 0f, 179f) * 0.5f;
            return distance * Mathf.Tan(half * Mathf.Deg2Rad);
        }

        /// <summary>
        /// 바닥에 닿은 스폿의 구역 반경. <b>원뿔 발자국</b>과 <b>세기가 버티는 폭</b> 중
        /// 작은 쪽이다 — 넓게 벌어져도 그만큼 멀면 어둡고, 세도 좁게 조이면 좁다.
        ///
        /// 세기가 버티는 폭은 피타고라스다. 바닥에서 <c>r</c>만큼 옆으로 간 점은
        /// 광원에서 <c>√(d²+r²)</c> 떨어져 있으므로, 그것이 <see cref="Reach"/> 안이려면
        /// <c>r ≤ √(Reach² − d²)</c>다. 빛기둥은 이 값이 28m이고 원뿔이 9m라
        /// <b>원뿔이 정한다</b> — 눈에 보이는 원이 그 9m다.
        /// </summary>
        public static float SpotZoneRadius(float intensity, float distance, float spotAngleDegrees)
        {
            float reach = Reach(intensity);
            if (distance >= reach) return 0f;

            float lit = Mathf.Sqrt(reach * reach - distance * distance);
            return Mathf.Min(ConeRadius(distance, spotAngleDegrees), lit);
        }

        /// <summary>
        /// 앞뒤 없는 점광원의 구역 반경. 사거리와 도달 거리 중 작은 쪽이다.
        /// </summary>
        public static float PointZoneRadius(float intensity, float range) =>
            Mathf.Min(range, Reach(intensity));
    }
}
