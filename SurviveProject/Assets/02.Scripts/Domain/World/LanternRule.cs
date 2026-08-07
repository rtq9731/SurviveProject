using UnityEngine;
using Survive.Items;

namespace Survive.World
{
    /// <summary>
    /// 랜턴의 규칙. <b>랜턴 수치를 돌리는 자리는 여기 하나다.</b>
    ///
    /// <b>어둠은 위협이 아니라 비용이다</b>(기획서 §9). 안 보이면 못 캐고, 못 짓고,
    /// 느리다. 그 비용이 <b>매 순간</b> 작동하게 하려고 셋을 못 박았다.
    ///
    /// <list type="number">
    /// <item><b>끌 수 있다. 다만 끌 이유가 없게 설계한다.</b> "상시 점등"은
    ///       기계적 잠금이 아니라 설계 의도다(검토회신 2026-08-07 ②).
    ///       <b>끌 수 있어야 "어둠은 비용"이 실제 선택이 된다</b> — 잠그면
    ///       플레이어가 비용을 감수할 방법이 없어 어둠이 상수로 굳는다.
    ///       대신 끄면 못 캐고, 못 짓고, 느리고, 가장자리가 안 보인다.
    ///       비용이 즉시 청구되므로 대부분의 상황에서 켜 두는 것이 최적해다.
    ///       스위치는 <see cref="NextSwitchState"/>가 정하고, 꺼져 있는 동안은
    ///       <see cref="AfterDrain(float,int,float,bool)"/>이 배터리를 멈춘다 —
    ///       <b>아끼는 대신 못 보는 것</b>이 거래의 다른 쪽이기 때문이다.</item>
    /// <item><b>반경은 작다.</b> 켜져 있어도 반경 밖은 캄캄해야 한다. 반경이
    ///       관대하면 어둠은 배터리가 떨어졌을 때만 나타나는 <b>처벌</b>이 되고,
    ///       그러면 "어둠은 비용"이라는 축이 죽는다.</item>
    /// <item><b>반경 확장은 티어 업그레이드로.</b> 다만 <b>넓을수록 초당 더
    ///       먹는다</b> — 그래야 티어가 "무조건 좋은 것"이 아니라 <b>넓게 볼
    ///       것인가 오래 버틸 것인가</b>의 판단이 된다. 순수 상향이면 고를 것이
    ///       없다.</item>
    /// </list>
    ///
    /// <b>수치는 아직 확정이 아니다.</b> 랜턴 반경·오프셋·초당 소모는 기획서 §9의
    /// <b>튜닝 5값</b> 중 셋이고, 최종 값은 사람이 정한다(실행 스펙 §16). 그래서 아래
    /// <b>손잡이 일곱</b>만 돌리면 전부 따라오도록 짜 두었다. 나머지는 전부
    /// 파생값이고, <see cref="Survive.World.LanternController"/>는 직렬화 필드를
    /// 갖지 않고 여기를 그대로 읽는다 — 프리팹에 사본이 있으면 상수를 돌려도
    /// 게임이 안 바뀐다(<see cref="Survive.Building.CampfireFuelRule"/>에서 실제로
    /// 겪은 일이다).
    /// </summary>
    public static class LanternRule
    {
        // ══ 튜닝 손잡이 일곱 ════════════════════════════════════
        // 랜턴 압박을 돌릴 때 손대는 곳은 이 일곱뿐이다.

        /// <summary>
        /// 배터리 최대치. <b>축의 단위</b>다.
        ///
        /// 셀 하나가 이만큼을 채운다(<see cref="BatteryPerCell"/>) — 즉 "가득 = 셀 하나"다.
        /// 이 대응을 깨면 화톳불 추출 레시피(스크랩 5개 → 셀 1개)와 배터리 눈금이
        /// 서로 다른 말을 하게 되고, 플레이어는 스크랩 몇 개어치를 태우고 있는지
        /// 셀 수 없게 된다.
        /// </summary>
        public const float MaxBattery = 100f;

        /// <summary>
        /// 배터리 셀 1개가 채우는 양. 화톳불 추출 레시피가 먹는 스크랩 수와 짝이다.
        /// </summary>
        public const float BatteryPerCell = 100f;

