namespace Survive.Progression
{
    /// <summary>
    /// 해금 채널 1의 <b>둘째 계기</b>: 어떤 자리에 닿았다 (챕터 1 스펙 §8-2).
    ///
    /// <b>새 시스템이 아니다.</b> 자막도 청사진도 원장도 재료 계기와 같은 것을 쓴다
    /// (<see cref="DiscoveryChannel"/>). 다른 것은 무엇이 발견을 부르느냐 하나뿐이다.
    ///
    /// <b>왜 계기가 하나 더 필요한가.</b> 레이더는 A섬 강 건너 A-3에서 얻는다.
    /// 거기 아이템을 하나 떨궈 두고 그것을 줍게 하면 채널이 늘지 않지만, 그러면
    /// "장비를 통째로 주웠다"가 되어 제작이라는 축이 없어진다. 얻는 것은
    /// <b>거기 갔다는 사실</b>이고, 그 사실이 청사진을 연다.
    ///
    /// 순수 정적이라 Unity 실행 없이 테스트한다.
    /// </summary>
    public static class LocationDiscovery
    {
        /// <summary>
        /// 이 자리에 닿았다고 알린다.
        /// </summary>
        /// <returns>
        /// 이번이 <b>첫 도달</b>이고 실제로 무언가 열렸으면 true.
        /// 매핑이 없는 자리, 이미 겪은 발견, 원장 없음은 전부 false다.
        /// </returns>
        public static bool TryDiscover(DiscoveryBookSO book, UnlockLedger ledger,
                                       string locationId, out DiscoverySO discovered)
        {
            discovered = null;
            if (book == null || string.IsNullOrEmpty(locationId)) return false;

            return DiscoveryChannel.Apply(book.FindByLocation(locationId), ledger, out discovered);
        }
    }
}
