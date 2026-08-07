using UnityEngine;
using Survive.Domain.Art;

namespace Survive.World
{
    /// <summary>
    /// 조명탄의 규칙. <b>조명탄 수치를 돌리는 자리는 여기 하나다.</b>
    ///
    /// <b>이 물건은 빛이 방어에서 공격으로 넘어가는 지점이다</b>(기획서 §5.2).
    /// 그전까지 빛은 "안에 있으면 안전한 것"이었고, 여기서부터
    /// <b>"쏘아서 밀어내는 것"</b>이 된다.
    ///
    /// <b>랜턴과 역할이 겹치지 않는다 — 랜턴은 죽지 않게 하고, 조명탄은 쫓아낸다.</b>
    /// 그 갈림을 말로만 두지 않으려고 아래 넷을 규칙으로 적어 두었다.
    /// <list type="number">
    /// <item><b>지속이 유한하다</b>(<see cref="BurnSeconds"/>). 랜턴은 배터리가 있는 한
    ///       계속 켜져 있지만 조명탄은 <b>탄다.</b> 그래서 거점을 대신할 수 없다</item>
    /// <item><b>들고 다니는 빛이 아니다.</b> <see cref="FlareZone"/>은 중심을 생성 때
    ///       못 박고 <c>IOffsetLitSource</c>를 구현하지 않는다 — 사람을 따라오지도 않고
    ///       앞뒤도 없다. 그 두 성질이 <b>등 뒤 사각을 메우는 자격</b>이고
    ///       (<see cref="LitZoneRegistry.IsBlindSide"/>), 붙어 있는 개체를 떼어낼 수 있는
    ///       이유가 정확히 그것이다</item>
    /// <item><b>보려고 쏘면 언제나 손해다</b>(<see cref="ForfeitsMoreThanItBurns"/>).
    ///       한 발이 태우는 배터리가 같은 시간 랜턴을 켜 두는 값보다 크다. 그래서
    ///       "조명탄으로 밝히고 다니기"가 최적해가 되는 판이 없다</item>
    /// <item><b>랜턴보다 넓다</b>(<see cref="OutgrowsEveryLantern"/>). 티어를 올린
    ///       랜턴보다도 넓어야 한다 — 조명탄은 티어 3 물건이라, 그때 이미 가진
    ///       랜턴보다 좁으면 「랜턴보다 범위가 크다」가 정작 쓰는 자리에서 거짓이 된다</item>
    /// </list>
    ///
    /// <b>새 축을 만들지 않았다.</b> 밀어내는 일은 이 파일이 하지 않는다 — 조명탄은
    /// <see cref="LitZoneRegistry"/>에 <b>고정 광원</b>으로 들어갈 뿐이고, 그 뒤는 이미
    /// 서 있는 규칙이 전부 한다. 밝은 구역 안에 선 개체는
    /// <c>CreatureDecision.JudgeLight</c>가 <c>Retreat</c>로 돌리고, 그 판정을 그대로
    /// 받는 <c>ScytheFsm</c>이 <b>Attack에서 Beware로 내린다</b>. 사람이 그 안에
    /// 서 있으면 <c>PlayerNearFixedLight</c>가 참이 되어 따라붙기까지 풀린다.
    /// 「조명탄에 밀린다」는 전이는 표에 이미 적혀 있었고, 이 라운드가 한 것은
    /// <b>그것을 당긴 것</b>뿐이다.
    ///
    /// <b>수치는 확정이 아니다.</b> <see cref="PushDistance"/>는 기획서 §5.2
    /// <b>튜닝 5값의 네 번째</b>이고 최종 값은 사람이 정한다. 아래 손잡이 넷만
    /// 돌리면 나머지가 전부 따라오도록 짜 두었다 — 랜턴이
    /// <see cref="LanternRule"/> 하나로 도는 것과 같은 규율이다.
    /// </summary>
    public static class FlareRule
    {
        // ══ 튜닝 손잡이 넷 ══════════════════════════════════════