        /// <summary>
        /// 티어 1의 반경(m). <b>"어둠은 비용"이 성립하는 이유가 이 숫자다.</b>
        ///
        /// 예전 값은 26m였다. 그 크기면 갱도 하나가 통째로 들어와서, 켜져 있는 동안
        /// 어둠이라는 것이 화면에서 사라진다. 그러면 비용은 배터리가 다한 순간에만
        /// 청구되는 <b>처벌</b>이 되고, 매 순간 작동하는 <b>비용</b>이 아니게 된다.
        /// 작게 두면 반경 밖이 늘 캄캄하므로, 무엇을 밝힐지가 매 발걸음의 선택이 된다.
        /// </summary>
        public const float Tier1Radius = 8f;

        /// <summary>티어 하나당 늘어나는 반경(m). 스위치에서 뺏은 선택을 돌려주는 폭.</summary>
        public const float RadiusPerTier = 4f;

        /// <summary>
        /// 티어 1의 초당 소모. 상시 점등이므로 이것이 <b>탐사 시간의 환율</b>이다.
        ///
        /// 예전 값 1.6을 그대로 둔다 — 스위치를 없애는 것과 자릿수를 바꾸는 것을
        /// 한 번에 하면, 배터리가 빨리 닳는 것이 무엇 탓인지 가릴 수 없다
        /// (화톳불 연료에서 같은 이유로 45초를 지켰다).
        /// </summary>
        public const float Tier1DrainPerSecond = 1.6f;

        /// <summary>
        /// 티어 하나당 늘어나는 초당 소모. <b>티어를 판단으로 만드는 값이다.</b>
        /// 0으로 두면 상위 티어가 순수 상향이 되어 고를 것이 없어진다.
        /// </summary>
        public const float DrainPerTier = 0.8f;

        /// <summary>
        /// 티어 1에서 광원 지점을 사람보다 <b>앞으로</b> 미는 거리(m).
        /// 기획서 §9 튜닝 5값의 <b>두 번째</b>이고, 곧 <b>등 뒤 사각의 크기</b>다.
        ///
        /// <b>왜 이것이 없으면 게임이 성립하지 않는가.</b> 랜턴이 상시 점등이 된 뒤로
        /// 빛 웅덩이가 사람을 정확히 가운데 놓고 따라다녔다. 그러면 플레이어는
        /// <b>걸어다니는 반경 8m짜리 안전지대</b>이고, 빛을 꺼리는 낫이 닿을 수 있는
        /// 자리가 세계 어디에도 없다. 죽는 길이 배터리 소진 하나로 줄어들고,
        /// 그 순간 <b>조명탄이 존재할 이유도 사라진다</b>(§9).
        ///
        /// 원을 앞으로 밀면 등 뒤가 실제로 어두워지고, 거기서 넷이 따라 나온다.
        /// <list type="number">
        /// <item>뒤를 자주 확인해야 하므로 <b>조작 자체가 방어</b>가 된다</item>
        /// <item>어느 쪽을 보든 반대쪽이 비므로 <b>개체수가 곧 난이도 곡선</b>이 된다</item>
        /// <item>채집·제작은 멈춰서 대상을 봐야 하므로 <b>채집이 위험 행동</b>이 된다</item>
        /// <item>등 뒤에서 맞으면 무엇이 때렸는지 모르므로 <b>방향 신호가 필수</b>가 된다
        ///       (<see cref="Survive.Combat.HurtBearing"/>)</item>
        /// </list>
        ///
        /// <b>수치는 사람의 몫이다.</b> 3m는 출발점일 뿐이고, 여기를 돌리면
        /// <see cref="ForwardReachForTier"/>·<see cref="BackReachForTier"/>가 함께 움직인다.
        /// </summary>
        public const float Tier1ForwardOffset = 3f;

        // ══ 파생값·고정값 ══════════════════════════════════════

        /// <summary>가장 높은 티어. 티어 0은 "랜턴이 없다"이지 티어가 아니다.</summary>
        public const int MaxTier = 3;

        /// <summary>랜턴 아이템의 기본 id. 상위 티어는 <c>lantern_t2</c>처럼 뒤에 붙인다.</summary>
        public const string ItemId = "lantern";

        /// <summary>램프의 밝기. 반경과 달리 압박 곡선에 들어가지 않는 순수 연출값이다.</summary>
        public const float Intensity = 5.5f;

        /// <summary>이 비율 아래로 떨어지면 깜빡여서 남은 배터리를 눈으로 알린다.</summary>
        public const float FlickerThreshold = 0.2f;

