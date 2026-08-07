using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>
    /// 액체 한 자락. <b>종류와 깊이와 폭 셋으로만 적힌다.</b>
    ///
    /// 호수와 강과 얕은 바다와 바다가 서로 다른 것은 이 세 수뿐이다. 규칙은 하나이고,
    /// 그 하나가 세 수를 받아 답을 낸다 - 기획서 §2.1 "세 특례가 아니라 한 규칙의
    /// 세 값이다". 2026-08-07에 <b>종류</b>가 축으로 더해졌다: 진짜 물만 내려 고인
    /// 호수는 무해하고, 매크로늄 바다는 담그면 깎인다(세계관 §2·§3).
    /// </summary>
    public readonly struct LiquidBody
    {
        /// <summary>무엇으로 된 액체인가. 이것이 부식을 정한다.</summary>
        public readonly LiquidKind Kind;

        /// <summary>바닥까지의 깊이(m). 발을 딛는가 뜨는가를 이것이 정한다.</summary>
        public readonly float Depth;

        /// <summary>가로질러야 하는 폭(m). 잠겨 있는 시간이 여기서 나온다.</summary>
        public readonly float Width;

        /// <summary>
        /// <b>종류 없이는 만들 수 없다.</b> 인자를 하나 덜 받는 생성자를 두면
        /// 그것이 곧 조용한 기본값이 되고, 「모르는 액체」가 씬을 지나 판정까지 온다.
        /// </summary>
        public LiquidBody(LiquidKind kind, float depth, float width)
        {
            Kind = Liquid.Require(kind);
            Depth = depth;
            Width = width;
        }

        /// <summary>폭만 바꾼 같은 액체. "폭이 답을 바꾸는가"를 묻는 검사에 쓴다.</summary>
        public LiquidBody Widened(float width) => new LiquidBody(Kind, Depth, width);

        /// <summary>
        /// 종류만 바꾼 같은 액체. <b>"같은 자리에서 종류만 갈면 답이 갈리는가"</b>를
        /// 묻는 데 쓴다 - 특례가 아니라 한 규칙의 두 값임을 재는 자가 이것이다.
        /// </summary>
        public LiquidBody OfKind(LiquidKind kind) => new LiquidBody(kind, Depth, Width);
    }

    /// <summary>액체 한 자락 앞에 선 사람에게 일어나는 일.</summary>
    public enum CrossingVerdict
    {
        /// <summary>아무 일도 없다. 걸어서든 헤엄쳐서든 값 없이 건넌다.</summary>
        Harmless,

        /// <summary>건너지만 값을 치른다. 잠긴 시간만큼 살이 깎인다.</summary>
        Costly,

        /// <summary>맨몸으로는 건너다 죽는다. 헤엄쳐서 닿을 수 있는 거리가 아니다.</summary>
        Fatal,

        /// <summary>액면 보행 장비가 받쳐 준다. 잠기지 않으므로 값이 없다.</summary>
        Supported,
    }

    /// <summary>
    /// <b>액체 앞에서 무슨 일이 벌어지는가 - 판정은 하나다.</b>
    ///
    /// 액체는 여러 얼굴로 나온다: 호수는 무해하고, 강은 아프고, 얕은 물은 무해하고,
    /// 바다는 죽인다. <b>그 넷은 네 개의 특례가 아니라 한 규칙의 네 값이다</b>
    /// (기획서 §2.1). 여기서 하는 계산은 하나뿐이다:
    ///
    /// <list type="number">
    /// <item>깊이가 상태를 정한다 - 발을 딛는가(<see cref="SeaImmersion.Wading"/>),
    ///       뜨는가(<see cref="SeaImmersion.Swimming"/>)</item>
    /// <item>폭이 시간을 정한다 - 헤엄쳐 건너는 데 걸리는 초</item>
    /// <item><b>종류가 초당 얼마인지를 정한다</b>(<see cref="Liquid.CorrosionPerSecond"/>)</item>
    /// <item>셋을 곱하면 값이 나온다</item>
    /// </list>
    ///
    /// 값이 0이면 무해하고, 체력보다 작으면 아프고, 체력 이상이면 죽는다.
    /// <b>분기는 그 하나뿐이고 구역 이름도 오브젝트 이름도 어디에도 나오지 않는다</b> -
    /// 강이라서 아픈 것이 아니라 깊고 넓고 매크로늄이라서 아픈 것이고,
    /// 얕은 자리가 무해한 것은 예외를 받아서가 아니라 발이 바닥에 닿아서다.
    ///
    /// <b>호수는 판정에서 빠지지 않는다.</b> 같은 판정에 들어가서 <b>다른 답</b>을 낸다.
    /// 빼 두었다면 "호수에서는 무슨 일이 일어나는가"에 답할 곳이 없어지고, 폭이나
    /// 깊이가 바뀔 때마다 누군가 특례를 하나씩 더 적게 된다.
    ///
    /// <b>왜 순서가 단조 증가가 아닌가.</b> 아프다(강) → 무해(얕은 물) → 죽음(바다).
    /// 계속 세지기만 하면 플레이어가 배우는 것은 "다음 물은 더 아프겠지" 하나뿐이고
    /// 그것은 규칙이 아니라 경사다. 한 번 풀어 주면 <b>"이 물이 어떤 물인지 봐야 한다"</b>를
    /// 배우게 된다. 종류 축이 그 수업을 한 겹 더 두껍게 만든다 - 이제 봐야 하는 것이
    /// 깊이만이 아니라 색이기도 하다(청록인가 자홍인가).
    /// 이 역전은 설계이므로 <c>SurfaceZoneTests</c>가 못 박아 둔다.
    /// </summary>
    public static class LiquidCrossing
    {
        /// <summary>
        /// 발이 잠기기 시작하는 깊이(m). <c>PlayerSwimming.wadeDepth</c>의 기본값과 같다 -
        /// 판정을 Domain에서 하려면 그 수가 여기에도 있어야 하고, 둘이 갈라지면
        /// 규칙과 몸이 다른 말을 하게 된다. <c>SurfaceZoneTests</c>가 대조한다.
        /// </summary>
        public const float WadeDepth = 0.35f;

        /// <summary>몸이 뜨기 시작하는 깊이(m). <c>PlayerSwimming.swimDepth</c>의 기본값.</summary>
        public const float SwimDepth = 1.15f;

        /// <summary>맨몸 수영 속도(m/s). <c>PlayerLocomotion.swimSpeed</c>의 기본값.</summary>
        public const float SwimSpeed = 3.2f;

        /// <summary>
        /// 한 번에 물어뜯는 간격(초). <b>종류를 묻지 않는다</b> - 연출 박자이지
        /// 매크로늄의 성질이 아니다.
        ///
        /// 매 프레임 조금씩 넣으면 피격 연출이 초당 수백 번 터진다. 초에 한 번
        /// 뭉쳐서 넣으면 화면도 견디고, "잠겨 있는 동안 계속 물린다"는 감각도 남는다.
        /// 호수에서도 이 박자는 돌지만 초당 값이 0이라 아무 일도 일어나지 않는다.
        ///
        /// <b>왜 여기로 옮겼나 (2026-08-07).</b> <see cref="MacroniumSea"/>에 있었다.
        /// 종류가 하나뿐일 때는 티가 안 났지만, 물이 축으로 들어오고 나서는
        /// <b>물에 잠긴 몸의 박자를 매크로늄 파일에서 읽어 오는</b> 모양이 됐다.
        /// 종류를 묻지 않는 수는 종류를 묻지 않는 자리에 있어야 한다 -
        /// <see cref="WadeDepth"/>·<see cref="SwimSpeed"/>와 같은 이유다.
        /// </summary>
        public const float BiteInterval = 1f;

        /// <summary>이 깊이에서 몸이 어디까지 잠기는가. <b>종류를 묻지 않는다.</b></summary>
        public static SeaImmersion ImmersionAt(float depth)
        {
            if (depth >= SwimDepth) return SeaImmersion.Swimming;
            if (depth >= WadeDepth) return SeaImmersion.Wading;
            return SeaImmersion.Dry;
        }

        /// <summary>
        /// 바닥을 딛고 서 있는가. 몸이 뜨기 전까지는 딛고 있다. <b>종류를 묻지 않는다.</b>
        ///
        /// <see cref="IsExposed"/>가 무해를 내주는 유일한 조건이 이것이다 -
        /// 얕은 자리가 무해한 이유가 여기 하나로 모인다.
        /// </summary>
        public static bool HasFooting(float depth) => depth < SwimDepth;

        /// <summary>
        /// <b>몸이 액체에 노출되어 있는가.</b> 종류를 묻지 않는다 - 물이든 매크로늄이든
        /// 잠기는 방식은 같고, 갈리는 것은 잠긴 다음이다.
        ///
        /// 헤엄치는 동안은 언제나 노출이다 - 바닥을 딛고 있어도 몸이 잠겼으면 마찬가지다.
        /// 발만 잠긴 상태는 <b>딛고 있을 때만</b> 아니다. 딛지 못한 채 떠 있다면
        /// 얕게 읽히더라도 그것은 물가가 아니라 한복판이다.
        ///
        /// <b>왜 상태 하나로 가르지 않는가.</b> 수면에 뜬 채 앞으로 나아가는 동안 몸은
        /// 부력과 중력 사이에서 오르내리고, 그 진동의 대부분은 잠긴 깊이가 얕아
        /// <see cref="SeaImmersion.Wading"/>으로 읽힌다(실측: 수면 횡단 3972프레임 중
        /// 여울 3574 · 헤엄 23). 상태만 보면 한복판을 떠서 건너는 사람이 공짜가 된다.
        /// </summary>
        public static bool IsExposed(SeaImmersion immersion, bool footing)
        {
            if (immersion == SeaImmersion.Swimming) return true;
            if (immersion == SeaImmersion.Wading) return !footing;
            return false;
        }

        /// <summary>
        /// 이 종류의 액체에 이렇게 잠겨 있을 때 <b>초당 깎이는 체력</b>.
        ///
        /// <b>여기가 두 값이 갈리는 유일한 자리다.</b> 노출 여부는 위에서 이미 정해졌고,
        /// 남은 것은 종류에게 「초당 얼마인가」를 묻는 것뿐이다. 물은 0을 돌려주고
        /// 매크로늄은 자기 값을 돌려준다. 갈래가 아니라 표 조회다.
        /// </summary>
        public static float DamagePerSecond(LiquidKind kind, SeaImmersion immersion, bool footing) =>
            IsExposed(immersion, footing) ? Liquid.CorrosionPerSecond(kind) : 0f;

        /// <summary>그 상태로 <paramref name="seconds"/>초를 버텼을 때의 피해 총량.</summary>
        public static float DamageOver(LiquidKind kind, SeaImmersion immersion, bool footing,
                                       float seconds)
        {
            if (seconds <= 0f) return 0f;
            return DamagePerSecond(kind, immersion, footing) * seconds;
        }

        /// <summary>이 폭을 헤엄쳐 건너는 데 걸리는 시간(초). <b>종류를 묻지 않는다.</b></summary>
        public static float CrossingSeconds(LiquidBody body)
        {
            if (SwimSpeed <= 0f || body.Width <= 0f) return 0f;
            return body.Width / SwimSpeed;
        }

        /// <summary>
        /// 맨몸으로 건널 때 치르는 값(체력). <b>위험도를 수로 매긴 것이 이것이다.</b>
        /// 무해한 자리에서는 0이고, <b>호수는 아무리 깊고 넓어도 0이다.</b>
        /// </summary>
        public static float Toll(LiquidBody body) =>
            DamageOver(body.Kind, ImmersionAt(body.Depth), HasFooting(body.Depth),
                       CrossingSeconds(body));

        /// <summary>
        /// 이 체력으로는 <b>이 폭부터 헤엄쳐 건널 수 없다</b>(m).
        ///
        /// 바다가 죽이는 것은 바다라서가 아니라 이 수를 넘겼기 때문이다.
        /// <b>호수에는 그런 폭이 없다</b> - 초당 0이므로 답이 무한대이고, 그것이
        /// "호수는 넓어도 건널 수 있다"의 수학적인 모습이다.
        /// </summary>
        public static float LethalWidth(LiquidKind kind, float health)
        {
            float perSecond = Liquid.CorrosionPerSecond(kind);
            if (perSecond <= 0f) return float.PositiveInfinity;
            return health / perSecond * SwimSpeed;
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
