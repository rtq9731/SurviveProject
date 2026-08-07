using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>
    /// 액체 한 자락. <b>깊이와 폭 둘로만 적힌다.</b>
    ///
    /// 강·얕은 바다·바다가 서로 다른 것은 이 두 수뿐이다. 물질도 같고
    /// 농도도 같고 규칙도 같다 — 기획서 §2.1 "강과 바다는 같은 옅은 구간이고
    /// 다른 것은 폭뿐이며, 얕은 바다가 무해한 것은 발을 딛고 선 자리에
    /// 값을 매기지 않는 규칙이 그대로 걸린 것이다".
    /// </summary>
    public readonly struct LiquidBody
    {
        /// <summary>바닥까지의 깊이(m). 발을 딛는가 뜨는가를 이것이 정한다.</summary>
        public readonly float Depth;

        /// <summary>가로질러야 하는 폭(m). 잠겨 있는 시간이 여기서 나온다.</summary>
        public readonly float Width;

        public LiquidBody(float depth, float width)
        {
            Depth = depth;
            Width = width;
        }

        /// <summary>폭만 바꾼 같은 액체. "폭이 답을 바꾸는가"를 묻는 검사에 쓴다.</summary>
        public LiquidBody Widened(float width) => new LiquidBody(Depth, width);
    }

    /// <summary>액체 한 자락 앞에 선 사람에게 일어나는 일.</summary>
    public enum CrossingVerdict
    {
        /// <summary>아무 일도 없다. 발을 딛고 걸어서 건넌다.</summary>
        Harmless,

        /// <summary>건너지만 값을 치른다. 잠긴 시간만큼 살이 깎인다.</summary>
        Costly,

        /// <summary>맨몸으로는 건너다 죽는다. 헤엄쳐서 닿을 수 있는 거리가 아니다.</summary>
        Fatal,

        /// <summary>액면 보행 장비가 받쳐 준다. 잠기지 않으므로 값이 없다.</summary>
        Supported,
    }

    /// <summary>
    /// <b>액체 앞에서 무슨 일이 벌어지는가 — 판정은 하나다.</b>
    ///
    /// A섬 안에서 액체는 세 번 다른 얼굴로 나온다: 강은 아프고, 얕은 바다는 무해하고,
    /// 바다는 죽인다. <b>그 셋은 세 개의 특례가 아니라 한 규칙의 세 값이다</b>(기획서 §2.1).
    /// 여기서 하는 계산은 하나뿐이다:
    ///
    /// <list type="number">
    /// <item>깊이가 상태를 정한다 — 발을 딛는가(<see cref="SeaImmersion.Wading"/>),
    ///       뜨는가(<see cref="SeaImmersion.Swimming"/>)</item>
    /// <item>폭이 시간을 정한다 — 헤엄쳐 건너는 데 걸리는 초</item>
    /// <item>그 둘을 <see cref="MacroniumSea"/>에 넣으면 값이 나온다</item>
    /// </list>
    ///
    /// 값이 0이면 무해하고, 체력보다 작으면 아프고, 체력 이상이면 죽는다.
    /// <b>분기는 그 하나뿐이고 구역 이름은 어디에도 나오지 않는다</b> — 강이라서 아픈 것이
    /// 아니라 깊고 넓어서 아픈 것이고, 얕은 바다가 무해한 것은 예외를 받아서가 아니라
    /// 발이 바닥에 닿아서다.
    ///
    /// <b>왜 순서가 단조 증가가 아닌가.</b> 아프다(강) → 무해(얕은 바다) → 죽음(바다).
    /// 계속 세지기만 하면 플레이어가 배우는 것은 "다음 물은 더 아프겠지" 하나뿐이고
    /// 그것은 규칙이 아니라 경사다. 한 번 풀어 주면 <b>"이 물이 어떤 물인지 봐야 한다"</b>를
    /// 배우게 된다. 이 역전은 설계이므로 <c>IslandZoneTests</c>가 못 박아 둔다.
    /// </summary>
    public static class LiquidCrossing
    {
        /// <summary>
        /// 발이 잠기기 시작하는 깊이(m). <c>PlayerSwimming.wadeDepth</c>의 기본값과 같다 —
        /// 판정을 Domain에서 하려면 그 수가 여기에도 있어야 하고, 둘이 갈라지면
        /// 규칙과 몸이 다른 말을 하게 된다. <c>IslandZoneTests</c>가 대조한다.
        /// </summary>
        public const float WadeDepth = 0.35f;

        /// <summary>몸이 뜨기 시작하는 깊이(m). <c>PlayerSwimming.swimDepth</c>의 기본값.</summary>
        public const float SwimDepth = 1.15f;

        /// <summary>맨몸 수영 속도(m/s). <c>PlayerLocomotion.swimSpeed</c>의 기본값.</summary>
        public const float SwimSpeed = 3.2f;

        /// <summary>이 깊이에서 몸이 어디까지 잠기는가.</summary>
        public static SeaImmersion ImmersionAt(float depth)
        {
            if (depth >= SwimDepth) return SeaImmersion.Swimming;
            if (depth >= WadeDepth) return SeaImmersion.Wading;
            return SeaImmersion.Dry;
        }

        /// <summary>
        /// 바닥을 딛고 서 있는가. 몸이 뜨기 전까지는 딛고 있다.
        ///
        /// <see cref="MacroniumSea.Corrodes"/>가 무해를 내주는 유일한 조건이 이것이다 —
        /// 얕은 바다가 무해한 이유가 여기 하나로 모인다.
        /// </summary>
        public static bool HasFooting(float depth) => depth < SwimDepth;

        /// <summary>이 폭을 헤엄쳐 건너는 데 걸리는 시간(초).</summary>
        public static float CrossingSeconds(LiquidBody body)
        {
            if (SwimSpeed <= 0f || body.Width <= 0f) return 0f;
            return body.Width / SwimSpeed;
        }

        /// <summary>
        /// 맨몸으로 건널 때 치르는 값(체력). <b>위험도를 수로 매긴 것이 이것이다.</b>
        /// 무해한 자리에서는 0이다.
        /// </summary>
        public static float Toll(LiquidBody body) =>
            MacroniumSea.DamageOver(ImmersionAt(body.Depth), HasFooting(body.Depth),
                                    CrossingSeconds(body));

        /// <summary>
        /// 이 체력으로는 <b>이 폭부터 헤엄쳐 건널 수 없다</b>(m).
        ///
        /// 바다가 죽이는 것은 바다라서가 아니라 이 수를 넘겼기 때문이다.
        /// 그래서 "얼마나 넓어야 바다인가"에 답이 있고, §16이 진짜 지형을 놓을 때
        /// 지켜야 하는 하한도 이것이다.
        /// </summary>
        public static float LethalWidth(float health)
        {
            if (MacroniumSea.CorrosionPerSecond <= 0f) return float.PositiveInfinity;
            return health / MacroniumSea.CorrosionPerSecond * SwimSpeed;
        }

        /// <summary>
        /// 이 액체 앞에서 지금 갖춘 것으로 무슨 일이 벌어지는가.
        ///
        /// 액면 보행 장비가 폭을 감당하면 잠기지 않으므로 값이 없다. 그 밖에는
        /// <see cref="Toll"/> 하나가 셋을 가른다.
        /// </summary>
        public static CrossingVerdict Judge(LiquidBody body, IReadOnlyList<GearCapability> loadout,
                                            float health)
        {
            var 관문 = new HazardZone(EnvironmentHazard.MacroniumSurface, body.Width);
            if (EnvironmentThreat.CanPass(관문, loadout)) return CrossingVerdict.Supported;

            float toll = Toll(body);
            if (toll <= 0f) return CrossingVerdict.Harmless;
            return toll >= health ? CrossingVerdict.Fatal : CrossingVerdict.Costly;
        }
    }
}
