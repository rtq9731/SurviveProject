using Survive.Core;

namespace Survive.Progression
{
    /// <summary>
    /// "이걸 만들 줄 아는가" 한 줄짜리 판정. 재료 게이팅과는 독립인 이중 잠금의
    /// 바깥쪽이다.
    /// </summary>
    public static class BlueprintGate
    {
        /// <summary>
        /// 지금 굴러가는 판의 원장. 없으면 null.
        ///
        /// 규칙 함수들이 원장을 인자로 받는 이유는 테스트 때문이고, 화면과
        /// 서비스는 매번 이 창구로 집어 온다.
        /// </summary>
        public static UnlockLedger Active =>
            GameServices.TryGet<UnlockLedger>(out var ledger) ? ledger : null;

        /// <summary>
        /// 요구가 없으면(<paramref name="required"/>가 비었으면) 언제나 열려 있다 —
        /// 기존 데이터가 그대로 동작하는 근거다.
        ///
        /// <paramref name="ledger"/>가 null이면 <b>막지 않는다</b>. 원장이 아직
        /// 서지 않았거나 순수 테스트 문맥이라는 뜻인데, 그때 잠그면 판이 통째로
        /// 얼어붙는다. 잠금은 열 수단이 있을 때만 의미가 있다.
        /// </summary>
        public static bool IsUnlocked(BlueprintSO required, UnlockLedger ledger)
        {
            if (required == null || string.IsNullOrWhiteSpace(required.id)) return true;
            if (ledger == null) return true;
            return ledger.IsUnlocked(required.id);
        }

        // 잠긴 줄에 띄울 문구를 만드는 LockText가 여기 있었다. 지웠다 —
        // 모르는 것은 이제 목록에 실리지 않으므로 띄울 자리 자체가 없고,
        // 그 문구가 청사진 이름과 hint("낫의 핵을 연구대에서 분석하면 알게 된다")를
        // 화면으로 옮기는 유일한 통로였다. 함수를 남겨 두면 언젠가 다시 쓰인다.
        // 도감(CodexCatalog)은 여기를 거치지 않고 자기 규칙으로 hint를 다룬다.
    }
}
