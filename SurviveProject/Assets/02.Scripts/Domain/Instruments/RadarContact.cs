namespace Survive.Instruments
{
    /// <summary>
    /// 결과 화면에 무엇이라고 적을 것인가. <b>판정에는 쓰이지 않는다.</b>
    ///
    /// 이 열거형이 판정에 한 번이라도 끼면 그 순간 "낫이라서 안 잡힌다"가 되고,
    /// 물성에서 나온 규칙이 종족 목록으로 타락한다. 그래서
    /// <see cref="RadarDetection"/>은 이 값을 읽지 않는다 — 그 사실을 테스트가 지킨다.
    /// </summary>
    public enum RadarContactKind
    {
        Unknown = 0,

        /// <summary>물 위로 솟은 큰 덩어리. 다른 섬.</summary>
        Island = 1,

        /// <summary>바다 아래 빈 곳. 공동.</summary>
        Cavity = 2,

        /// <summary>깊은 층 자체. 지하의 하늘.</summary>
        DeepLayer = 3,

        /// <summary>사람이 만든 것. 지하 구조물.</summary>
        Structure = 4,

        /// <summary>살아 움직이는 것.</summary>
        Creature = 5,

        /// <summary>지표의 갈라진 틈.</summary>
        Fissure = 6,
    }

    /// <summary>
    /// 레이더 앞에 놓인 것 하나를 <b>물리량으로만</b> 적은 것.
    ///
    /// 여기 담긴 것은 크기·속도·거리·깊이뿐이다. "이것은 낫이다"는 정보는
    /// <see cref="kind"/>에 들어 있지만 그것은 잡힌 뒤에 이름을 붙이기 위한 것이고,
    /// 잡히느냐 마느냐는 오직 앞의 넷과 <see cref="RadarBand"/>가 정한다.
    /// </summary>
    public class RadarContact
    {
        /// <summary>번역 표에서 이름을 찾는 열쇠. 화면에 그대로 찍지 않는다.</summary>
        public string id;

        public RadarContactKind kind = RadarContactKind.Unknown;

        /// <summary>가장 긴 쪽의 길이(m). 해상도와 견준다.</summary>
        public float sizeMeters;

        /// <summary>움직이는 빠르기(m/s). 붙박이면 0이다.</summary>
        public float speedMps;

        /// <summary>관측 지점에서의 수평 거리(m).</summary>
        public float distanceMeters;

        /// <summary>지표 아래로 들어간 깊이(m). 물 위나 지표면이면 0이다.</summary>
        public float depthMeters;

        /// <summary>관측 지점에서 본 방위(도). 북쪽이 0, 시계 방향.</summary>
        public float bearingDegrees;
    }
}