        /// <summary>
        /// 밝히는 반경(m). <b>이것이 곧 「조명탄 밀어내기 거리」다</b>
        /// (기획서 §5.2 튜닝 5값의 넷째, <see cref="PushDistance"/>).
        ///
        /// <b>두 이름이 한 값인 것이 이 라운드의 설계 판단이다.</b> 밀어내기를 따로
        /// 두면 축이 하나 더 생기고, 그러면 "빛이 여기까지인데 밀리는 것은 저기까지"가
        /// 되어 플레이어가 화면으로 규칙을 읽을 수 없다. 빛을 꺼리는 개체가
        /// 벗어나야 하는 자리는 <b>빛의 가장자리</b>이므로, 반경이 곧 밀어내는 거리다.
        /// 화면에 보이는 원이 그대로 규칙이 된다.
        ///
        /// <b>20을 고른 근거.</b> 못 박아야 할 밑바닥은 <b>랜턴 최고 티어 반경 16m</b>다
        /// (<see cref="OutgrowsEveryLantern"/>). 그 위에서 세 후보를 쟀다 —
        /// 재는 방법과 결과는 <c>E2EFlare</c>의 실측 절에 있다. 18은 낫의 감지 반경
        /// 14m를 겨우 넘고, 24는 밤 화면의 3분의 1을 밝혀 「어둠을 지킨다」와 부딪힌다.
        /// 20이면 밀려난 낫이 <b>감지 반경 밖</b>(14m)에 서게 되어
        /// "쫓아냈다"가 상태로도 성립한다.
        ///
        /// <b>사람이 정할 값이다.</b> 여기를 돌리면 <see cref="PushDistance"/>와
        /// <see cref="Covers"/>·<see cref="PushTarget"/>이 함께 움직인다.
        /// </summary>
        public const float Radius = 20f;

        /// <summary>
        /// 타는 시간(초). <b>유한한 것이 이 물건의 절반이다.</b>
        ///
        /// 랜턴 한 셀이 62.5초를 버티는데 조명탄은 12초다. 그래서 조명탄으로
        /// 거점을 세울 수 없고, 「그 자리를 비워 놓고 들어간다」는 <b>한 번의 창</b>이 된다.
        /// 길게 잡으면 던져 놓은 불빛이 사실상 이동식 화톳불이 되어
        /// 목재를 태워 거점을 지키는 축(§5.3)과 정면으로 부딪힌다.
        ///
        /// <b>12를 고른 근거.</b> 밀려난 낫이 20m 밖에서 다시 붙는 데 걸린 시간이
        /// 실측 기준이다 — 그 값보다 짧으면 조명탄이 아무것도 사 주지 못하고,
        /// 두 배 넘게 길면 낫이 없는 시간이 통째로 공짜가 된다.
        /// </summary>
        public const float BurnSeconds = 12f;

        /// <summary>
        /// 한 발이 태우는 배터리. <b>랜턴과 같은 통에서 먹는 것이 설계다.</b>
        ///
        /// 그래서 매 순간의 물음이 <b>"빛을 지키는 데 쓸 것인가 쫓아내는 데 쓸
        /// 것인가"</b>가 된다. 자원을 따로 두면 그 물음이 사라지고 조명탄은
        /// 그냥 "모아 두면 쓰는 것"이 된다.
        ///
        /// <b>40을 고른 근거.</b> 한 발이 랜턴 <b>25초</b>다
        /// (<see cref="LanternSecondsForfeited"/>). 실측 기준선으로 본 원정 한 번이
        /// 60~64초이므로, 한 발은 <b>원정의 5분의 2</b>를 태우는 셈이다 — 들고 나가는
        /// 발 수가 곧 그날 얼마나 멀리 갈 수 있는가가 된다. 그리고 12초를 밝히는 데
        /// 25초어치를 내므로 <see cref="ForfeitsMoreThanItBurns"/>가 성립한다.
        /// </summary>
        public const float BatteryCost = 40f;

