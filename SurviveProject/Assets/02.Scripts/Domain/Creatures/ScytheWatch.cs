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
    /// <b>올리는 것은 누구인가.</b> 각성은 이 파일이 든 구역 판정이 올린다
    /// (<see cref="ObserveAt"/>). 발령은 아직 아무도 아니다 — 무엇이 각성(먼 뭍 도착)과
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
        public static void Reset()
        {
            Alert = ScytheAlert.Calm;
            _awakening = default;
        }

        // ══ 개체수 ═════════════════════════════════════════════

        /// <summary>
        /// 지금 세계에 있어야 할 낫의 수. <b>등급의 함수이지 따로 저장하는 값이 아니다</b>
        /// (<see cref="ScytheCensus"/>). 몸(<c>ScytheSpawner</c>)이 이 수를 보고 맞춘다.
        ///
        /// <b>원장을 새로 두지 않은 근거가 여기 한 줄로 있다.</b> 저장할 것은 등급
        /// 하나이고, 그것은 이미 이 파일이 든다. 수를 따로 저장하면 "등급은 발령인데
        /// 수는 하나"인 상태가 만들어질 수 있고 그것을 막는 코드를 또 써야 한다.
        /// </summary>
        public static int Population => ScytheCensus.CountFor(Alert);

        // ══ 각성 방아쇠 ════════════════════════════════════════

        /// <summary>
        /// 각성을 거는 구역 판정. <b>방아쇠 구역이 비어 있다</b> —
        /// 어느 자리인지는 지형이 선 뒤에 사람이 정한다
        /// (<see cref="Survive.World.ZoneArrival"/>).
        /// </summary>
        static Survive.World.ZoneArrival _awakening;

        /// <summary>
        /// 각성 방아쇠가 정해져 있는가. <b>지금은 거짓이고, 그것이 정상이다.</b>
        /// 검증이 이 값을 물어 "아직 미결"을 값으로 남긴다 — 정해지는 날 그 테스트가
        /// 먼저 빨개져서, 방아쇠가 생긴 것이 사고가 아니라 결정이었음을 남긴다.
        /// </summary>
        public static bool AwakeningArmed => _awakening.HasTarget;

        /// <summary>
        /// <b>사람이 자리를 정하는 날 고칠 곳은 이 한 줄이다.</b>
        /// <c>ArmAwakening(SurfaceZone.어디)</c>를 세계가 세워질 때 한 번 부르면 된다.
        ///
        /// 함수로 열어 두는 것은 검증이 방아쇠를 세워 보고 되돌릴 수 있어야 하기
        /// 때문이다. 게임 코드에서는 아직 아무도 부르지 않는다.
        /// </summary>
        public static void ArmAwakening(Survive.World.SurfaceZone zone) =>
            _awakening = Survive.World.ZoneArrival.Watching(zone);

        /// <summary>
        /// 사람이 지금 이 자리에 있다고 알려 준다. <b>방아쇠 구역에 처음 닿는 순간</b>
        /// 평시가 각성으로 오른다.
        ///
        /// <b>내려가지 않는다.</b> 각성은 낫이 사람을 알아본 것이지 사람이 어디에
        /// 서 있는가가 아니다 — 돌아가면 풀린다면 한 발 물러서는 것이 위협을 끄는
        /// 스위치가 된다.
        ///
        /// <b>발령을 덮지 않는다.</b> 이미 발령이면 각성으로 내려올 이유가 없다.
        /// </summary>
        /// <returns>이번에 각성이 걸렸으면 참.</returns>
        public static bool ObserveAt(UnityEngine.Vector3 position)
        {
            if (!_awakening.ObserveAt(position)) return false;
            if (Alert == ScytheAlert.Alarmed) return false;

            Alert = ScytheAlert.Awake;
            return true;
        }
    }
}