        /// <summary>가득 찬 배터리로 티어 1이 버티는 시간(초). 보고·검사가 읽는 창구.</summary>
        public static float FullBatterySecondsAtTier1 => MaxBattery / Tier1DrainPerSecond;

        /// <summary>
        /// 등 뒤로 이만큼 넘어가면 어둡다(m). <b>티어가 올라가도 이 값은 그대로다.</b>
        ///
        /// 오프셋을 티어와 무관하게 고정하면 반경이 늘 때마다 등 뒤도 함께 밝아져,
        /// 상위 티어가 <b>등 뒤를 돈으로 사는 물건</b>이 된다. 이 파일이 반경 티어에
        /// 초당 소모를 붙여 둔 것과 같은 이유로 그렇게 두지 않는다 — 순수 상향이면
        /// 고를 것이 없다. 티어는 <b>앞을 더 멀리 볼 권리</b>만 팔고,
        /// 등 뒤 사각은 마지막 티어까지 같은 크기로 남는다.
        /// </summary>
        public static float BackReach => Tier1Radius - Tier1ForwardOffset;

        // ══ 계산 ════════════════════════════════════════════════

        /// <summary>
        /// 이 티어의 반경(m). 티어 0(랜턴 없음)은 0이고, 상한을 넘겨 세지 않는다.
        /// </summary>
        public static float RadiusForTier(int tier)
        {
            if (tier <= 0) return 0f;
            return Tier1Radius + RadiusPerTier * (Mathf.Min(tier, MaxTier) - 1);
        }

        /// <summary>
        /// 이 티어의 초당 소모. 티어 0은 켜질 것이 없으므로 0이다 —
        /// 랜턴을 만들기 전에 배터리가 닳으면 압박이 아니라 버그다.
        /// </summary>
        public static float DrainForTier(int tier)
        {
            if (tier <= 0) return 0f;
            return Tier1DrainPerSecond + DrainPerTier * (Mathf.Min(tier, MaxTier) - 1);
        }

        // ══ 앞으로 민 원 ════════════════════════════════════════
        // 여기부터가 등 뒤 사각의 전부다. 재는 일(사람이 어디를 보고 있는가)은 몸이
        // 하고, 잰 값으로 "이 자리가 밝은가"를 판정하는 일은 전부 여기에 있다 —
        // 씬을 띄우지 않고 경계값을 전수로 확인할 수 있어야 하기 때문이다.

        /// <summary>
        /// 이 티어에서 광원 지점을 앞으로 미는 거리(m).
        ///
        /// <b>반경에서 등 뒤 몫을 뺀 나머지가 전부 앞으로 간다.</b> 그래서 티어를
        /// 올리면 앞만 길어지고 등 뒤는 <see cref="BackReach"/>에 머문다.
        /// 반경이 등 뒤 몫보다 작으면 밀 것이 없으므로 0이다 — 그때는 예전처럼
        /// 사람을 가운데 둔 원이고, 사각도 없다.
        /// </summary>
        public static float OffsetForTier(int tier)
        {
            if (tier <= 0) return 0f;
            return Mathf.Max(0f, RadiusForTier(tier) - BackReach);
        }

        /// <summary>정면으로 빛이 닿는 거리(m). 반경 + 오프셋. 보고·검사가 읽는 창구.</summary>
        public static float ForwardReachForTier(int tier)
        {
            if (tier <= 0) return 0f;
            return RadiusForTier(tier) + OffsetForTier(tier);
        }

        /// <summary>
        /// 등 뒤로 빛이 닿는 거리(m). <b>이 거리 너머가 사각이다.</b>
        /// 밀 것이 없을 만큼 반경이 작으면 반경 그 자체다.
        /// </summary>
        public static float BackReachForTier(int tier)
        {
            if (tier <= 0) return 0f;
            return Mathf.Min(RadiusForTier(tier), BackReach);
        }

        /// <summary>
        /// 옆으로 빛이 닿는 거리(m). 원을 앞으로 밀면 <b>옆도 함께 줄어든다</b> —
        /// 피타고라스가 정하는 값이라 따로 돌릴 손잡이가 없다. 그래서 몸을 90도만
        /// 틀어도 시야가 좁아지고, "정면을 지킨다"가 실제로 값을 한다.
        /// </summary>
        public static float SideReachForTier(int tier)
        {
            if (tier <= 0) return 0f;
            float r = RadiusForTier(tier);
            float o = OffsetForTier(tier);
            float inside = r * r - o * o;
            return inside <= 0f ? 0f : Mathf.Sqrt(inside);
        }

