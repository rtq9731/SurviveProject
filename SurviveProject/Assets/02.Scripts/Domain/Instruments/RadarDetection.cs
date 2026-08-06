using System.Collections.Generic;

namespace Survive.Instruments
{
    /// <summary>왜 안 잡혔는가. 잡혔으면 <see cref="Detected"/>다.</summary>
    public enum RadarVerdict
    {
        /// <summary>잡혔다.</summary>
        Detected = 0,

        /// <summary>닿는 거리 밖이다.</summary>
        OutOfRange = 1,

        /// <summary>해상도 한 칸보다 작아 뭉개진다.</summary>
        BelowResolution = 2,

        /// <summary>한 장을 얻는 사이에 한 칸을 벗어나 번진다.</summary>
        FasterThanRefresh = 3,

        /// <summary>투과 깊이보다 깊다.</summary>
        TooDeep = 4,
    }

    /// <summary>
    /// 무엇이 잡히고 무엇이 안 잡히는가. <b>규칙은 셋뿐이고 셋 다 파장에서 나온다.</b>
    ///
    /// 종류를 보지 않는다는 것이 이 클래스의 전부다. 낫이 안 잡히는 것은 낫이어서가
    /// 아니라 <b>작고 빠르기 때문</b>이고, 발밑 균열이 안 잡히는 것은 균열이어서가
    /// 아니라 <b>작기 때문</b>이다. 그래서 나중에 아주 큰 낫이 나오면 그것은 잡히고,
    /// 그것이 옳다 — 큰 것은 잡힌다는 것이 이 장치의 성질이다.
    ///
    /// 순수 정적이라 Unity 실행 없이 테스트한다.
    /// </summary>
    public static class RadarDetection
    {
        /// <summary>
        /// 이 접촉이 잡히는가, 안 잡히면 왜인가.
        ///
        /// <b>검사 순서에 뜻이 있다.</b> 거리 → 크기 → 속도 → 깊이. 하나가 걸려도
        /// 다른 것들이 함께 걸려 있을 수 있으므로(낫은 작고 <i>또</i> 빠르다)
        /// 여기서 돌려주는 것은 "첫 번째 이유"이지 유일한 이유가 아니다.
        /// 그 사실이 중요한 자리에서는 <see cref="Reasons"/>를 쓴다.
        /// </summary>
        public static RadarVerdict Evaluate(RadarBand band, RadarContact contact)
        {
            if (band == null || contact == null) return RadarVerdict.OutOfRange;

            if (contact.distanceMeters > band.rangeMeters) return RadarVerdict.OutOfRange;
            if (contact.sizeMeters < band.ResolutionMeters) return RadarVerdict.BelowResolution;
            if (contact.speedMps > band.MaxTrackableSpeedMps) return RadarVerdict.FasterThanRefresh;
            if (contact.depthMeters > band.PenetrationDepthMeters) return RadarVerdict.TooDeep;

            return RadarVerdict.Detected;
        }

        public static bool CanDetect(RadarBand band, RadarContact contact) =>
            Evaluate(band, contact) == RadarVerdict.Detected;

        /// <summary>
        /// 걸리는 이유를 <b>전부</b> 모은다.
        ///
        /// 낫이 안 잡히는 이유가 둘(작다·빠르다)이라는 것은 우연이 아니라 저주파의
        /// 성질이 두 방향에서 같은 결론을 내는 것이다. 하나만 고쳐도 여전히 안 잡힌다는
        /// 사실을 테스트가 못 박으려면 이유를 하나로 줄여 놓으면 안 된다.
        /// </summary>
        public static List<RadarVerdict> Reasons(RadarBand band, RadarContact contact)
        {
            var reasons = new List<RadarVerdict>();
            if (band == null || contact == null) return reasons;

            if (contact.distanceMeters > band.rangeMeters) reasons.Add(RadarVerdict.OutOfRange);
            if (contact.sizeMeters < band.ResolutionMeters) reasons.Add(RadarVerdict.BelowResolution);
            if (contact.speedMps > band.MaxTrackableSpeedMps) reasons.Add(RadarVerdict.FasterThanRefresh);
            if (contact.depthMeters > band.PenetrationDepthMeters) reasons.Add(RadarVerdict.TooDeep);

            return reasons;
        }

        /// <summary>
        /// 한 번 훑어 잡힌 것만 골라 낸다. 가까운 것부터 세운다 —
        /// 화면이 몇 줄만 보여 줄 때 먼 섬이 밀려나는 편이 낫다.
        /// </summary>
        public static List<RadarContact> Sweep(RadarBand band, IEnumerable<RadarContact> candidates)
        {
            var found = new List<RadarContact>();
            if (band == null || candidates == null) return found;

            foreach (var c in candidates)
                if (c != null && CanDetect(band, c)) found.Add(c);

            found.Sort((a, b) => a.distanceMeters.CompareTo(b.distanceMeters));
            return found;
        }
    }
}
