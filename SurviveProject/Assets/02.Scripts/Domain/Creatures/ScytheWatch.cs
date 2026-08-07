namespace Survive.Creatures
{
    /// <summary>
    /// 경계 등급의 <b>소유자</b> — 월드에 하나뿐이다 (기획서 §4.5, 스펙 §20 · §5).
    ///
    /// <b>무엇이 어긋나 있었는가</b> (스펙 §0-2 ⑨). <see cref="ScytheAlert"/>는 서 있는데
    /// 값을 들고 있는 곳이 <c>HoverDrifter</c>였다 — 곧 <b>개체마다 하나씩</b>이었다.
    /// 그러면 같은 프레임에 한 마리는 평시이고 다른 마리는 발령인 상태가 만들어질 수
    /// 있고, 그때 "다섯이 전부 꼬리를 들고 온다"는 종막의 그림이 개체별로 흩어진다.
    /// 기획서는 처음부터 <b>등급은 월드가 소유하고 개체는 읽기만 한다</b>고 적었다.
    ///
    /// <b>왜 정적 하나인가.</b> 등급은 세계에 하나뿐인 사실이고, 개체는 매 프레임
    /// 그것을 읽기만 한다 — <see cref="Survive.World.LitZoneRegistry"/>가 "이 자리가
    /// 밝은가"를 답하는 것과 같은 종류의 창구다. 인스턴스를 두면 그 인스턴스를
    /// 누가 들고 있느냐가 다시 개체별 문제가 된다.
    ///
    /// <b>올리는 것은 누구인가.</b> 지금은 아무도 아니다 — 무엇이 각성(먼 뭍 도착)과
    /// 발령(코어 탈취)을 거는지는 스펙 §5·§21의 몫이고, 여기서는 <b>자리와 소유권만</b>
    /// 세운다. 다만 그 자리가 <b>개체 바깥</b>이라는 것이 이번에 정해진 것이다.
    /// </summary>
    public static class ScytheWatch
    {
        /// <summary>
        /// 지금 경계 등급. <b>개체 쪽에는 이것을 쓰는 API가 없다</b> — 몸(<c>HoverDrifter</c>)은
        /// 이 값을 읽어 제 서식 범위를 정할 뿐이고, 바꿀 수단을 갖지 않는다.
        /// </summary>
        public static ScytheAlert Alert { get; private set; } = ScytheAlert.Calm;

        /// <summary>
        /// 등급을 올린다. <b>월드 쪽 사건만 부른다</b> — 코어 탈취(§5)와 검증이다.
        ///
        /// 내리는 길을 따로 두지 않고 값을 그대로 받는 것은, 해제 조건이 기획서에서
        /// 조건 하나(<b>코어가 둥지에 있느냐</b>)로 정해져 있기 때문이다. 올림과 내림을
        /// 다른 함수로 가르면 그 하나뿐인 조건이 두 군데로 흩어진다.
        /// </summary>
        public static void Set(ScytheAlert alert) => Alert = alert;

        /// <summary>
        /// 평시로 되돌린다. 씬 전환과 검증 사이에 부른다 —
        /// 정적 하나이므로 앞 판의 발령이 다음 판으로 새면 안 된다.
        /// </summary>
        public static void Reset() => Alert = ScytheAlert.Calm;
    }
}