        /// <summary>
        /// 바라보는 방향의 <b>수평 성분</b>만 남긴 단위벡터. 못 정하면 0이다.
        ///
        /// <b>고개의 위아래(pitch)는 버린다.</b> 이 결정이 게임 감각을 가른다 —
        /// 자세한 근거는 <see cref="LanternController"/>에 적어 두었다. 한 줄로는,
        /// 위아래를 반영하면 <b>발밑을 보는 순간 사각이 사라지고</b> 하늘을 보는
        /// 순간 제 발밑이 어두워진다. 채집을 위험 행동으로 만들려는 설계가
        /// 정확히 거꾸로 뒤집힌다.
        /// </summary>
        public static Vector3 Facing(Vector3 look)
        {
            look.y = 0f;
            float m = look.magnitude;
            // 정확히 위나 아래를 보고 있으면 앞이 없다. 그럴 때 아무 방향이나
            // 고르면 사각이 순간이동하므로, 미는 것을 그만두고 원을 제자리에 둔다.
            return m < 0.0001f ? Vector3.zero : look / m;
        }

        /// <summary>빛 웅덩이의 중심. 사람이 선 자리에서 바라보는 쪽으로 오프셋만큼 간 곳.</summary>
        public static Vector3 LitCenter(Vector3 anchor, Vector3 look, float offset)
        {
            if (offset <= 0f) return anchor;
            var f = Facing(look);
            return f == Vector3.zero ? anchor : anchor + f * offset;
        }

