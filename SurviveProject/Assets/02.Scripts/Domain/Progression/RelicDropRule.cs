using System.Collections.Generic;
using Survive.Items;

namespace Survive.Progression
{
    /// <summary>
    /// 유물 하나의 자리 — <b>무엇을 떨구고, 그것으로 무엇을 알게 되는가</b>.
    ///
    /// 둘을 한 쌍으로 묶어 두는 이유는 "보유"의 뜻 때문이다. 유물을 손에 쥐고 있는
    /// 것도 보유지만, 이미 <b>다 밝혀낸</b> 것도 보유다 — 연구가 끝나면 유물은
    /// 사라지고 원장에 한 줄이 남는데, 그때 같은 유물을 또 떨구면 쓸 데가 없다.
    /// </summary>
    public readonly struct RelicOption
    {
        /// <summary>떨어지는 아이템의 id.</summary>
        public readonly string ItemId;

        /// <summary>이 유물을 연구하면 원장에 적히는 열쇠. 비면 원장을 보지 않는다.</summary>
        public readonly string ResearchKey;

        public RelicOption(string itemId, string researchKey)
        {
            ItemId = itemId;
            ResearchKey = researchKey;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId);
    }

    /// <summary>
    /// 낫이 순찰 중 무엇을 흘릴지 정하는 규칙 (스펙 §2 "드롭 안전장치").
    ///
    /// <b>미보유 종 우선(pity).</b> 스펙이 이것을 안전장치라고 부르는 이유는
    /// 순수한 확률이 진행을 막을 수 있기 때문이다 — 막을 다섯 번 연달아 주우면
    /// 액면 보행은 진작 열렸는데 돌파는 영영 열리지 않는다. 그래서 아직 없는 것부터
    /// 준다. 없는 것이 여럿이면 그중에서만 고른다.
    ///
    /// <b>전부 가졌으면 아무것도 떨구지 않는다.</b> 유물의 쓸모는 연구 하나뿐이라,
    /// 다 밝혀낸 뒤에도 계속 떨구면 바닥에 쓰레기만 쌓인다. 그 상태를 <see cref="Nothing"/>
    /// 으로 돌려 주고, 부르는 쪽은 그때 굴리기를 멈춘다.
    ///
    /// 난수는 여기서 뽑지 않는다. <paramref name="roll"/>을 받는 형태라야 "무엇을
    /// 먼저 주는가"를 Unity 없이 못 박을 수 있다.
    /// </summary>
    public static class RelicDropRule
    {
        /// <summary>떨굴 것이 없다. 전부 가졌거나 후보가 없다.</summary>
        public const int Nothing = -1;

        /// <summary>
        /// 이미 가진 것인가. 손에 있거나(인벤토리), 이미 다 밝혀냈으면(원장) 가진 것이다.
        /// </summary>
        public static bool AlreadyHeld(RelicOption option, Inventory inventory, UnlockLedger ledger)
        {
            if (!option.IsValid) return true;
            if (inventory != null && inventory.CountOf(option.ItemId) > 0) return true;

            return ledger != null &&
                   !string.IsNullOrWhiteSpace(option.ResearchKey) &&
                   ledger.IsUnlocked(option.ResearchKey);
        }

        /// <summary>
        /// 지금 떨굴 유물의 자리 번호. 미보유가 없으면 <see cref="Nothing"/>.
        /// </summary>
        /// <param name="roll">0 이상 1 미만. 미보유가 여럿일 때 그중 하나를 고른다.</param>
        public static int Pick(IReadOnlyList<RelicOption> options, Inventory inventory,
                               UnlockLedger ledger, float roll)
        {
            if (options == null || options.Count == 0) return Nothing;

            int 미보유 = 0;
            for (int i = 0; i < options.Count; i++)
                if (!AlreadyHeld(options[i], inventory, ledger)) 미보유++;

            if (미보유 == 0) return Nothing;

            int 뽑은자리 = (int)(Clamp01(roll) * 미보유);
            if (뽑은자리 >= 미보유) 뽑은자리 = 미보유 - 1;

            int 지나온것 = 0;
            for (int i = 0; i < options.Count; i++)
            {
                if (AlreadyHeld(options[i], inventory, ledger)) continue;
                if (지나온것 == 뽑은자리) return i;
                지나온것++;
            }

            return Nothing;
        }

        /// <summary>
        /// 더 떨굴 것이 남았는가. 부르는 쪽이 굴리기를 아예 멈출지 정할 때 본다 —
        /// 스팸을 막는 것도 규칙이지 부품의 사정이 아니다.
        /// </summary>
        public static bool AnythingLeft(IReadOnlyList<RelicOption> options, Inventory inventory,
                                        UnlockLedger ledger)
        {
            if (options == null) return false;

            for (int i = 0; i < options.Count; i++)
                if (!AlreadyHeld(options[i], inventory, ledger)) return true;

            return false;
        }

        static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            return v > 0.99999f ? 0.99999f : v;
        }
    }
}
