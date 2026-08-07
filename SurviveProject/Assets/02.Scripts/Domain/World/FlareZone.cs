using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 터진 조명탄 하나가 만드는 <b>밝은 구역</b>. 순수 C#이라 씬 없이 시계를 돌려
    /// 경계값을 전수로 확인할 수 있다 — 몸(<c>Survive.World.FlareBurn</c>)은 이것을
    /// 하나 들고 등록하고 <see cref="Tick"/>만 돌린다.
    ///
    /// <b>새 축을 만들지 않으려고 있는 형이다.</b> 조명탄이 낫을 밀어내는 힘은
    /// 여기 없다. 여기서 하는 일은 <see cref="ILitZoneSource"/>가 되는 것뿐이고,
    /// 그 뒤는 전부 이미 서 있는 규칙이 한다 — <see cref="LitZoneRegistry"/>가
    /// 이 원을 <b>고정 광원</b>으로 세고, 그 안에 선 개체를
    /// <c>CreatureDecision.JudgeLight</c>가 물러나게 하고,
    /// <c>ScytheFsm</c>이 <b>Attack에서 Beware로</b> 내린다.
    ///
    /// <b>고정 광원이라는 것이 이 물건의 전부다.</b> <c>IOffsetLitSource</c>를
    /// 구현하지 않는 것이 그 선언이고, 거기서 셋이 한꺼번에 따라 나온다.
    /// <list type="number">
    /// <item><b>등 뒤 사각을 메운다.</b> 앞뒤가 없으므로 누구의 등 뒤라도 내준 쪽이
    ///       아니다(<see cref="LitZoneRegistry.IsBlindSide"/>) — 랜턴이 못 하는 일이
    ///       정확히 이것이고, 그래서 <b>붙어 있는 개체가 떨어진다</b></item>
    /// <item><b>따라붙기를 푼다.</b> 사람이 이 안에 들어가면
    ///       <see cref="LitZoneRegistry.IsLitByFixed"/>가 참이 되어 낫이 순찰로 내려간다</item>
    /// <item><b>들고 다닐 수 없다.</b> 중심이 <c>readonly</c>다 — 옮기는 길이
    ///       아예 없으므로 "조명탄으로 랜턴을 대신한다"가 코드에서 성립하지 않는다</item>
    /// </list>
    /// </summary>
    public sealed class FlareZone : ILitZoneSource
    {
        /// <summary>
        /// 박힌 자리. <b>바뀌지 않는다</b> — 이 한 줄이 "들고 다니는 빛이 아니다"의
        /// 전부다. 옮기는 길을 하나라도 두면 언젠가 사람에게 매달리게 된다.
        /// </summary>
        public readonly Vector3 Center;

        /// <summary>이 조명탄의 반경(m). 기본은 <see cref="FlareRule.Radius"/>다.</summary>
        public readonly float Radius;

        float _since;

        /// <param name="center">박힌 자리.</param>
        /// <param name="radius">
        /// 반경(m). 비워 두면 규칙값을 쓴다. <b>값을 받는 이유는 실측 하나다</b> —
        /// 밀어내기 거리는 아직 사람이 정할 값이라(기획서 §5.2 튜닝 5값의 넷째)
        /// 후보를 여럿 세워 재 봐야 한다. 게임이 쏘는 조명탄은 언제나 규칙값이다.
        /// </param>
        public FlareZone(Vector3 center, float radius = -1f)
        {
            Center = center;
            Radius = radius > 0f ? radius : FlareRule.Radius;
        }

        /// <summary>지핀 뒤 흐른 시간(초).</summary>
        public float Age => _since;

        /// <summary>남은 시간(초). 다 타면 0이다.</summary>
        public float SecondsLeft => FlareRule.BurnLeft(_since);

        /// <summary>시계를 돌린다. 뒤로 가는 시간은 없다.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            _since += deltaSeconds;
        }

        /// <summary>다 태운다. 치우는 쪽이 쓰는 문.</summary>
        public void Snuff() => _since = FlareRule.BurnSeconds;

        // ── ILitZoneSource ───────────────────────────────────────

        public Vector3 LitZoneCenter => Center;
        public float LitZoneRadius => Radius;

        /// <summary>
        /// 아직 타고 있는가. <b>다 타면 그 프레임에 등록에서 빠진 것과 같아진다</b> —
        /// 화톳불이 연료가 떨어지면 false를 내는 것과 같은 규약이다.
        /// </summary>
        public bool IsLit => FlareRule.StillBurning(_since);

        /// <summary>이 자리를 지금 밝히고 있는가. 꺼졌으면 어디든 거짓이다.</summary>
        public bool Covers(Vector3 point) => IsLit && FlareRule.Covers(Center, point, Radius);

        /// <summary>이 자리에 선 것이 밀려나야 하는 자리.</summary>
        public Vector3 PushTargetFor(Vector3 from) => FlareRule.PushTarget(Center, from, Radius);
    }
}
