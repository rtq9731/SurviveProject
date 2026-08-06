using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>매크로늄 액면에 닿았을 때 치르는 대가.</summary>
    public enum MacroniumContactOutcome
    {
        /// <summary>아무 일도 없다 — 닿지 않았거나, 닿은 것이 매크로늄이 아니다.</summary>
        None,

        /// <summary>액면 보행 장비가 받쳐 준다. 빠지지 않고 그 위를 걷는다.</summary>
        Supported,

        /// <summary>받쳐 줄 것 없이 닿았다. 죽는다.</summary>
        Lethal,

        /// <summary>
        /// 돌파정을 걸치고 닿았고, 가라앉는 쪽을 택했다. 죽지 않고 <b>통과해 내려간다</b>.
        ///
        /// 기획서 §6.2 — "여태 닿으면 죽던 그 층으로 걸어 들어간다.
        /// 바뀌는 것은 플레이어가 무엇을 걸쳤는가뿐이다".
        /// </summary>
        Descending,
    }

    /// <summary>
    /// 액면에 <b>닿았을 때</b> 무슨 일이 벌어지는가.
    ///
    /// <see cref="EnvironmentThreat"/>는 "지날 수 있는가"를 답한다 — 관문 앞에서 묻는 물음이다.
    /// 여기서 답하는 것은 그 답을 무시하고 발을 들여놓았을 때의 결과다. 둘은 같은 판정을
    /// 쓰고 결론만 다르다: 지날 수 있으면 얹혀서 걷고, 지날 수 없으면 죽는다.
    ///
    /// <b>왜 판정을 다시 만들지 않는가.</b> "액면 위를 걷는 수단이 있는가"는 이미
    /// <see cref="EnvironmentThreat.CanPass"/>가 답하는 물음이다. 접촉 전용 규칙을 따로
    /// 두면 관문은 열리는데 밟으면 죽는(또는 그 반대) 상태가 언제든 생길 수 있다.
    ///
    /// 장비는 있는데 용량이 모자란 경우도 죽는 쪽으로 친다 — 판정이 "못 지난다"고 답한
    /// 이상 액면이 몸을 받쳐 주지 못한다는 뜻이고, 반쯤 받쳐 주는 상태는 없다.
    ///
    /// <b>돌파정이 붙으면서 결과가 셋이 됐다</b> (기획서 §6.2). 같은 액면 앞에서 사람은
    /// 셋 중 하나가 된다 — 죽거나, 위를 걷거나, 뚫고 내려가거나. 무엇이 되는지는
    /// 무엇을 걸쳤는가로 갈리고, 둘 다 걸쳤을 때만 <b>어느 쪽을 원하는가</b>가 더해진다.
    ///
    /// <b>왜 둘 다 있을 때 기본이 "걷기"인가.</b> 돌파정을 만들려면 4번 섬의 매크로늄이
    /// 필요하고, 4번 섬에 닿으려면 액면 보행 장비가 필요하다(§5.4의 사슬). 즉 종막에
    /// 이른 사람은 <b>반드시 둘 다</b> 지니고 있다. 여기서 가라앉기를 기본으로 두면
    /// 4번 섬으로 돌아가려고 액면에 발을 얹는 순간 챕터가 끝나 버린다 —
    /// 마지막 한 걸음은 실수로 밟는 것이 아니라 스스로 고르는 것이어야 한다.
    ///
    /// 고르는 수단은 새로 만들지 않는다. 이미 물속에서 가라앉는 데 쓰는 Ctrl
    /// (<c>InputReaderSO.IsDescending</c>, 기획서 §5.6 "Ctrl = 하강")이 그대로 하강이다.
    ///
    /// <b>여기서 묻지 않는 것 — 얼마나 깊은 층인가.</b> 가라앉는 것은 껍데기를 걸쳤는가만
    /// 묻는다. 그 아래 층을 끝까지 뚫어내는가는 층이 묻고, 그 판정은
    /// <see cref="EnvironmentHazard.MacroniumLayer"/> 구간(<c>DescentZone</c>)이
    /// <see cref="EnvironmentThreat"/>에 맡긴다. 그래야 "닿는 순간"과 "다 내려간 순간"이
    /// 각자의 자리에서 판정된다.
    /// </summary>
    public static class MacroniumContact
    {
        /// <summary>
        /// 발이 액면에 얹힌 것으로 치는 여유(m).
        ///
        /// 0으로 두면 표면 위에 정확히 선 상태가 "닿지 않음"으로 떨어진다 —
        /// 액면 보행 장비로 걷는 동안이 바로 그 상태라, 걷고 있는데 아무것도
        /// 닿지 않은 것으로 보고된다. 발바닥 두께만큼의 여유를 준다.
        /// </summary>
        public const float ContactSkin = 0.15f;

        /// <summary>발밑이 액면에 닿았는가. 높이만 본다 — 수평 범위는 구역이 답한다.</summary>
        public static bool Touches(float feetY, float surfaceY) => feetY <= surfaceY + ContactSkin;

        /// <summary>
        /// 돌파정을 걸치고 있는가. 용량은 보지 않는다 —
        /// 얼마나 깊은 층을 뚫는지는 층이 묻는다(클래스 주석 참고).
        /// </summary>
        public static bool HasHull(IReadOnlyList<GearCapability> loadout)
        {
            if (loadout == null) return false;
            for (int i = 0; i < loadout.Count; i++)
                if (loadout[i].Gear == TraversalGear.BreachCraft) return true;
            return false;
        }

        /// <summary>닿은 사실과 지금 갖춘 것으로 결과를 낸다.</summary>
        /// <param name="descentRequested">
        /// 지금 가라앉기를 원하고 있는가(Ctrl). 걸을 수단과 뚫을 수단을 둘 다 갖췄을 때만 갈린다.
        /// </param>
        public static MacroniumContactOutcome Resolve(bool touching, HazardZone zone,
                                                      IReadOnlyList<GearCapability> loadout,
                                                      bool descentRequested = false)
        {
            if (!touching) return MacroniumContactOutcome.None;

            // 다른 위협이 걸린 구역은 닿는다고 해서 대가가 없다. 어둠도 수심도
            // 폭도 "밟으면 죽는" 종류가 아니다 — 이 규칙은 액면 하나에만 붙는다.
            if (zone.Hazard != EnvironmentHazard.MacroniumSurface) return MacroniumContactOutcome.None;

            bool canWalk = EnvironmentThreat.CanPass(zone, loadout);

            // 껍데기가 없으면 규칙은 예전 그대로다 — 받쳐지거나, 죽거나.
            if (!HasHull(loadout))
                return canWalk ? MacroniumContactOutcome.Supported : MacroniumContactOutcome.Lethal;

            // 껍데기를 걸친 사람은 액면에 죽지 않는다. 걸을 수단이 없으면 남는 것은
            // 가라앉는 것뿐이고, 그것은 사고가 아니라 이 물건의 용도다.
            if (!canWalk) return MacroniumContactOutcome.Descending;

            return descentRequested
                ? MacroniumContactOutcome.Descending
                : MacroniumContactOutcome.Supported;
        }

        /// <summary>높이로 닿았는지까지 한 번에 본다.</summary>
        public static MacroniumContactOutcome Resolve(float feetY, float surfaceY, HazardZone zone,
                                                      IReadOnlyList<GearCapability> loadout,
                                                      bool descentRequested = false) =>
            Resolve(Touches(feetY, surfaceY), zone, loadout, descentRequested);
    }
}