        /// <summary>
        /// 총구에서 날아가는 최대 거리(m). <b>「총」이므로 날아가서 박힌다.</b>
        ///
        /// 발밑에 놓는 물건이 아니라는 것이 이 값의 존재 이유다 — 놓는 물건이면
        /// 「낫이 있는 자리를 비워 놓고 들어간다」(§5.2)가 성립하지 않는다.
        /// 먼저 비우고 들어가려면 <b>내가 아직 없는 자리</b>를 밝힐 수 있어야 한다.
        ///
        /// 반경(20m)의 두 배로 잡았다. 그래야 최대 사거리로 쏜 조명탄의 원이
        /// 제 발밑에 닿지 않아 <b>"저쪽을 비운다"와 "여기를 지킨다"가 갈린다.</b>
        /// </summary>
        public const float MaxThrowDistance = 40f;

        // ══ 파생값·고정값 ══════════════════════════════════════

        /// <summary>
        /// 기획서 §5.2 튜닝 5값의 <b>넷째 — 「조명탄 밀어내기 거리」</b>.
        /// <see cref="Radius"/>와 같은 값이고, 같은 값인 것이 판단이다(위 설명 참조).
        /// </summary>
        public static float PushDistance => Radius;

        /// <summary>조명탄 총 아이템의 id.</summary>
        public const string ItemId = "flare_gun";

        /// <summary>박힌 자리에서 광원을 띄우는 높이(m). 땅에 파묻히면 빛이 잘린다.</summary>
        public const float GroundClearance = 0.5f;

        /// <summary>램프의 밝기. 반경과 달리 압박 곡선에 들어가지 않는 순수 연출값이다.</summary>
        public const float Intensity = 7f;

        /// <summary>
        /// 조명탄의 색. <b>자홍이다</b> — 매크로늄 석영으로 만들므로 재료의 색이
        /// 그대로 간다(기획서 §7). 새 색을 만들지 않으므로 광원 4색 규칙 안에
        /// 그대로 들어간다.
        /// </summary>
        public static Color Color => ArtPalette.Macronium;

        /// <summary>
        /// 한 발을 쏘느라 <b>포기하는 랜턴 시간</b>(초). 티어 1 기준.
        /// 플레이어가 실제로 견주는 것이 이 값이다 — "이 발을 쏘면 25초 일찍 어두워진다".
        /// </summary>
        public static float LanternSecondsForfeited =>
            LanternRule.Tier1DrainPerSecond <= 0f
                ? 0f
                : BatteryCost / LanternRule.Tier1DrainPerSecond;

        /// <summary>
        /// <b>보려고 쏘면 언제나 손해인가.</b> 한 발이 포기하게 하는 랜턴 시간이
        /// 타는 시간보다 길어야 한다.
        ///
        /// 이것이 뒤집히면 조명탄이 <b>더 싼 랜턴</b>이 되고, 그 순간 두 물건의 역할이
        /// 겹친다 — 쏘아 두고 그 안에서 지내는 것이 최적해가 되기 때문이다.
        /// </summary>
        public static bool ForfeitsMoreThanItBurns => LanternSecondsForfeited > BurnSeconds;

        /// <summary>
        /// <b>어느 티어의 랜턴보다도 넓은가.</b> 조명탄은 티어 3 제작물이라
        /// 랜턴 업그레이드보다 뒤에 온다 — 그때 이미 가진 랜턴보다 좁으면
        /// 「랜턴보다 범위가 크다」가 정작 쓰는 자리에서 거짓이 된다.
        /// </summary>
        public static bool OutgrowsEveryLantern
        {
            get
            {
                for (int tier = 1; tier <= LanternRule.MaxTier; tier++)
                    if (Radius <= LanternRule.RadiusForTier(tier)) return false;
                return true;
            }
        }

