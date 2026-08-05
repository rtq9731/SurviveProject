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

        /// <summary>
        /// 잠긴 줄에 그대로 띄울 한 줄.
        ///
        /// 이름과 힌트를 가르는 것은 <b>쌍점</b>이다. 줄표(em dash)를 쓰던 시절이
        /// 있었는데 본문 글꼴(ChosunGu)에 그 글자가 없어 화면에는 두부(□)가 떴다.
        /// 화면에 나가는 문자는 글꼴이 아는 것만 쓴다.
        /// </summary>
        public static string LockText(BlueprintSO required)
        {
            if (required == null) return "[잠김]";

            string name = string.IsNullOrWhiteSpace(required.displayName)
                ? required.id
                : required.displayName;

            return string.IsNullOrWhiteSpace(required.hint)
                ? $"[잠김] {name} 청사진이 필요하다"
                : $"[잠김] {name} 청사진이 필요하다: {required.hint}";
        }
    }
}
