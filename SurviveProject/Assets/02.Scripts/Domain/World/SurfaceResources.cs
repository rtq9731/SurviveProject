using System;

namespace Survive.World
{
    /// <summary>
    /// 지상에서 나오는 것.
    ///
    /// <b>이 파일은 2026-08-07에 논증을 통째로 다시 세웠다.</b> 옛 논증은 첫 마디를
    /// 「등급 사슬의 첫 칸」에, 마지막 마디를 「군도의 두 섬 사이」에 걸고 있었다.
    /// <b>등급 구분도 군도도 폐기되어 사슬의 양 끝이 함께 사라졌다</b>(세계관 §8).
    /// 낱말만 갈아 끼우면 남는 것은 근거 없는 목록이므로, 살아 있는 사실 위에 다시 세운다.
    ///
    /// <b>새 논증 — 지상에 있는 것은 이 행성이 만든 것이 아니라 들여온 것의 잔해다.</b>
    ///
    /// <list type="number">
    /// <item><b>매크로늄은 지상에 두지 않는다.</b> 그것은 MARSO가 깔아 바다가 된 물질이고
    ///       <b>지하의 것</b>이다(세계관 §8 — "등급은 하나다. 지상에 매크로늄 광석을 두지
    ///       않는다"). 지상에 재료가 없다는 것이 곧 내려가야 하는 이유다(세계관 §7)</item>
    /// <item><b>그런데 셋은 지상에 있다.</b> 스크랩·기계 부품·이종 합금은 이 행성이
    ///       기른 것이 아니라 <b>사람이 들여온 물건의 잔해</b>다. 이종 합금이 개명된 근거가
    ///       바로 그것이다 — 바깥에서 온 것이 아니라 인류가 갖고 도망친 기반기술의
    ///       산물이고, 지구 환경에서 만들어져 성분이 좀 다를 뿐이다(세계관 §8)</item>
    /// <item><b>그리고 이종 합금은 지상에 <i>있어야</i> 한다.</b> 「액면 보행 장비」가
    ///       그것을 요구하고, 그 장비가 넓은 액면을 여는 <b>내려가기 전 단계</b>다.
    ///       지하로 옮기면 <i>내려가려면 내려가서 얻어야 하는</i> 순환이 되어 챕터가 닫힌다</item>
    /// </list>
    ///
    /// <b>사슬을 실제로 밟는 것은 <c>SurfaceResourceTests</c>다.</b> 한 마디라도 끊기면
    /// 그쪽이 빨개진다 — 근거가 사라지면 예외도 함께 무너져야 하기 때문이다.
    /// </summary>
    public static class SurfaceResources
    {
        /// <summary>지상에서 캘 수 있는 것. 이 셋뿐이다.</summary>
        public static readonly string[] Materials = { "scrap", "machine_part", "alien_alloy" };

        /// <summary>
        /// 지상에 있으면 안 되는 물질의 앞머리. <c>macronium</c>·<c>macronium_quartz</c>가
        /// 한 번에 걸린다 — 이름이 늘어도 같은 앞머리를 쓸 것이므로 목록이 낡지 않는다.
        /// </summary>
        public const string BannedPrefix = "macronium";

        // ── 이종 합금이라는 예외 ────────────────────────────────

        /// <summary>
        /// 지상에 남은 <b>유일한 고급 자원</b>. 스크랩·기계 부품은 잔해라 널려 있는 것이
        /// 당연하지만, 이것은 그렇지 않다 — 그래서 <see cref="ExceptionRecipe"/>라는
        /// 근거를 달고 남아 있다.
        /// </summary>
        public const string ExceptionMaterial = "alien_alloy";

        /// <summary>
        /// <b>예외의 근거.</b> 「액면 보행 장비」가 이종 합금을 요구하고,
        /// <b>그것이 곧 넓은 액면을 건너는 장비</b>다. 이종 합금을 지하로 옮기면
        /// <i>내려가려면 내려가서 얻어야 하는</i> 순환이 되어 챕터가 닫힌다.
        ///
        /// <b>근거가 사라지면 예외도 함께 무너져야 한다.</b> 이 레시피가 이종 합금을
        /// 더 이상 쓰지 않게 되는 날, 지상에 이종 합금이 남을 이유도 사라진다 —
        /// <c>SurfaceResourceTests</c>가 그 사슬을 한 마디씩 밟아 확인한다.
        /// </summary>
        public const string ExceptionRecipe = "surface_walker";

        /// <summary>그 레시피가 여는 구역. 사슬의 끝이 여기라서 순환이 성립한다.</summary>
        public const SurfaceZone ExceptionOpens = SurfaceZone.OpenWater;

        /// <summary>
        /// 지상에서 이종 합금이 나오는 자리. 「합금 더미」와 <b>낫</b> 둘뿐이다
        /// (기획서 §2.1·§4.5). 에셋 파일 이름으로 적는다 — id는 옛 이름(<c>ore_vein</c>)이라
        /// 여기 적으면 읽는 사람이 헷갈린다.
        /// </summary>
        public static readonly string[] ExceptionSources = { "OreVein", "Scythe" };

        // ── 조회 ────────────────────────────────────────────────

        /// <summary>지상에 있어도 되는 물질인가.</summary>
        public static bool Allows(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            for (int i = 0; i < Materials.Length; i++)
                if (Materials[i] == itemId) return true;
            return false;
        }

        /// <summary>지상에 있으면 안 되는 물질인가.</summary>
        public static bool IsBanned(string itemId) =>
            !string.IsNullOrEmpty(itemId) &&
            itemId.StartsWith(BannedPrefix, StringComparison.Ordinal);
    }
}