        /// <summary>
        /// 이 자리가 그 랜턴의 빛 웅덩이 안인가. <b>판정은 구(球)다</b> —
        /// 중심만 앞으로 갔을 뿐 모양은 예전 그대로이므로, 오프셋 0이면
        /// 글자 그대로 예전 판정이 된다.
        /// </summary>
        public static bool Covers(Vector3 anchor, Vector3 look, float radius, float offset,
                                  Vector3 point)
        {
            if (radius <= 0f) return false;
            return (point - LitCenter(anchor, look, offset)).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// 이 자리가 사람의 <b>등 뒤</b>인가. 수평으로만 본다 —
        /// 머리 위와 발밑은 앞뒤가 없다.
        /// </summary>
        public static bool IsBehind(Vector3 anchor, Vector3 look, Vector3 point)
        {
            var f = Facing(look);
            if (f == Vector3.zero) return false;

            var d = point - anchor;
            d.y = 0f;
            return Vector3.Dot(d, f) < 0f;
        }

        /// <summary>
        /// 등 뒤 사각의 <b>크기</b>(m). 곧 오프셋이다.
        ///
        /// 원을 앞으로 o만큼 밀면 등 뒤로 닿는 거리가 r에서 r−o로 줄어든다.
        /// 그 사이의 o미터가 <b>예전에는 지켜 주던 자리인데 이제 내준 자리</b>이고,
        /// 그것이 사각의 크기다. 그래서 <b>오프셋 0이면 사각도 0</b>이고
        /// 오프셋이 커지면 그만큼 단조로 커진다 — 기획서 §9가 "랜턴 오프셋
        /// (= 등 뒤 사각의 크기)"라고 한 등호가 여기서 글자 그대로 성립한다.
        /// </summary>
        public static float BlindSpotDepth(float radius, float offset)
        {
            if (radius <= 0f) return 0f;
            return Mathf.Clamp(offset, 0f, radius);
        }

        /// <summary>이 티어가 내주는 등 뒤 사각의 크기(m).</summary>
        public static float BlindSpotDepthForTier(int tier) =>
            BlindSpotDepth(RadiusForTier(tier), OffsetForTier(tier));

        // ══ 어둠에서 때릴 수 있는 자리 ══════════════════════════
        // 사각을 <b>깊이</b>로만 세면 "낫이 거기서 사람에게 닿는가"에 답하지 못한다.
        // 기획서 §9가 오프셋과 낫 공격 거리를 <b>같이 보라</b>고 한 것이 그 뜻이고,
        // 여기 셋이 그 물음에 답한다 — 넓이·각도·여유. 셋 다 <b>수평 평면</b>에서
        // 잰다. 사람도 낫도 바닥을 딛고 있고, 사각은 발치에서 갈리기 때문이다.
        //
        // <b>사각을 정하는 것은 오프셋이 아니라 「등 뒤 도달 거리」다.</b> 사람 뒤로
        // 가장 가까운 어두운 자리는 반경 − 오프셋 미터 뒤에 있고, 낫이 거기 서서
        // 사람에게 닿으려면 <b>공격 거리가 그만큼은 돼야 한다.</b> 오프셋을 그대로
        // 공격 거리와 견주면(3 대 2.2) 여유가 0.8m로 보이지만, 실제로 견줄 것은
        // 5m 대 2.2m다 — 부호가 뒤집힌다.

        /// <summary>
        /// 낫이 <b>어둠 속에 선 채로</b> 사람에게 닿을 수 있는 여유(m).
        /// 공격 거리에서 등 뒤 도달 거리를 뺀 값이다.
        ///
        /// <b>0 이하이면 그런 자리가 없다.</b> 낫은 여전히 등 뒤로 파고들 수 있지만
        /// (<see cref="LitZoneRegistry.IsBlindSide"/>는 반경이 아니라 <b>반평면</b>이다)
        /// 때리는 순간의 제 몸은 빛 안이다.
        /// </summary>
        public static float DarkStrikeMargin(float radius, float offset, float reach)
            => reach - DarkBehindDistance(radius, offset);

        /// <summary>사람 뒤로 가장 가까운 <b>어두운</b> 자리까지의 거리(m).</summary>
        public static float DarkBehindDistance(float radius, float offset)
            => Mathf.Max(0f, radius - Mathf.Max(0f, offset));

        /// <summary>어둠에서 때릴 수 있는 자리가 하나라도 있는가.</summary>
        public static bool HasDarkStrikeWindow(float radius, float offset, float reach)
            => reach > 0f && DarkStrikeMargin(radius, offset, reach) > 0f;

        /// <summary>
        /// 이 반경에서 사각이 열리기 시작하는 <b>오프셋</b>(m).
        /// 이보다 크게 밀어야 낫이 어둠에 선 채로 닿는다.
        /// </summary>
        public static float OffsetThatOpensDark(float radius, float reach)
            => Mathf.Max(0f, radius - Mathf.Max(0f, reach));

        /// <summary>
        /// 이 반경·오프셋에서 사각이 열리기 시작하는 <b>공격 거리</b>(m).
        /// 곧 <see cref="DarkBehindDistance"/>와 같은 값이다 — 두 손잡이가
        /// 같은 문턱의 양쪽이라는 것이 이 등호로 드러난다.
        /// </summary>
        public static float ReachThatOpensDark(float radius, float offset)
            => DarkBehindDistance(radius, offset);

        /// <summary>
        /// 사각의 <b>넓이</b>(m²) — 빛 밖이면서 낫 사거리 안인 바닥의 크기.
        /// 사거리 원에서 빛 원과 겹치는 몫을 뺀 것이다.
        /// </summary>
        public static float DarkStrikeArea(float radius, float offset, float reach)
        {
            if (reach <= 0f) return 0f;
            float R = reach;
            float r = Mathf.Max(0f, radius);
            float d = Mathf.Max(0f, offset);
            return Mathf.PI * R * R - LensArea(R, r, d);
        }

        /// <summary>
        /// 사각이 사람을 둘러싸는 <b>각도</b>(도). 등 뒤 한복판을 가운데 둔 부채꼴이다.
        /// 0이면 어느 방향으로도 어둠에 선 채로 닿을 수 없고, 360이면 사방이 그렇다.
        /// </summary>
        public static float DarkStrikeAngle(float radius, float offset, float reach)
        {
            if (reach <= 0f) return 0f;
            float R = reach;
            float r = Mathf.Max(0f, radius);
            float o = Mathf.Max(0f, offset);

            // 오프셋이 0이면 원이 사람을 가운데 두므로 방향이 없다 — 사방이 같다.
            if (o < 1e-6f) return R > r ? 360f : 0f;

            float k = (R * R + o * o - r * r) / (2f * R * o);
            if (k >= 1f) return 360f;
            if (k <= -1f) return 0f;
            return 2f * (180f - Mathf.Acos(k) * Mathf.Rad2Deg);
        }

        /// <summary>두 원이 겹치는 넓이. 중심 사이가 <paramref name="d"/>다.</summary>
        static float LensArea(float ra, float rb, float d)
        {
            if (ra <= 0f || rb <= 0f) return 0f;
            if (d >= ra + rb) return 0f;

            float small = Mathf.Min(ra, rb);
            if (d <= Mathf.Abs(ra - rb)) return Mathf.PI * small * small;

            float a2 = ra * ra, b2 = rb * rb, d2 = d * d;
            float ca = Mathf.Clamp((d2 + a2 - b2) / (2f * d * ra), -1f, 1f);
            float cb = Mathf.Clamp((d2 + b2 - a2) / (2f * d * rb), -1f, 1f);
            float alpha = Mathf.Acos(ca);
            float beta = Mathf.Acos(cb);
            return a2 * (alpha - Mathf.Sin(2f * alpha) * 0.5f) +
                   b2 * (beta - Mathf.Sin(2f * beta) * 0.5f);
        }

        /// <summary>
        /// 이 자리가 <b>등 뒤 사각</b>인가 — 뒤이면서 빛이 닿지 않는다.
        ///
        /// <b>밀린 것이 없으면 사각도 없다.</b> 오프셋 0이면 원이 사람을 가운데 두므로
        /// 어느 방향이든 대칭이고, 그때 "등 뒤가 어둡다"는 것은 그냥 <b>멀다</b>는 뜻이지
        /// 사각이 아니다. 이 가지가 없으면 오프셋을 0으로 되돌려도 예전 동작으로
        /// 돌아가지 않고, 빛을 꺼리는 개체가 뒤에서는 언제나 자유로워진다.
        ///
        /// 낫이 읽을 값이다. 세계 전체를 보는 창구는
        /// <see cref="LitZoneRegistry.IsBlindSpot"/>이고(화톳불이 뒤를 채우면
        /// 사각이 사라진다), 이쪽은 랜턴 하나만 놓고 보는 순수 판정이다.
        /// </summary>
        public static bool IsBlindSpot(Vector3 anchor, Vector3 look, float radius, float offset,
                                       Vector3 point) =>
            BlindSpotDepth(radius, offset) > 0f &&
            IsBehind(anchor, look, point) &&
            !Covers(anchor, look, radius, offset, point);

        // ══ 스위치 ══════════════════════════════════════════════
        // 끌 수 있어야 "어둠은 비용"이 실제 선택이 된다(검토회신 ②). 스위치의
        // 상태는 몸이 들고 있지만(LanternController), 그 상태가 무엇을 뜻하는지는
        // 전부 여기 있다 — 씬을 띄우지 않고 경계값을 확인할 수 있어야 한다.

        /// <summary>
        /// 스위치의 처음 자리. <b>켜져 있다.</b> 랜턴을 손에 넣는 순간
        /// 이유 없이 어두우면 플레이어는 물건이 고장 났다고 읽는다.
        /// </summary>
        public const bool DefaultSwitchedOn = true;

        /// <summary>
        /// 불을 켤 <b>재료</b>가 있는가 — 랜턴을 가졌고 배터리가 남았다.
        /// 스위치는 보지 않는다. 배터리 눈금이 이것으로 뜬다.
        /// </summary>
        public static bool CanLight(int tier, float battery) => tier > 0 && battery > 0f;

        /// <summary>
        /// 지금 불이 들어와 있는가. <b>재료와 스위치가 둘 다 있어야 한다.</b>
        /// 불이 없는 상태에 이르는 길이 둘이라는 것이 예전과 갈리는 자리다 —
        /// 다 태워서(비용을 다 낸 결과)와 꺼서(비용을 안 내기로 한 선택).
        /// </summary>
        public static bool IsLit(int tier, float battery, bool switchedOn) =>
            switchedOn && CanLight(tier, battery);

        /// <summary>
        /// F를 눌렀을 때 스위치가 갈 자리.
        ///
        /// <b>랜턴이 없으면 누른 적이 없는 것과 같다.</b> 제작 전에 꺼 두면
        /// 랜턴을 손에 넣는 순간 이유 없이 어둡고, 플레이어는 무엇을 눌러야
        /// 하는지 배운 적이 없다.
        /// </summary>
        public static bool NextSwitchState(bool current, int tier) =>
            tier <= 0 ? DefaultSwitchedOn : !current;

        /// <summary>
        /// 지난 시간만큼 닳은 뒤의 배터리. 0 아래로는 내려가지 않는다.
        ///
        /// <b>꺼져 있으면 줄지 않는다.</b> 이것이 거래의 한쪽이다 —
        /// 끄면 아끼는 대신 못 본다. 꺼도 계속 닳으면 끄는 것이 순수한 손해라
        /// 아무도 끄지 않고, 그러면 잠그지 않았을 뿐 잠근 것과 같다.
        /// </summary>
        public static float AfterDrain(float battery, int tier, float deltaSeconds, bool switchedOn)
        {
            if (!switchedOn) return Mathf.Clamp(battery, 0f, MaxBattery);
            return AfterDrain(battery, tier, deltaSeconds);
        }

        /// <summary>켜 둔 채로 지난 시간만큼 닳은 뒤의 배터리.</summary>
        public static float AfterDrain(float battery, int tier, float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return Mathf.Clamp(battery, 0f, MaxBattery);
            float drained = battery - DrainForTier(tier) * deltaSeconds;
            return Mathf.Clamp(drained, 0f, MaxBattery);
        }

        /// <summary>채운 뒤의 배터리. 최대치를 넘지 않는다.</summary>
        public static float AfterRecharge(float battery, float amount)
        {
            if (amount <= 0f) return Mathf.Clamp(battery, 0f, MaxBattery);
            return Mathf.Min(MaxBattery, Mathf.Max(0f, battery) + amount);
        }

        /// <summary>지금 배터리로 이 티어가 버티는 시간(초). 티어 0은 0이다.</summary>
        public static float SecondsOfLight(float battery, int tier)
        {
            float drain = DrainForTier(tier);
            if (drain <= 0f) return 0f;
            return Mathf.Max(0f, battery) / drain;
        }

        /// <summary>
        /// 깜빡일 때가 되었는가. 꺼진 랜턴은 경고하지 않는다 —
        /// 스위치로 껐든 배터리를 다 썼든 마찬가지다. 깜빡임은 빛의 경고라
        /// 빛이 없으면 낼 것이 없다.
        /// </summary>
        public static bool IsWarning(int tier, float battery, bool switchedOn) =>
            IsLit(tier, battery, switchedOn) && battery <= MaxBattery * FlickerThreshold;

        /// <summary>
        /// 이 아이템이 랜턴이면 그 티어, 아니면 0.
        ///
        /// <b>무엇이 랜턴인가를 id로 묻지 않는다.</b> 조명 장비 자리에 걸리는 것이
        /// 랜턴이다(<see cref="EquipmentSlotKind.Light"/>) — 상위 티어를 더할 때
        /// 에셋만 늘고 코드는 그대로여야 한다. 티어는 <see cref="ToolItemSO.tier"/>를
        /// 그대로 읽는다. 채집 티어와 같은 칸을 쓰는 것이 맞는 이유는, 랜턴에게
        /// "몇 번째 물건인가"는 하나뿐이기 때문이다.
        /// </summary>
        public static int TierOf(ItemDataSO item)
        {
            if (item == null || item.equipSlot != EquipmentSlotKind.Light) return 0;
            int tier = item is ToolItemSO tool ? tool.tier : 1;
            return Mathf.Clamp(tier, 1, MaxTier);
        }

        /// <summary>
        /// 이 인벤토리가 켜고 있는 랜턴의 티어. 없으면 0.
        ///
        /// <b>장비 자리에 걸린 것이 먼저다.</b> 자리가 하나뿐이므로 "지금 무엇을
        /// 켜고 있는가"에 답이 하나로 정해진다 — 티어 교체가 판단이 되려면
        /// 가진 것 중 제일 좋은 것이 저절로 켜지면 안 된다.
        ///
        /// 자리가 비었을 때만 소지품 칸을 훑는다. 저장 복원이 칸에 직접 앉히는
        /// 길을 하나 남겨 두었고(<see cref="Inventory.RehomeEquipment"/>),
        /// 그 사이에 불이 꺼지면 불러오기 한 번에 어둠이 된다.
        /// </summary>
        public static int EquippedTier(Inventory inventory)
        {
            if (inventory == null) return 0;

            var equipped = inventory.Equipment?.Get(EquipmentSlotKind.Light);
            int tier = TierOf(equipped);
            if (tier > 0) return tier;

            var slots = inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.IsEmpty) continue;
                tier = Mathf.Max(tier, TierOf(slot.item));
            }
            return tier;
        }
    }
}
