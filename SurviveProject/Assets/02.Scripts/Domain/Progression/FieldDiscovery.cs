namespace Survive.Progression
{
    /// <summary>
    /// 해금 채널 1 — 현장 발견의 규칙.
    ///
    /// "처음 주웠다"를 따로 세어 두지 않는다. 발견 자체가 원장의 열쇠 하나라,
    /// 넣어 보고 새로 들어갔으면 그게 첫 습득이다. 세는 자리가 둘이면
    /// 언젠가 둘이 어긋난다.
    ///
    /// 순수 정적이라 Unity 실행 없이 테스트한다.
    /// </summary>
    public static class FieldDiscovery
    {
        /// <summary>발견 기록에 쓰는 열쇠. 에셋에 id가 비어 있어도 겹치지 않게 만든다.</summary>
        public static string KeyOf(DiscoverySO discovery)
        {
            if (discovery == null) return null;
            if (!string.IsNullOrWhiteSpace(discovery.id)) return discovery.id;
            return discovery.item != null ? "discovery:" + discovery.item.id : null;
        }

        /// <summary>
        /// 이 아이템을 손에 넣었다고 알린다.
        /// </summary>
        /// <returns>
        /// 이번이 <b>첫 습득</b>이고 실제로 무언가 열렸으면 true.
        /// 매핑이 없는 아이템, 이미 겪은 발견, 원장 없음은 전부 false다.
        /// </returns>
        public static bool TryDiscover(DiscoveryBookSO book, UnlockLedger ledger,
                                       string itemId, out DiscoverySO discovered)
        {
            discovered = null;
            if (book == null || ledger == null || string.IsNullOrEmpty(itemId)) return false;

            var d = book.Find(itemId);
            if (d == null) return false;

            var key = KeyOf(d);
            if (key == null) return false;
            if (!ledger.Unlock(key)) return false;      // 이미 겪었다

            if (d.unlocks != null)
            {
                foreach (var bp in d.unlocks)
                {
                    if (bp == null) continue;
                    ledger.Unlock(bp.id);
                }
            }

            discovered = d;
            return true;
        }
    }
}
