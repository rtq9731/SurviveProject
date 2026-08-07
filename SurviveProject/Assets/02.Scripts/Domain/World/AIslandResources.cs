using System;

namespace Survive.World
{
    /// <summary>
    /// A섬에서 나오는 것.
    ///
    /// <b>구역별로 나뉘지 않는다</b>(2026-08-07 회신 ①). 셋은 A섬 전역에 흩어지고,
    /// 구역을 가르는 것은 자원 종류가 아니라 <b>액체의 얼굴</b>이다.
    ///
    /// <b>A섬에 없는 것은 매크로늄이다.</b> A섬은 학습·준비 구간이고, 거기서 배우는 것은
    /// 물질이 아니라 규칙이다 — 액체 세 얼굴 · 어둠은 비용 · 낫은 빛에 막힌다.
    /// 새 물질을 처음 만나는 것은 B섬부터다(기획서 §2.1).
    /// </summary>
    public static class AIslandResources
    {
        /// <summary>A섬에서 캘 수 있는 것. 이 셋뿐이다.</summary>
        public static readonly string[] Materials = { "scrap", "machine_part", "alien_alloy" };

        /// <summary>
        /// A섬에 있으면 안 되는 물질의 앞머리. <c>macronium</c>·<c>macronium_quartz</c>가
        /// 한 번에 걸린다 — 새 등급이 생겨도 같은 앞머리를 쓸 것이므로 목록이 낡지 않는다.
        /// </summary>
        public const string BannedPrefix = "macronium";

        // ── 이종 합금이라는 예외 ────────────────────────────────

        /// <summary>
        /// A섬에 남은 <b>유일한 고급 자원</b>. 스크랩·기계 부품은 난파선의 잔해라
        /// 시작 지점에 있는 것이 당연하지만, 이것은 그렇지 않다 —
        /// 그래서 <see cref="ExceptionRecipe"/>라는 근거를 달고 남아 있다.
        /// </summary>
        public const string ExceptionMaterial = "alien_alloy";

        /// <summary>
        /// <b>예외의 근거.</b> 「액면 보행 장비」가 이종 합금을 요구하고,
        /// <b>그것이 곧 B섬으로 건너가는 장비</b>다. 이종 합금을 B섬으로 옮기면
        /// <i>B섬에 가려면 B섬의 자원이 필요한</i> 순환이 되어 챕터가 닫힌다.
        ///
        /// <b>근거가 사라지면 예외도 함께 무너져야 한다.</b> 이 레시피가 이종 합금을
        /// 더 이상 쓰지 않게 되는 날, A섬에 이종 합금이 남을 이유도 사라진다 —
        /// <c>AIslandResourceTests</c>가 그 사슬을 한 마디씩 밟아 확인한다.
        /// </summary>
        public const string ExceptionRecipe = "surface_walker";

        /// <summary>그 레시피가 여는 구역. 사슬의 끝이 여기라서 순환이 성립한다.</summary>
        public const IslandZone ExceptionOpens = IslandZone.Sea;

        /// <summary>
        /// A섬에서 이종 합금이 나오는 자리. 「합금 더미」와 <b>낫</b> 둘뿐이다
        /// (기획서 §2.1). 에셋 파일 이름으로 적는다 — id는 옛 이름(<c>ore_vein</c>)이라
        /// 여기 적으면 읽는 사람이 헷갈린다.
        /// </summary>
        public static readonly string[] ExceptionSources = { "OreVein", "Scythe" };

        // ── 조회 ────────────────────────────────────────────────

        /// <summary>A섬에 있어도 되는 물질인가.</summary>
        public static bool Allows(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            for (int i = 0; i < Materials.Length; i++)
                if (Materials[i] == itemId) return true;
            return false;
        }

        /// <summary>A섬에 있으면 안 되는 물질인가.</summary>
        public static bool IsBanned(string itemId) =>
            !string.IsNullOrEmpty(itemId) &&
            itemId.StartsWith(BannedPrefix, StringComparison.Ordinal);
    }
}
