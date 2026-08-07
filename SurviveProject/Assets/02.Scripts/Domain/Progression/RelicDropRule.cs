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
        /// 지금 유물을 흘릴 수 있는 자리인가 — <b>사람이 곁에 있고, 그 사람이 어둠에 있을 때</b>
        /// (세계관 §10 "칠흑 속에서 랜턴을 꺼야 낫에게 닿는다").
        ///
        /// <b>왜 새 규칙이 필요한가.</b> 「빛에 막힌다」만으로는 저절로 성립하지 않는다.
        /// 목격 반경은 40m이고 감지 반경은 14m라, 그 사이 26m 띠에서는 <b>랜턴을 켠
        /// 채로도</b> 굴림이 돈다 — 사람은 안전한 거리에 서서 유물만 받아 갈 수 있었다.
        /// 오프셋을 6.5m로 올린 뒤로는 더 심해졌다: 정면 도달 14.5m가 감지 반경 14m를
        /// 넘어, <b>마주 보고 서 있기만 하면</b> 낫이 아예 다가오지 못한 채 흘린다.
        /// 겁나는 구석이 한 군데도 없는 진행 조건이 되어 버린다.
        ///
        /// <b>왜 줍는 쪽이 아니라 흘리는 쪽을 막는가.</b> 줍기만 막으면 낮에 밝은 채로
        /// 잔뜩 떨어뜨려 두었다가 나중에 안전한 곳에서 어둠을 만들어 주울 수 있다.
        /// 그러면 무서운 것은 「줍는 순간」뿐이고 <b>낫 곁에 서 있는 시간</b>이 사라진다.
        /// 설계가 노린 두려움은 어둠 속에서 <b>그것과 함께 있는 동안</b>이다.
        ///
        /// <b>이야기로도 맞는다.</b> 낫은 은폐 프로토콜로 도는 유지보수 유닛이고
        /// (세계관 §5) 눈에 띄지 않게 다닌다. 빛 안에 선 사람 곁에서 부품을 흘리는
        /// 것은 그 프로토콜과 어긋난다. <b>보이지 않을 때만 제 일을 한다.</b>
        /// </summary>
        /// <param name="playerWithinWitness">사람이 목격 반경 안인가.</param>
        /// <param name="playerInLight">그 사람이 지금 밝은 구역 안인가. 랜턴이든 화톳불이든 가리지 않는다.</param>
        public static bool CanShed(bool playerWithinWitness, bool playerInLight) =>
            playerWithinWitness && !playerInLight;

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
