namespace Survive.World
{
    /// <summary>
    /// 바다에 몸이 얼마나 잠겼는가. <c>PlayerSwimming.State</c>를 Domain 쪽에 옮겨 적은 것이다.
    ///
    /// 그대로 쓰지 못하는 이유는 하나뿐이다 — 그 열거형은 MonoBehaviour 안에 있고,
    /// Domain 어셈블리는 런타임 어셈블리를 보지 못한다. 값과 뜻은 일부러 같게 두었으니
    /// 한쪽이 늘면 다른 쪽도 늘어야 한다.
    /// </summary>
    public enum SeaImmersion
    {
        /// <summary>물 밖.</summary>
        Dry,

        /// <summary>발은 잠겼지만 몸은 아니다.</summary>
        Wading,

        /// <summary>몸이 잠겨 헤엄친다.</summary>
        Swimming,
    }

    /// <summary>
    /// 섬 사이의 바다는 물이 아니다 — <b>묽은 매크로늄</b>이고, 몸을 담그면 살을 문다.
    ///
    /// <b>3→4 액면과 같은 물질이다.</b> 농도만 다르다. 짙은 층은 닿는 즉시 죽이고
    /// (<see cref="MacroniumContact"/>), 묽은 바다는 잠겨 있는 동안 조금씩 깎는다.
    /// 그래서 이름도 규칙도 나란히 둔다 — 둘이 따로 놀면 "어떤 매크로늄은 아프고
    /// 어떤 매크로늄은 안 아프다"가 되어 세계가 일관성을 잃는다.
    ///
    /// <b>깎는 것이 관문을 대신하지는 않는다.</b> 스펙 §4는 위협을 "막는 것"으로 두고
    /// 체력을 닳게 하는 방식을 배제한다. 이 규칙은 그 원칙을 뒤집는 것이 아니라
    /// 2번 섬으로 가는 <i>수영</i>에 값을 매기는 것이다 — 수영 자체는 여전히 통과하고,
    /// 다만 공짜가 아니게 된다. 관문(액면·폭·어둠)은 그대로 막는 쪽에 남는다.
    ///
    /// <b>무해한 자리를 남겨 두는 이유.</b> 2번 섬 해안에서 발목을 담근 채 캐는 일은
    /// 잡무가 되면 안 된다. 그래서 <b>바닥을 딛고 있는 얕은 물</b>은 값을 매기지 않는다.
    ///
    /// <b>왜 상태 하나로 가르지 않는가.</b> 수면에 뜬 채 앞으로 나아가는 동안 몸은
    /// 부력과 중력 사이에서 오르내리고, 그 진동의 대부분은 잠긴 깊이가 얕아
    /// <see cref="SeaImmersion.Wading"/>으로 읽힌다(실측: 수면 횡단 3972프레임 중
    /// 여울 3574 · 헤엄 23). 상태만 보면 바다 한가운데를 떠서 건너는 사람이 공짜가 된다.
    /// 그래서 <b>발을 딛고 있는가</b>를 함께 본다 — 해안에서 바닥을 밟고 선 사람과
    /// 심연 위에 떠 있는 사람을 가르는 것은 잠긴 깊이가 아니라 그것이다.
    /// </summary>
    public static class MacroniumSea
    {
        /// <summary>
        /// 잠겨 있는 동안 초당 깎이는 체력.
        ///
        /// <b>실측으로 역산한 값이다.</b> 1번 섬 물가 (40,-2)에서 2번 섬 물가 (82,12)까지
        /// 41.2m를 실제로 키를 눌러 건너며 "떠 있는 시간"을 쟀다:
        /// <list type="bullet">
        /// <item>맨몸 보통 수영 — 10.3초 (Space를 눌러 수면으로 건너면 11.9초)</item>
        /// <item>Shift 수영 — 7.1초 (Space 병행 8.4초)</item>
        /// </list>
        /// 초당 3이면 보통 횡단이 체력의 31%, Shift 횡단이 21%다. 요구한 25~35% 구간
        /// 한가운데에 들어가고, 가속이 3분의 1을 아껴 주므로 Shift가 실제로 생존기가 된다.
        /// 체력 100으로 왕복하면 절반이 넘게 사라진다 — 한 번은 건너지만 빈손으로
        /// 돌아올 여유는 없다.
        /// </summary>
        public const float CorrosionPerSecond = 3f;

        /// <summary>
        /// 한 번에 물어뜯는 간격(초).
        ///
        /// 매 프레임 조금씩 넣으면 피격 연출이 초당 수백 번 터진다. 초에 한 번
        /// 뭉쳐서 넣으면 화면도 견디고, "잠겨 있는 동안 계속 물린다"는 감각도 남는다.
        /// </summary>
        public const float BiteInterval = 1f;

        /// <summary>한 번 물릴 때의 피해.</summary>
        public const float BiteDamage = CorrosionPerSecond * BiteInterval;

        /// <summary>
        /// 지금 살이 깎이고 있는가.
        ///
        /// 헤엄치는 동안은 언제나 깎인다 — 바닥을 딛고 있어도 몸이 잠겼으면 마찬가지다.
        /// 발만 잠긴 상태는 <b>딛고 있을 때만</b> 무해하다. 딛지 못한 채 떠 있다면
        /// 얕게 읽히더라도 그것은 해안이 아니라 바다 위다.
        /// </summary>
        public static bool Corrodes(SeaImmersion immersion, bool footing)
        {
            if (immersion == SeaImmersion.Swimming) return true;
            if (immersion == SeaImmersion.Wading) return !footing;
            return false;
        }

        /// <summary>초당 피해. 무해한 자리에서는 0이다.</summary>
        public static float DamagePerSecond(SeaImmersion immersion, bool footing) =>
            Corrodes(immersion, footing) ? CorrosionPerSecond : 0f;

        /// <summary>그 상태로 <paramref name="seconds"/>초를 버텼을 때의 피해 총량.</summary>
        public static float DamageOver(SeaImmersion immersion, bool footing, float seconds)
        {
            if (seconds <= 0f) return 0f;
            return DamagePerSecond(immersion, footing) * seconds;
        }
    }
}
