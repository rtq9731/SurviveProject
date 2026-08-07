namespace Survive.Creatures
{
    /// <summary>
    /// 낫의 <b>몸통</b>이 얼마나 빛나는가 (기획서 §4.5 "아트").
    ///
    /// <b>고친 것.</b> 이 프리팹의 <c>Consumer_Blade</c> 머티리얼은 자홍으로 <b>상시</b>
    /// 발광하고, 그것을 쓰는 부품이 열둘이다(Jaw·Fang·Fin·FinTip·Scythe·ScytheTip·Claw
    /// 좌우). 그래서 꼬리가 무슨 자세를 잡든 화면 전체 발광의 대부분을 몸통이 차지했고,
    /// <b>작업 중과 공격 태세가 구별되지 않았다</b> — 실측으로 총량 비가 1.16배였다.
    /// 상태 표시등이 꼬리 하나라는 설계(<see cref="ScythePosture"/>)가 화면에서는
    /// 성립하지 않고 있었던 셈이다.
    ///
    /// <b>그렇다고 끄지는 않는다.</b> 환경광 0인 세계에서 발광을 전부 빼면 낫은 완전한
    /// 검은 실루엣이 되고, 기획서 §3 "아트"가 요구하는 <b>금속 재질과 라이팅</b>이
    /// 읽히지 않는다. 남기되 <b>응축된 소수의 라인</b>으로 남긴다.
    ///
    /// <b>무엇을 남기는가 — 끝단이다.</b> 이 개체의 생김새를 정하는 것은 날의 끝이므로
    /// 이름이 <c>Tip</c>으로 끝나는 부품(ScytheTipL/R·FinTipL/R)만 잔광을 남기고
    /// 나머지 몸통은 검게 둔다. 이름 규칙으로 적은 이유는 <b>모델이 교체될 예정</b>이기
    /// 때문이다(스펙 §17) — 새 몸이 와도 끝단이라고 이름 붙은 것이 같은 답을 받는다.
    /// </summary>
    public static class ScytheBodyEmission
    {
        /// <summary>
        /// 남기는 끝단의 세기.
        ///
        /// <b>기준은 꼬리에서 가장 어두운 값이다.</b> 공격 태세의 호는 0.1이고 호의 세기
        /// 상한이 3.2이므로 화면에 나오는 가장 어두운 꼬리는 0.32다. 몸통 한 줄이 그보다
        /// 세면 어두운 자세에서 눈이 꼬리가 아니라 몸통을 따라간다. 그 아래로 잡되
        /// 0은 아니게 — 정리 전(2.2)의 <b>1/10</b>이다.
        /// </summary>
        public const float TipLine = 0.22f;

        /// <summary>몸통의 나머지. 실루엣과 라이팅만 남는다.</summary>
        public const float Dark = 0f;

        /// <summary>끝단 부품 이름의 꼬리표. 프리팹의 <c>ScytheTipL</c>·<c>FinTipR</c> 꼴이다.</summary>
        public const string TipMarker = "Tip";

        /// <summary>이 부품이 잔광을 남기는 끝단인가.</summary>
        public static bool KeepsLine(string partName) =>
            !string.IsNullOrEmpty(partName) && partName.Contains(TipMarker);

        /// <summary>이 부품이 낼 발광 세기.</summary>
        public static float LevelFor(string partName) => KeepsLine(partName) ? TipLine : Dark;
    }
}
