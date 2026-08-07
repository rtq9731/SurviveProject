using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// <b>세계의 시각. 「지금이 언제인가」에 답하는 자리는 여기 하나다.</b>
    ///
    /// <b>왜 <c>Time.time</c>이 아닌가.</b> 프레임 시계는 세 가지 성질을 갖는데,
    /// 셋 다 <b>세계 상태가 원하지 않는 것</b>이다.
    /// <list type="number">
    /// <item><b>저장되지 않는다.</b> 다 캔 자리가 언제 비었는지가 저장본에 없으면,
    ///   불러온 세계는 「막 캔 자리」와 「거의 다 찬 자리」를 구별하지 못한다.</item>
    /// <item><b>배속을 탄다.</b> 검증이 8배속으로 돌리면 재생 주기도 8배로 빨라진다.
    ///   그러면 검증이 재는 것은 게임의 값이 아니라 검증 자신의 설정이다.</item>
    /// <item><b>씬 로드로 0으로 돌아간다.</b> 돌아갔는데 <c>_depletedAt</c>만 큰 값으로
    ///   남으면 <c>now - depletedAt</c>이 음수가 되어 그 자리는 <b>영영</b> 안 돌아온다.</item>
    /// </list>
    ///
    /// <b>그래서 이 시계는 셋을 뒤집는다</b> — 저장되고(<c>DayNightService</c>가 그
    /// 값을 저장본에 싣는다), 배속을 곱하지 않으며(받은 초를 그대로 더한다),
    /// 씬을 갈아 끼워도 살아 있다(정적이고, 모는 쪽이 <c>DontDestroyOnLoad</c>다).
    ///
    /// <b>배속과 일시정지는 다른 것이다.</b> 배속은 <b>화면 연출</b>이라 세계의
    /// 시각에 곱하지 않는다. 반면 일시정지는 <b>세계가 멈춘 것</b>이므로 존중한다 —
    /// 그 구분을 <see cref="Paused"/> 하나가 들고 있고, 이 클래스 안에는 배율을
    /// 받는 자리가 <b>아예 없다</b>. 그것이 「배속을 안 탄다」의 뜻이다.
    ///
    /// <b>되감지 않는다.</b> 시각을 뒤로 돌리면 다 캔 시각과 불을 지핀 시각이
    /// <b>미래</b>가 되고, 그 자리들은 영영 돌아오지 않는다. 저장본을 불러올 때만
    /// 예외로 값을 통째로 앉힌다(<see cref="Restore"/>) — 그때는 되감는 것이 아니라
    /// <b>그 세계의 시각으로 갈아 끼우는 것</b>이다.
    ///
    /// 정적인 이유는 <see cref="LitZoneRegistry"/>와 같다 — 창구가 하나여야
    /// 서로 다른 답이 나오지 않는다. 검증은 <see cref="Reset"/>으로 비운다.
    /// </summary>
    public static class WorldClock
    {
        static double _seconds;

        /// <summary>
        /// <b>재생을 켤 때마다 새 세계다.</b> 정적인 값은 도메인 리로드를 끈
        /// 에디터에서 <b>앞 판을 살아서 넘어온다</b> — 그러면 두 번째 판이
        /// 앞 판의 시각에서 시작하고, 씬이 새로 놓아둔 채집물들이 「이미 다 캔
        /// 지 오래」인 세계에 서게 된다. 판이 바뀌는 자리에서 한 번 비운다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void 판이_바뀌면_비운다()
        {
            Reset();
            WorldLedgerRegistry.Clear();
        }

        /// <summary>흐른 세계 초. 하루를 넘어도 계속 자란다.</summary>
        public static double Now => _seconds;

        /// <summary>
        /// 같은 값을 <c>float</c>로. 규칙들(<c>HarvestRespawnRule</c> 등)이 <c>float</c>로
        /// 재므로 경계에서 한 번만 좁힌다. 100시간을 놀아도 0.03초 해상도라
        /// 분 단위 재생 주기에는 넉넉하다.
        /// </summary>
        public static float Seconds => (float)_seconds;

        /// <summary>
        /// 세계가 멈춰 있는가. 참인 동안 <see cref="Advance"/>는 아무 일도 하지 않는다.
        /// <b>일시정지는 존중하고 배속은 무시한다</b>는 구분이 이 한 값이다.
        /// </summary>
        public static bool Paused { get; set; }

        /// <summary>
        /// 이 시계가 한 번이라도 세워졌는가. 시계를 모는 쪽이 <b>시작 시각을
        /// 다시 앉혀도 되는지</b>를 이것으로 가른다 — 이미 흐르고 있는 시계를
        /// 새 컴포넌트가 아침으로 되돌리면 그 세션의 재생 시각이 전부 미래가 된다.
        /// </summary>
        public static bool Started { get; private set; }

        /// <summary>
        /// 이만큼 흘려보낸다. <b>배율을 받는 인자가 없다</b> — 받은 초가 곧 흐른 초다.
        /// 음수는 무시한다(되감지 않는다). 멈춰 있으면 아무 일도 하지 않는다.
        /// </summary>
        public static void Advance(double seconds)
        {
            if (Paused || seconds <= 0.0) return;
            _seconds += seconds;
            Started = true;
        }

        /// <summary>
        /// 이만큼 건너뛴다. 멈춰 있어도 건너뛴다 — 이것은 흐름이 아니라
        /// <b>손으로 옮기는 것</b>이라 일시정지와 무관하다. 음수는 무시한다.
        /// </summary>
        public static void Skip(double seconds)
        {
            if (seconds <= 0.0) return;
            _seconds += seconds;
            Started = true;
        }

        /// <summary>
        /// 세계를 <b>처음</b> 세운다. 이미 세워져 있으면 아무 일도 하지 않는다 —
        /// 시계를 모는 컴포넌트가 씬마다 다시 깨어나도 시각이 아침으로
        /// 되돌아가지 않게 하는 것이 이 「처음뿐」의 뜻이다.
        /// </summary>
        public static bool StartAt(double seconds)
        {
            if (Started) return false;
            _seconds = seconds < 0.0 ? 0.0 : seconds;
            Started = true;
            return true;
        }

        /// <summary>
        /// 저장본이 적어 둔 시각으로 갈아 끼운다. <b>불러오기만 이 문을 쓴다.</b>
        /// 되감기가 아니라 다른 세계의 시각을 앉히는 것이므로 뒤로 가도 된다.
        /// </summary>
        public static void Restore(double seconds)
        {
            _seconds = seconds < 0.0 ? 0.0 : seconds;
            Started = true;
        }

        /// <summary>
        /// 앞으로만 간다는 약속을 지키면서 <paramref name="seconds"/>로 옮긴다.
        /// 목표가 지금보다 뒤에 있으면 <b>다음 번 그 자리</b>로 간다.
        /// </summary>
        /// <param name="seconds">가고 싶은 절대 초.</param>
        /// <param name="period">한 바퀴의 길이. 0 이하면 그냥 앞으로만 간다.</param>
        public static void ForwardTo(double seconds, double period)
        {
            if (seconds > _seconds) { _seconds = seconds; Started = true; return; }
            if (period <= 0.0) return;

            // 지나간 시각을 달라고 하면 다음 바퀴의 같은 자리로 보낸다.
            double bumps = System.Math.Floor((_seconds - seconds) / period) + 1.0;
            _seconds = seconds + bumps * period;
            Started = true;
        }

        /// <summary>검증·씬 전환 사이에 시계를 비운다.</summary>
        public static void Reset()
        {
            _seconds = 0.0;
            Paused = false;
            Started = false;
        }
    }
}
