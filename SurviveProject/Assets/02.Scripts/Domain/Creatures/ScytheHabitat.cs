namespace Survive.Creatures
{
    /// <summary>이 자리가 낫에게 무엇인가.</summary>
    public enum HabitatZone
    {
        /// <summary>액면 위. 받칠 지형이 액체보다 아래에 있거나 아예 없다.</summary>
        Liquid,

        /// <summary>해안선. 지형이 액면 위로 올라오되 <see cref="ScytheHabitat.ShoreRise"/>까지다.</summary>
        Shore,

        /// <summary>육지. 액면에서 그만큼 넘게 올라온 자리.</summary>
        Inland,
    }

    /// <summary>
    /// 낫의 경계 태세. <b>서식 범위가 무엇을 묻는지에 대한 답은 이것 하나뿐이다</b> —
    /// 육지로 올라오는가.
    ///
    /// 기획서 §4.5의 경계 상태는 셋(평시 1마리 · 각성 2마리 · 발령 5마리)인데
    /// 여기 값이 둘인 것은 <b>각성이 개체수만 바꾸고 서식 범위는 바꾸지 않기</b>
    /// 때문이다. 서식 범위 쪽에서 갈리는 것은 발령이냐 아니냐뿐이라, 셋을 여기까지
    /// 끌고 오면 이 규칙이 답할 수 없는 물음("각성이면 어디까지?")을 스스로 만든다.
    ///
    /// <b>지금은 아무도 이 값을 올리지 않는다.</b> 무엇이 발령을 거는가는 스펙 §5의
    /// 몫이고, 여기서는 그 자리만 열어 둔다.
    /// </summary>
    public enum ScytheAlert
    {
        /// <summary>평시. 은폐 프로토콜이 살아 있어 육지로 올라오지 않는다.</summary>
        Calm,

        /// <summary>발령. 은폐를 포기하고 육지로 올라온다.</summary>
        Alarmed,
    }

    /// <summary>
    /// 낫이 어디에 있을 수 있는가 (기획서 §4.5 "서식 범위", 스펙 §3).
    ///
    /// <b>낫은 포식자가 아니라 MARSO의 유지보수 유닛이다.</b> 다리가 없는 부유체라
    /// 액체 위에서 움직이고, 상시 활동 범위는 <b>바다 전역과 양쪽 섬의 해안선까지</b>다.
    ///
    /// <b>육지에 못 올라가는 것이 아니다.</b> 호버링이므로 물리적으로는 언제든
    /// 가능하다. 평소 올라오지 않는 것은 능력이 아니라 <b>은폐 프로토콜</b> 때문이고
    /// (빛은 위에서 뚫린 구멍이라 금방 메워지므로 어디서도 보이지 않게 만들어 두었다),
    /// 그 프로토콜을 포기하는 순간이 <see cref="ScytheAlert.Alarmed"/>다. 이 구분이
    /// 값을 하는 자리가 종막이다 — 평생 육지에 오지 않던 것이 올라온다는 사실 자체가
    /// 개체수가 다섯으로 느는 것보다 큰 압력이 된다.
    ///
    /// <b>이 규정 하나가 셋을 동시에 푼다.</b>
    /// <list type="bullet">
    /// <item>A섬 내륙과 B섬 지하가 왜 안전한가 — 낫이 상시로는 오지 않는다</item>
    /// <item>액면 보행 장비 없이 티어 2 유물을 어떻게 먼저 얻는가 —
    ///       해안선까지는 나오므로 가장자리에서 줍는다 (순환이 풀린다)</item>
    /// <item>3단 목격을 어디에 두는가 — 해안에서 바다 너머로 →
    ///       해안까지 나온 개체를 가까이 → 액면 위를 걸을 때 정면</item>
    /// </list>
    ///
    /// <b>왜 Domain에 있는가.</b> "해안선이 어디까지인가"는 씬을 띄우지 않고 답할 수
    /// 있어야 하는 물음이다. 재는 일(지형 높이·액면 높이)은 몸이 하고, 잰 값으로
    /// 무엇인지 판정하는 일은 전부 여기에 있다.
    /// </summary>
    public static class ScytheHabitat
    {
        /// <summary>
        /// 액면에서 이만큼까지 올라온 지형은 <b>해안선</b>이다(m).
        ///
        /// <b>왜 높이로 긋는가.</b> "물가에서 몇 미터"로 그으면 완만한 갯벌과 깎아지른
        /// 벼랑이 같은 폭을 갖는다. 액면에 붙어 사는 것이 올라올 수 있는 범위는
        /// 수평 거리가 아니라 <b>얼마나 솟았는가</b>로 정해지는 것이 물성에 맞고,
        /// 지도를 고쳐도 규칙이 따라 움직이지 않는다.
        ///
        /// <b>왜 0.75인가 — 실측으로 정했다.</b> 처음에는 사람의 무릎 높이라는 이유로
        /// 1.5를 썼는데, A섬의 걸을 수 있는 지형을 334곳 재어 보니 <b>액면 50.1 위로
        /// 최고가 52.32, 중앙값이 51.55</b>였다. 1.5m 띠는 그 섬의 절반을 해안선으로
        /// 삼켜, "A섬 내륙이 안전하다"가 지도 위에서 성립하지 않는다.
        ///
        /// 0.75는 <b>한 걸음 턱</b>이다. 물가에 선 사람이 팔을 뻗어 유물에 닿을 만큼은
        /// 나오면서(순환 해소의 조건), 물가를 한 발 벗어나면 곧바로 내륙이 된다.
        /// 섬 메시는 사람이 다시 만들므로(스펙 §16) 그때 이 값을 다시 재야 한다 —
        /// <b>이 상수는 지형과 짝이다.</b>
        /// </summary>
        public const float ShoreRise = 0.75f;

        /// <summary>
        /// 잰 값으로 이 자리가 무엇인지 판정한다.
        /// </summary>
        /// <param name="hasLiquid">이 수평 자리에 액체가 있는가.</param>
        /// <param name="liquidSurfaceY">그 액체의 수면 높이.</param>
        /// <param name="hasGround">받칠 지형을 찾았는가. 못 찾았으면 아래는 액체뿐이다.</param>
        /// <param name="groundY">찾은 지형의 윗면 높이.</param>
        public static HabitatZone Classify(bool hasLiquid, float liquidSurfaceY,
                                           bool hasGround, float groundY)
        {
            // 액체가 없는 자리는 전부 육지다. 부유체가 무엇에도 얹히지 못하는 허공을
            // "액면 위"로 세면 섬 바깥 허공이 통째로 서식지가 된다.
            if (!hasLiquid) return HabitatZone.Inland;

            // 받칠 것이 없으면 아래는 액체뿐이다 — 바다 한가운데.
            if (!hasGround) return HabitatZone.Liquid;

            // 지형이 수면에 잠겨 있으면 그 위는 여전히 액면이다 (여울·모래톱).
            if (groundY <= liquidSurfaceY) return HabitatZone.Liquid;

            if (groundY <= liquidSurfaceY + ShoreRise) return HabitatZone.Shore;

            return HabitatZone.Inland;
        }

        /// <summary>
        /// 그 자리에 들어갈 수 있는가. <b>육지만 태세를 묻고, 나머지는 언제나 열려 있다.</b>
        /// </summary>
        public static bool CanEnter(HabitatZone zone, ScytheAlert alert) =>
            zone != HabitatZone.Inland || alert == ScytheAlert.Alarmed;

        /// <summary>재는 것과 판정을 한 번에. 몸이 부르는 창구다.</summary>
        public static bool CanOccupy(bool hasLiquid, float liquidSurfaceY,
                                     bool hasGround, float groundY, ScytheAlert alert) =>
            CanEnter(Classify(hasLiquid, liquidSurfaceY, hasGround, groundY), alert);

        /// <summary>
        /// 그 자리에서 떠 있어야 할 높이.
        ///
        /// <b>기준면이 비행과 다르다.</b> 나는 것은 지면 위 고도를 지키지만 이쪽은
        /// 액면에 붙는다 — 꼬리가 액체를 훑어야 하기 때문이다(§4.5 "꼬리가 매크로늄을
        /// 훑는다"). 그래서 물 위에서는 수면이 기준이고, 지형이 수면 위로 올라온
        /// 해안선에서만 그 지형을 기준으로 삼는다. 둘 중 <b>높은 쪽</b>을 쓰는 것이
        /// 곧 그 규칙이다.
        /// </summary>
        public static float FloatHeight(bool hasLiquid, float liquidSurfaceY,
                                        bool hasGround, float groundY, float clearance)
        {
            float baseY = hasLiquid ? liquidSurfaceY : groundY;
            if (hasGround && groundY > baseY) baseY = groundY;
            return baseY + clearance;
        }
    }
}
