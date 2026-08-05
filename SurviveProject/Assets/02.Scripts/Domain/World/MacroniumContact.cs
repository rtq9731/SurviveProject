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

        /// <summary>닿은 사실과 지금 갖춘 것으로 결과를 낸다.</summary>
        public static MacroniumContactOutcome Resolve(bool touching, HazardZone zone,
                                                      IReadOnlyList<GearCapability> loadout)
        {
            if (!touching) return MacroniumContactOutcome.None;

            // 다른 위협이 걸린 구역은 닿는다고 해서 대가가 없다. 어둠도 수심도
            // 폭도 "밟으면 죽는" 종류가 아니다 — 이 규칙은 액면 하나에만 붙는다.
            if (zone.Hazard != EnvironmentHazard.MacroniumSurface) return MacroniumContactOutcome.None;

            return EnvironmentThreat.CanPass(zone, loadout)
                ? MacroniumContactOutcome.Supported
                : MacroniumContactOutcome.Lethal;
        }

        /// <summary>높이로 닿았는지까지 한 번에 본다.</summary>
        public static MacroniumContactOutcome Resolve(float feetY, float surfaceY, HazardZone zone,
                                                      IReadOnlyList<GearCapability> loadout) =>
            Resolve(Touches(feetY, surfaceY), zone, loadout);
    }
}
