using System.Collections.Generic;

namespace Survive.Domain.Audio
{
    /// <summary>
    /// 지금 이 소리를 내도 되는가. <b>소리 하나하나가 아니라 귀를 지키는 규칙이다.</b>
    ///
    /// 곡괭이를 꾹 누르고 있으면 타격이 초당 대여섯 번 들어오고, 낫 무리가
    /// 한꺼번에 맞으면 같은 피격음이 한 프레임에 열 번 불린다. 그대로 다 내보내면
    /// 같은 파형이 위상만 어긋난 채 겹쳐 진폭이 치솟는다 — 볼륨을 낮춰도
    /// 해결되지 않는 종류의 소음이다.
    ///
    /// 그래서 세 개의 문을 세운다.
    ///   1) <b>최소 간격</b> — 같은 소리는 정해진 시간 안에 두 번 나지 않는다.
    ///   2) <b>소리별 동시 발성 상한</b> — 같은 소리가 동시에 몇 개까지 살아 있는가.
    ///   3) <b>전체 상한</b> — 모든 소리를 합쳐 몇 개까지. 재생 창구의 풀 크기와 같다.
    ///
    /// <b>여기에 Unity가 없는 이유.</b> 이 규칙이 맞는지는 소리를 들어서는 알 수 없다.
    /// 한 프레임에 쉰 번을 불렀을 때 실제로 몇 개가 나갔는지를 <i>세어야</i> 알 수 있고,
    /// 그러려면 씬 없이 돌아야 한다.
    /// </summary>
    public sealed class AudioThrottle
    {
        /// <summary>끝나는 시각을 모르는 소리(루프)의 표시.</summary>
        public const float Endless = float.PositiveInfinity;

        struct Voice
        {
            public int CueId;
            public float EndsAt;
        }

        readonly List<Voice> _voices = new List<Voice>();
        readonly Dictionary<int, float> _lastClaimed = new Dictionary<int, float>();

        public AudioThrottle(int maxVoices)
        {
            MaxVoices = maxVoices < 1 ? 1 : maxVoices;
        }

        /// <summary>동시에 살아 있을 수 있는 소리의 총수.</summary>
        public int MaxVoices { get; }

        /// <summary>지금 살아 있는 소리의 수.</summary>
        public int ActiveVoices(float now)
        {
            Expire(now);
            return _voices.Count;
        }

        /// <summary>지금 살아 있는 이 소리의 수.</summary>
        public int ActiveVoicesOf(int cueId, float now)
        {
            Expire(now);
            int n = 0;
            for (int i = 0; i < _voices.Count; i++)
                if (_voices[i].CueId == cueId) n++;
            return n;
        }

        /// <summary>
        /// 자리를 하나 잡는다. 잡았으면 true — 부르는 쪽은 그때만 실제로 재생한다.
        /// </summary>
        /// <param name="cueId">소리의 신원. 같은 값이면 같은 소리로 본다.</param>
        /// <param name="now">지금 시각(초). 단조 증가여야 한다.</param>
        /// <param name="minIntervalSeconds">같은 소리 사이의 최소 간격. 0 이하면 검사하지 않는다.</param>
        /// <param name="durationSeconds">이 소리가 살아 있을 시간. 루프는 <see cref="Endless"/>.</param>
        /// <param name="perCueLimit">이 소리의 동시 발성 상한. 0 이하면 검사하지 않는다.</param>
        public bool TryClaim(int cueId, float now, float minIntervalSeconds,
                             float durationSeconds, int perCueLimit)
        {
            Expire(now);

            // 1) 너무 빨리 또 불렀는가.
            if (minIntervalSeconds > 0f &&
                _lastClaimed.TryGetValue(cueId, out float last) &&
                now - last < minIntervalSeconds)
                return false;

            // 2) 이 소리가 이미 충분히 겹쳐 있는가.
            if (perCueLimit > 0)
            {
                int mine = 0;
                for (int i = 0; i < _voices.Count; i++)
                    if (_voices[i].CueId == cueId) mine++;
                if (mine >= perCueLimit) return false;
            }

            // 3) 전체가 찼는가. 여기서 막는 것은 풀에서 남의 소리를 빼앗지 않기 위해서다.
            if (_voices.Count >= MaxVoices) return false;

            // 길이가 0이거나 음수인 소리도 한 프레임은 자리를 차지한 것으로 본다 —
            // 그래야 같은 프레임 안의 연달은 호출이 상한에 걸린다.
            float ends = durationSeconds > 0f ? now + durationSeconds : now;
            _voices.Add(new Voice { CueId = cueId, EndsAt = ends });
            _lastClaimed[cueId] = now;
            return true;
        }

        /// <summary>
        /// 루프를 멈출 때 자리를 돌려준다. 같은 소리의 자리 하나만 지운다.
        /// 끝나는 시각이 있는 소리는 저절로 빠지므로 부를 필요가 없다.
        /// </summary>
        public void Release(int cueId)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                if (_voices[i].CueId != cueId) continue;
                _voices.RemoveAt(i);
                return;
            }
        }

        /// <summary>
        /// 전부 잊는다. 씬을 다시 올리면 <c>Time</c>이 0으로 돌아가는데,
        /// 그때 옛 기록이 남아 있으면 "아직 최소 간격 안"으로 읽혀 한동안 소리가 죽는다.
        /// </summary>
        public void Reset()
        {
            _voices.Clear();
            _lastClaimed.Clear();
        }

        /// <summary>
        /// 끝난 소리를 걷어낸다.
        ///
        /// <b>끝나는 시각이 정확히 <i>지금</i>인 것은 남긴다.</b> 길이를 모르는 소리는
        /// 잡을 때 <c>EndsAt = now</c>로 적어 두는데, 여기서 그것까지 걷으면 같은 프레임
        /// 안의 다음 호출에게 자리가 그대로 비어 보인다 — 한 프레임에 쉰 번을 불러도
        /// 쉰 번이 다 나가게 된다. 시간이 한 눈금이라도 흐른 뒤에 걷는다.
        /// </summary>
        void Expire(float now)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
                if (_voices[i].EndsAt < now) _voices.RemoveAt(i);
        }
    }
}
