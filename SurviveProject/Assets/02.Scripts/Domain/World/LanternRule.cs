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
    /// <item><b>상시 점등이 전제다. 끄는 선택지를 두지 않는다.</b> 예전에는 F로
    ///       켜고 껐다. 그러면 최적해가 "어두운 데서는 꺼 두고 필요할 때만 켠다"가
    ///       되어, 플레이어는 비용을 내는 대신 <b>비용을 피하는 조작</b>을 배운다.
    ///       스위치가 없으면 배터리는 시계가 되고, 시계는 매 순간 돈다.</item>
    /// <item><b>반경은 작다.</b> 켜져 있어도 반경 밖은 캄캄해야 한다. 반경이
    ///       관대하면 어둠은 배터리가 떨어졌을 때만 나타나는 <b>처벌</b>이 되고,
    ///       그러면 "어둠은 비용"이라는 축이 죽는다.</item>
    /// <item><b>반경 확장은 티어 업그레이드로.</b> 스위치에서 뺏은 선택을 여기서
    ///       돌려준다. 다만 <b>넓을수록 초당 더 먹는다</b> — 그래야 티어가
    ///       "무조건 좋은 것"이 아니라 <b>넓게 볼 것인가 오래 버틸 것인가</b>의
    ///       판단이 된다. 순수 상향이면 고를 것이 없고, 고를 것이 없으면
    ///       스위치에서 뺏은 선택을 돌려준 것이 아니다.</item>
    /// </list>
    ///
    /// <b>수치는 아직 확정이 아니다.</b> 랜턴 반경·초당 소모는 기획서 §9의
    /// <b>튜닝 3값</b>이고, 최종 값은 사람이 정한다(실행 스펙 §16). 그래서 아래
    /// <b>손잡이 여섯</b>만 돌리면 전부 따라오도록 짜 두었다. 나머지는 전부
    /// 파생값이고, <see cref="Survive.World.LanternController"/>는 직렬화 필드를
    /// 갖지 않고 여기를 그대로 읽는다 — 프리팹에 사본이 있으면 상수를 돌려도
    /// 게임이 안 바뀐다(<see cref="Survive.Building.CampfireFuelRule"/>에서 실제로
    /// 겪은 일이다).
    /// </summary>
    public static class LanternRule
    {
        // ══ 튜닝 손잡이 여섯 ════════════════════════════════════
        // 랜턴 압박을 돌릴 때 손대는 곳은 이 여섯뿐이다.

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

        /// <summary>
        /// 지금 불이 들어와 있는가. <b>조건은 둘뿐이고 그중 어느 것도 조작이 아니다.</b>
        /// 랜턴을 가졌는가(티어 &gt; 0), 배터리가 남았는가.
        /// </summary>
        public static bool IsLit(int tier, float battery) => tier > 0 && battery > 0f;

        /// <summary>지난 시간만큼 닳은 뒤의 배터리. 0 아래로는 내려가지 않는다.</summary>
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

        /// <summary>깜빡일 때가 되었는가. 꺼진 랜턴은 경고하지 않는다.</summary>
        public static bool IsWarning(int tier, float battery) =>
            IsLit(tier, battery) && battery <= MaxBattery * FlickerThreshold;

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
