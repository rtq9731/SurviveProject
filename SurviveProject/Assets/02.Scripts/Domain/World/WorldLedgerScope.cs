using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>
    /// <b>원장이 무엇을 담고 무엇을 안 담는가.</b>
    ///
    /// <b>가르는 잣대는 하나다 — 「씬이 놓아둔 모습과 달라진 것인가」.</b>
    /// 세계는 씬이 놓아둔 것에서 출발하고, 원장은 그 위에 얹힌 <b>차이</b>만 적는다.
    /// 그래서 <b>실행 중에 태어난 것</b>(세운 건축물·떨군 낙하물)은 이번 원장이
    /// 담지 않는다 — 그것들은 「달라진 것」이 아니라 「없던 것」이고, 되살리려면
    /// 무엇으로 다시 만드는지(프리팹 신원·자세)가 필요하다. 그것은 원장이 아니라
    /// <b>생성 목록</b>이라는 다른 물건이다. <b>자리는 열어 둔다</b> —
    /// <see cref="Structure"/>·<see cref="Drop"/>·<see cref="CampfireFuel"/>·
    /// <see cref="Population"/>이 그 자리다.
    ///
    /// <b>안 담는 이유는 여기가 아니라 테스트에 적혀 있다</b>
    /// (<c>WorldLedgerTests.안_담기로_한_것에는_전부_이유가_적혀_있다</c>).
    /// 이 파일은 화면에 나가는 코드로 분류되어 한글 문장을 상수로 들 수 없고
    /// (<c>LocSentenceGateTests</c>), 무엇보다 <b>이유는 테스트가 못 박아야</b>
    /// 갈래를 하나 더 뺄 때 이유 없이 뺄 수 없게 된다. 여기서는 「무엇이
    /// 갈래인가」와 「그중 무엇을 담는가」만 정한다.
    ///
    /// <b>파생값은 어떤 갈래에서도 담지 않는다.</b> 남은 재생 시간, 타는 중의
    /// 불꽃 세기, 부풀어 오르는 중의 크기 같은 것들은 담긴 값에서 다시 계산된다.
    /// 특히 <b>남은 시간</b>을 적으면 불러온 순간부터 다시 세므로 저장해 둔 사이에
    /// 흐른 세계 시간이 통째로 사라진다. 원장은 언제나 <b>절대 시각</b>을 적는다.
    /// </summary>
    public static class WorldLedgerScope
    {
        // ══ 담는 갈래 ═══════════════════════════════════════════

        /// <summary>채집 노드 — 다 캤는가와 그 시각. 재생이 그 시각에서 센다.</summary>
        public const string Harvest = "harvest";

        /// <summary>군락의 갓 한 무더기 — 땄는가와 그 시각. 군락의 밝기가 여기 걸린다.</summary>
        public const string GlowCap = "glowcap";

        /// <summary>자라는 식물 — 지금 몇 단계인가, 시들어 사라졌는가.</summary>
        public const string Plant = "plant";

        // ══ 자리만 열어 둔 갈래 ═════════════════════════════════

        /// <summary>세운 건축물. <b>아직 담지 않는다.</b></summary>
        public const string Structure = "structure";

        /// <summary>바닥에 떨어진 물건. <b>아직 담지 않는다.</b></summary>
        public const string Drop = "drop";

        /// <summary>화톳불의 연료. <b>아직 담지 않는다.</b></summary>
        public const string CampfireFuel = "campfire";

        /// <summary>구역별 개체수. <b>아직 담지 않는다.</b></summary>
        public const string Population = "population";

        // ══ 표 ══════════════════════════════════════════════════

        static readonly string[] _carried = { Harvest, GlowCap, Plant };

        static readonly string[] _excluded = { Structure, Drop, CampfireFuel, Population };

        /// <summary>이 갈래를 원장이 담는가.</summary>
        public static bool Carries(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return false;
            for (int i = 0; i < _carried.Length; i++)
                if (_carried[i] == kind) return true;
            return false;
        }

        /// <summary>
        /// 이 갈래를 <b>일부러</b> 뺐는가. 모르는 갈래는 false다 —
        /// 모르는 것을 「일부러 뺀 것」이라고 말하면 실수를 설계로 둔갑시킨다
        /// (<c>HarvestRespawnRule.NeverOnPurpose</c>와 같은 결).
        /// </summary>
        public static bool ExcludedOnPurpose(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return false;
            for (int i = 0; i < _excluded.Length; i++)
                if (_excluded[i] == kind) return true;
            return false;
        }

        /// <summary>담는 갈래들.</summary>
        public static IReadOnlyList<string> Carried => _carried;

        /// <summary>일부러 뺀 갈래들. <b>테스트가 이 목록마다 이유를 요구한다.</b></summary>
        public static IReadOnlyList<string> Excluded => _excluded;
    }
}
