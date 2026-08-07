using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>
    /// <b>원장이 무엇을 담고 무엇을 안 담는가.</b>
    ///
    /// <b>가르는 잣대는 하나다 — 「씬이 놓아둔 모습과 달라진 것인가」.</b>
    /// 세계는 씬이 놓아둔 것에서 출발하고, 원장은 그 위에 얹힌 <b>차이</b>만 적는다.
    /// 그래서 <b>실행 중에 태어난 것</b>(세운 건축물·떨군 낙하물)은 원장이
    /// 담지 않는다 — 그것들은 「달라진 것」이 아니라 「없던 것」이고, 되살리려면
    /// 무엇으로 다시 만드는지(프리팹 신원·자세)가 필요하다.
    ///
    /// <b>그래서 갈래는 셋으로 갈린다.</b>
    /// <list type="number">
    /// <item><b>원장이 담는 것</b>(<see cref="Carries"/>) — 씬이 놓은 것의 변화.</item>
    /// <item><b>생성 목록이 담는 것</b>(<see cref="Spawns"/>) — 없던 것의 존재.
    ///   <see cref="SpawnLedger"/>가 그 물건이다. <b>두 물건이지만 창구는
    ///   하나다</b> — 저장본에서 둘은 「세계」 절 하나에 함께 실린다
    ///   (<see cref="WorldLedgerState.spawned"/>).</item>
    /// <item><b>아직 아무도 안 담는 것</b>(<see cref="ExcludedOnPurpose"/>) —
    ///   자리만 열어 둔 갈래.</item>
    /// </list>
    ///
    /// <b>어느 쪽이 담는지도, 안 담는 이유도 여기가 아니라 테스트에 적혀 있다</b>
    /// (<c>WorldLedgerTests</c>의 두 표). 이 파일은 화면에 나가는 코드로 분류되어
    /// 한글 문장을 상수로 들 수 없고 (<c>LocSentenceGateTests</c>), 무엇보다
    /// <b>이유는 테스트가 못 박아야</b> 갈래를 옮기거나 뺄 때 이유 없이
    /// 그럴 수 없게 된다. 여기서는 「무엇이 갈래인가」와 「그중 누가 담는가」만 정한다.
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

        // ══ 생성 목록이 담는 갈래 ═══════════════════════════════

        /// <summary>세운 건축물 — 무엇으로 어디에 세웠는가.</summary>
        public const string Structure = "structure";

        /// <summary>바닥에 떨어진 물건 — 어디에 무엇이 몇 개인가.</summary>
        public const string Drop = "drop";

        /// <summary>
        /// 화톳불의 연료 — 언제 다 타는가, 마지막으로 언제 지폈는가.
        /// <b>제 줄이 없다</b>: 몸을 싣는 <see cref="Structure"/> 줄에 함께 실린다
        /// (<see cref="SpawnLedgerRule.HasRow"/>).
        /// </summary>
        public const string CampfireFuel = "campfire";

        // ══ 자리만 열어 둔 갈래 ═════════════════════════════════

        /// <summary>구역별 개체수. <b>아직 담지 않는다.</b></summary>
        public const string Population = "population";

        // ══ 표 ══════════════════════════════════════════════════

        static readonly string[] _carried = { Harvest, GlowCap, Plant };

        static readonly string[] _spawned = { Structure, Drop, CampfireFuel };

        static readonly string[] _excluded = { Population };

        /// <summary>이 갈래를 원장이 담는가.</summary>
        public static bool Carries(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return false;
            for (int i = 0; i < _carried.Length; i++)
                if (_carried[i] == kind) return true;
            return false;
        }

        /// <summary>
        /// 이 갈래를 <b>생성 목록</b>이 담는가 (<see cref="SpawnLedger"/>).
        ///
        /// 원장이 담는 갈래와 <b>겹치지 않는다</b> — 한 갈래를 둘이 담으면
        /// 불러올 때 어느 쪽이 이기는지가 저장본의 줄 순서에 달리게 된다.
        /// 그 배타성은 테스트가 못 박는다.
        /// </summary>
        public static bool Spawns(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return false;
            for (int i = 0; i < _spawned.Length; i++)
                if (_spawned[i] == kind) return true;
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

        /// <summary>원장이 담는 갈래들.</summary>
        public static IReadOnlyList<string> Carried => _carried;

        /// <summary>생성 목록이 담는 갈래들. <b>테스트가 이 목록마다 이유를 요구한다.</b></summary>
        public static IReadOnlyList<string> Spawned => _spawned;

        /// <summary>일부러 뺀 갈래들. <b>테스트가 이 목록마다 이유를 요구한다.</b></summary>
        public static IReadOnlyList<string> Excluded => _excluded;
    }
}
