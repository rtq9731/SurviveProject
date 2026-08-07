using Survive.World;

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
    /// 기획서 §4.5의 경계 상태는 셋이다 — <b>평시 1마리 · 각성 2마리 · 발령 5마리</b>.
    ///
    /// <b>오래 값이 둘이었다.</b> 각성이 개체수만 바꾸고 서식 범위는 바꾸지 않으므로,
    /// 서식 범위만 묻던 시절에는 갈리는 것이 발령이냐 아니냐뿐이었기 때문이다.
    /// 2026-08-07에 개체수(<see cref="ScytheCensus"/>)가 서면서 <b>각성이 값을
    /// 하기 시작</b>했고, 그래서 셋이 됐다.
    ///
    /// <b>서식 범위 쪽 규칙은 한 글자도 바뀌지 않았다.</b> 아래 규칙들이 전부
    /// <c>== Alarmed</c>만 묻기 때문이다 — 각성은 평시와 같은 곳까지 간다.
    /// 그것이 "각성이면 어디까지?"라는 답할 수 없는 물음을 만들지 않는 방법이다.
    /// </summary>
    public enum ScytheAlert
    {
        /// <summary>평시. 은폐 프로토콜이 살아 있어 육지로 올라오지 않는다. <b>1마리.</b></summary>
        Calm,

        /// <summary>
        /// 각성. <b>수만 늘고 행동은 평시와 같다</b> — 육지로 올라오지 않는다. <b>2마리.</b>
        /// 방아쇠는 특정 구역 도달이고, 어느 구역인지는 아직 미결이다
        /// (<see cref="Survive.World.ZoneArrival"/>).
        /// </summary>
        Awake,

        /// <summary>발령. 은폐를 포기하고 육지로 올라온다. <b>5마리.</b></summary>
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
    /// <item>안쪽 뭍(<c>SurfaceZone.Inland</c>)이 왜 안전한가 — 낫이 상시로는 오지 않는다</item>
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
        /// 1.5를 썼는데, 걸을 수 있는 지형을 334곳 재어 보니 <b>액면 50.1 위로
        /// 최고가 52.32, 중앙값이 51.55</b>였다. 1.5m 띠는 그 섬의 절반을 해안선으로
        /// 삼켜, "안쪽 뭍이 안전하다"가 지도 위에서 성립하지 않는다.
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

        // ══ 시간 축 ════════════════════════════════════════════
        // 이 파일은 오래 <b>자리</b>와 <b>태세</b> 두 축만 알고 있었다. 세 번째 축이
        // 여기 붙는다 — <b>시각</b>. 근거는 세계관 §5다: "일반 생물들에게 방해받지
        // 않기 위해 눈에 띄지 않게 다닌다. 밤에는 더 보이지 않으므로 밤에 움직인다."
        // <b>적의가 아니라 은폐다.</b>

        /// <summary>
        /// 지금 낫이 <b>나와 있는가</b>.
        ///
        /// <b>"없어지는가"가 아니라 "어디까지 나오는가"다.</b> 낮에 개체를 지웠다가
        /// 밤에 다시 만들면 그것은 자리를 옮긴 것이 아니라 <b>다른 물건</b>이 된다 —
        /// 도감과 관측(§5.13)이 「본 적 있다」를 세는데 그 셈이 흔들리고, 유물을
        /// 흘리던 개체의 굴림 시계도 매일 처음부터 시작한다. 그래서 낮에도 낫은
        /// 세계에 남아 있고, <b>물러날 뿐</b>이다(<see cref="CanEnter(HabitatZone, ScytheAlert, bool)"/>).
        ///
        /// <b>경계는 하나의 구간으로 잡는다.</b> 해질녘이 시작되는 순간부터 해뜰녘이
        /// 시작되는 순간까지가 활동 시간이다 — 곧 <b>빛이 기울기 시작하면 나오고,
        /// 빛이 돌아오기 시작하면 물러난다.</b> 박명 둘을 각각 어느 쪽으로 붙일지
        /// 따로 정하지 않고 <b>한 구간</b>으로 두는 것이 요점이다. 둘을 따로 정하면
        /// 경계가 넷이 되고, 그 넷 중 하나에서 값이 튀면 낫이 깜빡인다.
        ///
        /// 감광도(<see cref="DayNightCycle.Daylight"/>)에 문턱을 걸지 않은 것도 같은
        /// 이유다. 그것은 부드러운 곡선이라 문턱 근처에서 프레임마다 답이 뒤집힌다.
        /// 시각은 단조롭게 흐르므로 구간 판정이 흔들리지 않는다.
        /// </summary>
        public static bool IsAbroad(float timeOfDay) =>
            timeOfDay >= DayNightCycle.DuskStart || timeOfDay < DayNightCycle.DawnStart;

        /// <summary>
        /// 태세까지 본 활동 여부. <b>발령은 낮을 이긴다.</b>
        ///
        /// 발령은 은폐 프로토콜을 <b>버렸다</b>는 뜻이다(<see cref="ScytheAlert.Alarmed"/>).
        /// 숨을 이유가 사라진 개체가 해가 떴다고 물러나면 그 말이 거짓이 된다.
        /// 그리고 등급은 <b>월드가 건 것</b>이라, 개체가 읽는 시계가 그것을 뚫으면
        /// 소유권을 월드에 둔 뜻이 없어진다 — 고정 조명이 발령을 풀지 못하는 것과
        /// 같은 원리다(<see cref="ScytheFsm"/>).
        /// </summary>
        public static bool IsAbroad(float timeOfDay, ScytheAlert alert) =>
            alert == ScytheAlert.Alarmed || IsAbroad(timeOfDay);

        /// <summary>
        /// 시간까지 본 서식 범위. <b>낮에는 액면 위로 물러난다.</b>
        ///
        /// 낮의 범위를 <see cref="HabitatZone.Liquid"/> 하나로 좁히는 것이
        /// "물러난다"의 내용이다. 해안선을 잃으므로 물가에 선 사람에게 닿지 않고,
        /// 유물도 물 위에만 떨어져 <b>액면 보행 장비 없이는 주울 수 없다</b> —
        /// 낮과 밤이 갈리는 자리가 전부 이 한 줄에서 나온다.
        /// </summary>
        public static bool CanEnter(HabitatZone zone, ScytheAlert alert, bool abroad) =>
            abroad ? CanEnter(zone, alert) : zone == HabitatZone.Liquid;

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
