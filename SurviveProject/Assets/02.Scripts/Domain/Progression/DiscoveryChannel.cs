namespace Survive.Progression
{
    /// <summary>
    /// 발견이 실제로 <b>일어나는</b> 자리. 계기가 무엇이든 여기로 모인다.
    ///
    /// <b>왜 갈라 놓았는가.</b> 지금까지 발견의 계기는 "재료를 처음 주웠다" 하나였고
    /// (<see cref="FieldDiscovery"/>), 그래서 그 판정과 그 뒤에 일어나는 일이 한 함수에
    /// 붙어 있었다. 챕터 1 스펙 §8-2가 계기를 하나 더 요구한다 —
    /// <b>어떤 자리에 닿았다</b>(<see cref="LocationDiscovery"/>). 그때 뒤에 일어나는 일은
    /// 똑같다: 원장에 열쇠를 적고, 청사진을 열고, AI가 한 줄 말한다.
    ///
    /// 그 "뒤에 일어나는 일"을 두 벌 적으면 언젠가 한쪽만 고쳐진다. 그래서 계기만
    /// 둘이고 몸통은 하나다 — 새 시스템이 아니라 계기가 하나 는 것뿐이라는 스펙의
    /// 말이 코드에서도 참이 된다.
    /// </summary>
    public static class DiscoveryChannel
    {
        /// <summary>
        /// 발견 기록에 쓰는 열쇠. 에셋에 id가 비어 있어도 겹치지 않게 만든다.
        ///
        /// 계기가 둘이므로 대체 열쇠도 둘이다. 장소 쪽에 <c>@</c>를 붙이는 것은
        /// 아이템 id와 장소 id가 우연히 같아도 서로를 덮지 않게 하려는 것이다.
        /// </summary>
        public static string KeyOf(DiscoverySO discovery)
        {
            if (discovery == null) return null;
            if (!string.IsNullOrWhiteSpace(discovery.id)) return discovery.id;
            if (discovery.item != null) return "discovery:" + discovery.item.id;
            if (!string.IsNullOrWhiteSpace(discovery.locationId)) return "discovery:@" + discovery.locationId;
            return null;
        }

        /// <summary>
        /// 이 발견을 겪는다.
        /// </summary>
        /// <returns>
        /// 이번이 <b>처음</b>이고 실제로 무언가 열렸으면 true.
        /// 발견 없음, 이미 겪은 발견, 원장 없음은 전부 false다.
        /// </returns>
        public static bool Apply(DiscoverySO discovery, UnlockLedger ledger, out DiscoverySO discovered)
        {
            discovered = null;
            if (discovery == null || ledger == null) return false;

            var key = KeyOf(discovery);
            if (key == null) return false;
            if (!ledger.Unlock(key)) return false;      // 이미 겪었다

            if (discovery.unlocks != null)
            {
                foreach (var bp in discovery.unlocks)
                {
                    if (bp == null) continue;
                    ledger.Unlock(bp.id);
                }
            }

            discovered = discovery;
            return true;
        }
    }
}
