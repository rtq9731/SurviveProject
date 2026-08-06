namespace Survive.Progression
{
    /// <summary>
    /// 해금 채널 1 — 현장 발견의 <b>계기 하나</b>: 재료를 처음 손에 넣었다.
    ///
    /// "처음 주웠다"를 따로 세어 두지 않는다. 발견 자체가 원장의 열쇠 하나라,
    /// 넣어 보고 새로 들어갔으면 그게 첫 습득이다. 세는 자리가 둘이면
    /// 언젠가 둘이 어긋난다.
    ///
    /// 뒤에 일어나는 일(원장·청사진·대사)은 <see cref="DiscoveryChannel"/>이 든다 —
    /// 장소 계기(<see cref="LocationDiscovery"/>)와 같은 몸통을 쓴다.
    ///
    /// 순수 정적이라 Unity 실행 없이 테스트한다.
    /// </summary>
    public static class FieldDiscovery
    {
        /// <summary>발견 기록에 쓰는 열쇠. <see cref="DiscoveryChannel.KeyOf"/>와 같다.</summary>
        public static string KeyOf(DiscoverySO discovery) => DiscoveryChannel.KeyOf(discovery);

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
            if (book == null || string.IsNullOrEmpty(itemId)) return false;

            return DiscoveryChannel.Apply(book.Find(itemId), ledger, out discovered);
        }
    }
}
