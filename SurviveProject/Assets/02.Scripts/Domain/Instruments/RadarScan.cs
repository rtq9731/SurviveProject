using UnityEngine;

namespace Survive.Instruments
{
    /// <summary>관측 한 번의 상태.</summary>
    public enum RadarScanState
    {
        /// <summary>아무것도 하고 있지 않다.</summary>
        Idle = 0,

        /// <summary>쌓는 중. 서 있어야 한다.</summary>
        Scanning = 1,

        /// <summary>다 쌓았다. 결과가 있다.</summary>
        Complete = 2,

        /// <summary>도중에 못 쓰게 됐다. 쓴 배터리는 돌아오지 않는다.</summary>
        Cancelled = 3,
    }

    /// <summary>왜 끊겼는가.</summary>
    public enum RadarCancelReason
    {
        None = 0,

        /// <summary>움직여서 위상 기준을 잃었다.</summary>
        Moved = 1,

        /// <summary>전원이 다했다.</summary>
        PowerOut = 2,

        /// <summary>사람이 껐다.</summary>
        Aborted = 3,
    }

    /// <summary>
    /// 관측 한 번 — "언제 시작했고 지금 어디까지, 배터리를 얼마나 먹었나".
    ///
    /// <see cref="Survive.Progression.ResearchJob"/>과 같은 자리이되 두 가지가 다르다.
    /// 하나는 <b>끊길 수 있다</b>는 것이고, 다른 하나는 <b>끊겨도 낸 값은 돌아오지
    /// 않는다</b>는 것이다. 그 둘이 이 장치의 사용 비용을 만든다 — 정보를 사는 값이
    /// 배터리 + 정지 시간이고, 값을 치르다 말면 정보 없이 배터리만 잃는다.
    ///
    /// <b>왜 움직이면 끊기는가.</b> 저주파의 되돌아오는 신호는 약해서 여러 장을 겹쳐
    /// 쌓아야 형상이 선다. 겹치려면 장치가 같은 자리에 있어야 하고
    /// (<see cref="RadarBand.CoherenceRadiusMeters"/>), 그 반경은 걸음 한 번보다 좁다.
    /// 게임 규칙으로 "이동 금지"를 건 것이 아니라 원리가 그렇게 시킨다.
    ///
    /// 순수 C#이라 Unity 실행 없이 테스트한다. 시간도 위치도 밖에서 넣어 준다.
    /// </summary>
    public class RadarScan
    {
        public RadarScan(RadarBand band, float scanSeconds, float batteryPerSecond)
        {
            Band = band ?? new RadarBand();
            ScanSeconds = Mathf.Max(0.01f, scanSeconds);
            BatteryPerSecond = Mathf.Max(0f, batteryPerSecond);
        }

        public RadarBand Band { get; }

        /// <summary>한 번 다 쌓는 데 드는 시간(초).</summary>
        public float ScanSeconds { get; }

        /// <summary>초당 먹는 배터리.</summary>
        public float BatteryPerSecond { get; }

        /// <summary>한 번에 드는 배터리 전부.</summary>
        public float FullCost => ScanSeconds * BatteryPerSecond;

        public RadarScanState State { get; private set; } = RadarScanState.Idle;
        public RadarCancelReason CancelReason { get; private set; } = RadarCancelReason.None;

        /// <summary>여기까지 쌓은 시간.</summary>
        public float Elapsed { get; private set; }

        /// <summary>지금까지 먹은 배터리. 끊겨도 줄어들지 않는다.</summary>
        public float Drawn { get; private set; }

        /// <summary>게이지가 읽는 값 0~1.</summary>
        public float Progress => Mathf.Clamp01(Elapsed / ScanSeconds);

        public float SecondsLeft => Mathf.Max(0f, ScanSeconds - Elapsed);

        public bool IsRunning => State == RadarScanState.Scanning;

        /// <summary>
        /// 관측을 건다. 전원이 <b>끝까지 갈 만큼</b> 없으면 시작조차 하지 않는다.
        ///
        /// 반쯤 하다 꺼지게 두면 장치가 조용히 배터리만 먹고 아무것도 안 준다.
        /// 값을 치를 수 없을 때는 값을 받지 않는 편이 정직하다 — 도중에 꺼지는 길은
        /// 그래도 남아 있다(랜턴이 같은 통을 함께 먹으므로).
        /// </summary>
        public bool Begin(float availableCharge)
        {
            if (State == RadarScanState.Scanning) return false;
            if (availableCharge < FullCost) return false;

            State = RadarScanState.Scanning;
            CancelReason = RadarCancelReason.None;
            Elapsed = 0f;
            Drawn = 0f;
            return true;
        }

        /// <summary>
        /// 시간을 흘린다.
        /// </summary>
        /// <param name="deltaSeconds">지난 시간.</param>
        /// <param name="displacementMeters">관측을 건 자리에서 지금까지 벗어난 거리.</param>
        /// <param name="availableCharge">지금 남아 있는 배터리.</param>
        /// <returns>이번에 먹은 배터리. 부르는 쪽이 실제 통에서 뺀다.</returns>
        public float Tick(float deltaSeconds, float displacementMeters, float availableCharge)
        {
            if (State != RadarScanState.Scanning) return 0f;
            if (deltaSeconds <= 0f) return 0f;

            // 움직임을 먼저 본다. 이미 못 쓰게 된 장을 위해 배터리를 더 먹으면
            // 값만 치르고 아무것도 못 사는 구간이 한 틱 생긴다.
            if (displacementMeters > Band.CoherenceRadiusMeters)
            {
                Cancel(RadarCancelReason.Moved);
                return 0f;
            }

            float draw = BatteryPerSecond * deltaSeconds;
            if (draw > availableCharge)
            {
                Cancel(RadarCancelReason.PowerOut);
                return 0f;
            }

            Drawn += draw;
            Elapsed += deltaSeconds;

            if (Elapsed >= ScanSeconds)
            {
                Elapsed = ScanSeconds;
                State = RadarScanState.Complete;
            }

            return draw;
        }

        /// <summary>사람이 껐다.</summary>
        public void Abort()
        {
            if (State != RadarScanState.Scanning) return;
            Cancel(RadarCancelReason.Aborted);
        }

        /// <summary>다음 관측을 걸 수 있게 되돌린다. 결과 화면을 닫을 때 부른다.</summary>
        public void Reset()
        {
            State = RadarScanState.Idle;
            CancelReason = RadarCancelReason.None;
            Elapsed = 0f;
            Drawn = 0f;
        }

        void Cancel(RadarCancelReason reason)
        {
            State = RadarScanState.Cancelled;
            CancelReason = reason;
        }
    }
}