        /// <summary>
        /// <b>조명탄이 랜턴을 대신할 수 있는가.</b> 언제나 거짓이어야 한다.
        ///
        /// 대신하려면 셋이 다 되어야 한다 — 배터리가 있는 한 꺼지지 않고(무한),
        /// 따라다니고, 싸야 한다. 여기서는 <b>지속이 유한하다</b> 하나만 봐도 끝난다.
        /// 따라다니지 않는다는 것은 형(<see cref="FlareZone"/>)이 이미 못 박고 있다.
        /// </summary>
        public static bool CanReplaceLantern =>
            BurnSeconds >= LanternRule.FullBatterySecondsAtTier1;

        // ══ 배터리 ══════════════════════════════════════════════

        /// <summary>지금 배터리로 한 발을 쏠 수 있는가. 정확히 값이면 쏜다.</summary>
        public static bool CanFire(float battery) => battery >= BatteryCost;

        /// <summary>한 발 쏜 뒤의 배터리. 0 아래로는 내려가지 않는다.</summary>
        public static float AfterFire(float battery) =>
            Mathf.Clamp(battery - BatteryCost, 0f, LanternRule.MaxBattery);

        /// <summary>가득 찬 배터리로 몇 발을 쏠 수 있는가.</summary>
        public static int ShotsPerFullBattery =>
            BatteryCost <= 0f ? 0 : Mathf.FloorToInt(LanternRule.MaxBattery / BatteryCost);

        // ══ 타는 시계 ═══════════════════════════════════════════

        /// <summary>
        /// 지핀 지 이만큼 지났을 때 아직 타고 있는가.
        /// <b>정확히 <see cref="BurnSeconds"/>면 꺼진 것으로 본다</b> — 다 탄 것이다.
        /// </summary>
        public static bool StillBurning(float sinceIgnited) =>
            sinceIgnited >= 0f && sinceIgnited < BurnSeconds;

        /// <summary>남은 시간(초). 다 탔으면 0이다.</summary>
        public static float BurnLeft(float sinceIgnited) =>
            Mathf.Clamp(BurnSeconds - sinceIgnited, 0f, BurnSeconds);

        // ══ 어디를 밝히는가 ═════════════════════════════════════

        /// <summary>
        /// 이 자리가 그 조명탄의 원 안인가. <b>경계 위는 안이다</b> —
        /// <see cref="LitZoneRegistry.IsLit"/>이 쓰는 것과 같은 부등호라야
        /// 규칙과 화면이 같은 답을 낸다.
        /// </summary>
        public static bool Covers(Vector3 center, Vector3 point, float radius) =>
            radius > 0f && (point - center).sqrMagnitude <= radius * radius;

        /// <summary>기본 반경으로 본 판정.</summary>
        public static bool Covers(Vector3 center, Vector3 point) => Covers(center, point, Radius);

        /// <summary>
        /// 이 자리에 선 개체가 <b>밀려나야 하는 자리</b>. 조명탄 중심에서 지금 자리
        /// 쪽으로 <see cref="PushDistance"/>만큼 간 곳이다.
        ///
        /// <b>규칙이 여기로 순간이동시키지는 않는다.</b> 실제로 옮기는 것은 이미 서 있는
        /// 도주 행동(<c>CreatureNavigation.FleeDestination</c>)이고, 이 함수는
        /// <b>어디까지 가야 빛에서 벗어나는가</b>를 답할 뿐이다 — 실측이 "실제로 몇 미터
        /// 물러났는가"를 이 값과 견준다.
        ///
        /// 중심과 정확히 겹쳐 있으면 방향이 없으므로 제자리를 돌려준다.
        /// 다음 프레임이면 조금이라도 어긋나 방향이 생긴다(도주 규칙과 같은 처리다).
        /// </summary>
        public static Vector3 PushTarget(Vector3 center, Vector3 from, float radius)
        {
            Vector3 away = from - center;
            away.y = 0f;

            float m = away.magnitude;
            if (m < 1e-4f) return from;

            Vector3 pushed = center + away / m * radius;
            pushed.y = from.y;
            return pushed;
        }

