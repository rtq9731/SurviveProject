namespace Survive.World
{
    /// <summary>
    /// 액체에 몸이 얼마나 잠겼는가. <c>PlayerSwimming.State</c>를 Domain 쪽에 옮겨 적은 것이다.
    ///
    /// 그대로 쓰지 못하는 이유는 하나뿐이다 - 그 열거형은 MonoBehaviour 안에 있고,
    /// Domain 어셈블리는 런타임 어셈블리를 보지 못한다. 값과 뜻은 일부러 같게 두었으니
    /// 한쪽이 늘면 다른 쪽도 늘어야 한다.
    ///
    /// <b>종류를 담지 않는다.</b> 잠기는 방식은 물이든 매크로늄이든 같기 때문이다
    /// (<see cref="LiquidKind"/>).
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
    /// <b>매크로늄이 몸을 무는 값.</b> 이 파일에는 수만 있고 판정은 없다 -
    /// 판정은 <see cref="LiquidCrossing"/> 하나가 하고, 그것이
    /// <see cref="Liquid.CorrosionPerSecond"/>를 거쳐 여기의 수를 집어 간다.
    ///
    /// <b>지평선을 덮은 것은 물이 아니다.</b> 70%가 물이지만 나머지 30%가 MARSO가
    /// 구성한 물질이라 마실 수 없고, 담그면 살이 깎인다(세계관 §3). <b>층이 둘로
    /// 나뉜 것이 아니라 하나의 농도 구배</b>이므로 깊이가 곧 농도이고 농도가 곧
    /// 위험도다. 표면 가까이는 옅어서 잠긴 동안 조금씩 깎고, 짙은 구간은 닿는
    /// 즉시 죽인다(<see cref="MacroniumContact"/>). 규칙을 나란히 두는 이유는,
    /// 둘이 따로 놀면 "어떤 매크로늄은 아프고 어떤 매크로늄은 안 아프다"가 되어
    /// 세계가 일관성을 잃기 때문이다.
    ///
    /// <b>호수는 여기에 오지 않는다.</b> 진짜 물만 내려 고인 자리는 초당 0이고
    /// (<see cref="LiquidKind.Water"/>), 그것도 <b>같은 판정</b>이 내는 답이다.
    /// 판정을 건너뛰는 것이 아니라 표에서 0을 집어 오는 것이다.
    ///
    /// <b>깎는 것이 관문을 대신하지는 않는다.</b> 스펙 §4는 위협을 "막는 것"으로 두고
    /// 체력을 닳게 하는 방식을 배제한다. 이 값은 그 원칙을 뒤집는 것이 아니라
    /// 건너편으로 가는 <i>수영</i>에 값을 매기는 것이다 - 수영 자체는 여전히 통과하고,
    /// 다만 공짜가 아니게 된다. 관문(액면·폭·어둠)은 그대로 막는 쪽에 남는다.
    ///
    /// <b>무해한 자리를 남겨 두는 이유.</b> 물가에서 발목을 담근 채 캐는 일은
    /// 잡무가 되면 안 된다. 그래서 <b>바닥을 딛고 있는 얕은 물</b>은 값을 매기지 않는다.
    /// 그 판정은 <see cref="LiquidCrossing.IsExposed"/>에 있다.
    /// </summary>
    public static class MacroniumSea
    {
        /// <summary>
        /// 매크로늄에 잠겨 있는 동안 초당 깎이는 체력.
        ///
        /// <b>실측으로 역산한 값이다.</b> 임시 지형의 물가 (40,-2)에서 (82,12)까지
        /// 41.2m를 실제로 키를 눌러 건너며 "떠 있는 시간"을 쟀다:
        /// <list type="bullet">
        /// <item>맨몸 보통 수영 - 10.3초 (Space를 눌러 수면으로 건너면 11.9초)</item>
        /// <item>Shift 수영 - 7.1초 (Space 병행 8.4초)</item>
        /// </list>
        /// 초당 3이면 보통 횡단이 체력의 31%, Shift 횡단이 21%다. 요구한 25~35% 구간
        /// 한가운데에 들어가고, 가속이 3분의 1을 아껴 주므로 Shift가 실제로 생존기가 된다.
        /// 체력 100으로 왕복하면 절반이 넘게 사라진다 - 한 번은 건너지만 빈손으로
        /// 돌아올 여유는 없다.
        /// </summary>
        public const float CorrosionPerSecond = 3f;

        /// <summary>
        /// 매크로늄에 한 번 물릴 때의 피해.
        ///
        /// 박자는 종류가 정하지 않으므로 <see cref="LiquidCrossing.BiteInterval"/>에서
        /// 가져온다 - 이 파일에 남는 것은 <b>매크로늄의 수</b>뿐이다.
        /// </summary>
        public const float BiteDamage = CorrosionPerSecond * LiquidCrossing.BiteInterval;
    }
}