        /// <summary>기본 반경으로 본 밀려날 자리.</summary>
        public static Vector3 PushTarget(Vector3 center, Vector3 from) =>
            PushTarget(center, from, PushDistance);

        /// <summary>
        /// <b>붙어 있는 개체까지 삼키는가.</b> 사람 발밑에 터진 조명탄이
        /// 공격 거리 안의 모든 자리를 덮어야, 등에 붙은 개체가 빛 안에 서게 되고
        /// 그제야 물러난다.
        ///
        /// 이것이 거짓이면 조명탄은 <b>이미 붙은 것을 떼어내지 못한다</b> —
        /// 그러면 랜턴과 하는 일이 같아지고(둘 다 다가오는 것만 막는다)
        /// 이 물건이 존재할 이유가 사라진다.
        /// </summary>
        public static bool PeelsOffAttacker(float attackRange) =>
            attackRange >= 0f && attackRange < Radius;

        // ══ 어디에 박히는가 ═════════════════════════════════════

        /// <summary>
        /// 날아간 조명탄이 <b>박히는 자리</b>.
        ///
        /// <b>맞은 자리와 지면을 가른다.</b> 총구에서 겨눈 쪽으로 쏘아
        /// <list type="number">
        /// <item>무언가에 맞으면 <b>맞은 표면</b>에 박힌다. 법선 쪽으로 조금 띄워
        ///       벽이나 바닥에 파묻히지 않게 한다</item>
        /// <item>아무것도 못 맞히면 <b>날아간 끝의 발밑</b>으로 떨어진다. 허공에
        ///       뜬 채로 타면 「그 자리를 밝힌다」가 지면과 어긋나고, 밀려나는
        ///       개체가 원의 아래쪽을 그냥 지나간다</item>
        /// <item>발밑도 못 찾으면 날아간 끝 그대로. 바다 위로 쏘면 이 자리다</item>
        /// </list>
        ///
        /// <b>순수 함수다.</b> 물리 질의는 부르는 쪽이 하고 결과만 넘긴다 —
        /// 씬을 띄우지 않고 세 갈래를 전수로 확인할 수 있어야 한다.
        /// </summary>
        /// <param name="muzzle">총구. 대개 눈높이다.</param>
        /// <param name="aim">겨눈 쪽. 단위벡터가 아니어도 된다.</param>
        /// <param name="hitAhead">앞으로 쏜 선이 무언가에 맞았는가.</param>
        /// <param name="hitPoint">맞은 자리.</param>
        /// <param name="hitNormal">맞은 면의 법선.</param>
        /// <param name="foundGround">못 맞혔을 때, 날아간 끝의 발밑을 찾았는가.</param>
        /// <param name="groundPoint">그 발밑.</param>
        public static Vector3 ImpactPoint(Vector3 muzzle, Vector3 aim,
                                          bool hitAhead, Vector3 hitPoint, Vector3 hitNormal,
                                          bool foundGround, Vector3 groundPoint)
        {
            if (hitAhead)
            {
                Vector3 n = hitNormal.sqrMagnitude < 1e-6f ? Vector3.up : hitNormal.normalized;
                return hitPoint + n * GroundClearance;
            }

            if (foundGround) return groundPoint + Vector3.up * GroundClearance;

            return FarEnd(muzzle, aim);
        }

        /// <summary>아무것도 맞히지 못했을 때 조명탄이 도달하는 끝점.</summary>
        public static Vector3 FarEnd(Vector3 muzzle, Vector3 aim)
        {
            Vector3 d = aim;
            float m = d.magnitude;
            // 겨눈 쪽이 없으면 쏠 수 없다. 총구 자리를 돌려주면 부르는 쪽이
            // 제 발밑에 터뜨린 것과 같은 답을 받는다.
            if (m < 1e-4f) return muzzle;
            return muzzle + d / m * MaxThrowDistance;
        }
    }
}
