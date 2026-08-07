namespace Survive.Creatures
{
    /// <summary>
    /// 낫의 꼬리 자세. <b>이 개체의 상태 표시등은 게이지도 알림도 아니고 꼬리 하나다</b>
    /// (기획서 §4.5 "상태 표현 — 꼬리", 스펙 §4).
    ///
    /// <b>왜 실루엣으로 말하게 하는가.</b> 이 세계의 판독 규칙은 "둥글다 = 무해 /
    /// 뾰족하다 = 위험"이다. 그 규칙을 <b>종족이 아니라 상태에</b> 걸면, 규칙을 하나도
    /// 어기지 않으면서 같은 개체에서 한 단계 더 쓸 수 있다 — 같은 몸이 넓고 낮은
    /// 실루엣과 뭉치고 뾰족한 실루엣을 번갈아 갖는다.
    ///
    /// 그리고 <b>꼬리를 거두는 동작 자체가 경고</b>다. AI 동료의 경고보다 먼저 오고,
    /// 알림창이 아니라 눈으로 온다. 그래서 이 값이 화면에 늦게 반영되면 경고가
    /// 경고이기를 그만둔다.
    ///
    /// 둥근 몸통이라 위협적으로 생기지 않은 것은 의도다. 둥근 것은 무해라는 규칙
    /// 아래에서 <b>둥근데 위험하다</b>는 사실이, 이것이 생태계의 규칙 밖 존재라는
    /// 신호가 된다.
    /// </summary>
    public enum ScythePosture
    {
        /// <summary>
        /// 작업 중. 꼬리를 늘어뜨려 액면을 훑는다. 실루엣이 <b>넓고 낮다</b>.
        /// 유지보수 유닛이 제 일을 하고 있는 평상시다.
        /// </summary>
        Trailing,

        /// <summary>
        /// 경계. 꼬리를 든다. <b>작업을 멈췄다</b>는 뜻이고, 아직 덤비지는 않았다.
        /// </summary>
        Raised,

        /// <summary>
        /// 공격 태세. 꼬리를 몸에 바짝 붙인다. 실루엣이 <b>뭉치고 뾰족하다</b>.
        /// 육지 진입과 교전이 여기다.
        /// </summary>
        Furled,

        /// <summary>
        /// 짐을 들었다. 코어를 꼬리로 물고 둥지로 가는 중이다
        /// (기획서 §4.5 "회수 연출", <see cref="ScytheState.Retrieve"/>).
        ///
        /// <b>공격 태세와 반드시 갈려야 한다.</b> 기획서가 이 연출에 건 약속은
        /// "꼬리가 무기인데 짐을 들고 있으므로 <b>공격할 수 없다는 것이 형태로
        /// 읽힌다</b>"이다. 회수 중인 개체가 <see cref="Furled"/>로 보이면 그 약속이
        /// 정확히 거꾸로 읽힌다 — 가장 위험해 보이는 실루엣이 실은 때리지 못하는
        /// 상태가 된다.
        /// </summary>
        Laden,
    }

    /// <summary>
    /// 꼬리와 함께 움직이는 빛의 세기. <b>광원은 쓰지 않는다 — 전부 에미션이다</b>
    /// (기획서 §4.5 "아트"). 환경광이 0인 세계라 광원을 하나 더 두면 그 자리만
    /// 밝아져 어둠이 무너지고, 낙하물 발광 상한과 같은 규칙 체계에 걸린다.
    ///
    /// 셋으로 나눈 이유는 <b>서로 다른 물음에 답하기 때문</b>이다.
    /// <list type="bullet">
    /// <item><see cref="Arc"/> — 꼬리가 <b>펼쳐져 있는가</b>. 호를 따라 흐르는 선</item>
    /// <item><see cref="Contact"/> — 꼬리가 <b>액면에 닿아 있는가</b>. 접점에서 번지는 빛줄기</item>
    /// <item><see cref="Blade"/> — 날이 <b>몸에 붙어 뭉쳤는가</b>. 응축된 점</item>
    /// </list>
    ///
    /// 어둠 속 먼 거리에서 보이는 것은 몸통이 아니라 <b>수면을 긋는 빛줄기</b>이고,
    /// 육지에 올라오면 그 줄기가 사라지면서 날의 에미션만 남는다.
    /// <b>넓게 퍼진 빛 → 뭉친 빛</b>이 곧 위험도의 눈금이다.
    /// </summary>
    public readonly struct ScytheGlow
    {
        /// <summary>꼬리의 호를 따라 흐르는 선의 세기(0~1).</summary>
        public readonly float Arc;

        /// <summary>액면 접점에서 번지는 빛줄기의 세기(0~1). 닿아 있지 않으면 0이다.</summary>
        public readonly float Contact;

        /// <summary>날에 응축된 빛의 세기(0~1).</summary>
        public readonly float Blade;

        public ScytheGlow(float arc, float contact, float blade)
        {
            Arc = arc;
            Contact = contact;
            Blade = blade;
        }
    }

    /// <summary>
    /// 꼬리가 지금 어떤 자세여야 하는가 (스펙 §4).
    ///
    /// <b>새 상태 기계를 세우지 않았다.</b> 자세는 상태가 아니라 <b>상태를 읽는 방식</b>이다 —
    /// 기존 <see cref="CreatureState"/>·<see cref="ScytheAlert"/>·<see cref="HabitatZone"/>를
    /// 입력으로 받아 자세 하나를 내는 <b>순수 함수</b>일 뿐이다. 옆에 상태 기계를 하나
    /// 더 세우면 둘이 따로 놀다가 어긋난 프레임이 생기고, 그때 화면이 거짓말을 한다.
    /// 여기서는 어긋날 자리가 없다 — 저장하는 것이 없기 때문이다.
    ///
    /// 같은 이유로 <b>되돌아오는 조건을 따로 적지 않았다.</b> 올리는 조건이 전부
    /// 사라지면 그 프레임에 곧바로 작업 자세가 나온다. "언제 내려오는가"를 별도 규칙으로
    /// 적으면 올리는 규칙과 내리는 규칙이 갈라져 어느 한쪽에 걸린 채 굳는 상태가 생긴다.
    ///
    /// <b>왜 Domain에 있는가.</b> 자세가 곧 경고이므로 그 규칙은 씬 없이 전수 확인할 수
    /// 있어야 한다. 재는 일(거리·구역)은 몸이 하고, 잰 값으로 판정하는 일은 여기 있다.
    /// </summary>
    /// <remarks>
    /// 위협 「목록」을 받는 입구는 <c>ScytheStance.Threats.cs</c>에 따로 있다
    /// (스펙 §22). 여기 있는 규칙은 그대로다.
    /// </remarks>
    public static partial class ScytheStance
    {
        /// <summary>
        /// 이번 프레임의 꼬리 자세.
        ///
        /// 순서가 곧 우선순위다. 위험한 쪽을 먼저 묻는다 — 애매한 프레임에 낮은
        /// 경보를 내는 것보다 높은 경보를 내는 편이 안전하다.
        /// </summary>
        /// <param name="traits">감지 반경을 여기서 읽는다.</param>
        /// <param name="senses">거리·어그로. 판단 문법은 <see cref="CreatureDecision"/>과 같다.</param>
        /// <param name="state">지금 무엇을 하고 있는가.</param>
        /// <param name="zone">지금 어디에 있는가.</param>
        /// <param name="alert">경계 태세. 무엇이 이것을 올리는가는 스펙 §5의 몫이다.</param>
        public static ScythePosture PostureFor(in CreatureTraits traits, in CreatureSenses senses,
                                               CreatureState state, HabitatZone zone,
                                               ScytheAlert alert)
        {
            // <b>규칙은 이제 여기 없다.</b> 낫이 지금 무엇을 하는 중인지는 §20의
            // 4상태가 답하고, 이 함수는 그 답을 자세로 옮기기만 한다. 우선순위 if
            // 체인을 그대로 두고 옆에 상태 기계를 세우면 둘이 어긋난 프레임이 생긴다 —
            // 이 파일이 처음부터 경계하던 것이 그것이므로, 세우는 대신 <b>여기를
            // 그 위로 옮겼다</b>. 옮긴 뒤에도 답이 한 글자도 달라지지 않는다는 것은
            // ScytheStanceTests가 입력을 전수로 돌려 못 박는다.
            bool detected = CreatureDecision.IsDetected(senses.DistanceToThreat,
                                                        traits.DetectRadius);
            var scythe = ScytheFsm.FromCreatureState(state, detected, senses.AggroLeft, alert);

            return PostureFrom(scythe, state, zone);
        }

        /// <summary>
        /// 4상태에서 자세로 (스펙 §20 "PostureFor를 새 4상태에서 파생하도록 잇는다").
        ///
        /// 상태와 자세를 <b>따로 두지 않고 유도한 이유</b>는 하나다 — 자세는 규칙이
        /// 아니라 <b>규칙을 읽는 방식</b>이기 때문이다. 둘을 독립으로 두면 "지금 상태는
        /// Beware인데 꼬리는 내려가 있다"가 만들어질 수 있고, 그 프레임에 화면이
        /// 거짓말을 한다. 꼬리가 곧 경고이므로 거짓말의 값이 가장 비싼 자리다.
        ///
        /// <b>상태 넷 위에 자리와 죽음이 얹힌다.</b> 순서가 곧 우선순위다.
        /// </summary>
        /// <param name="scythe">§20의 네 상태.</param>
        /// <param name="creatureState">범용 상태. <b>죽었는가</b>만 여기서 본다.</param>
        /// <param name="zone">지금 어디에 있는가.</param>
        public static ScythePosture PostureFrom(ScytheState scythe, CreatureState creatureState,
                                                HabitatZone zone)
        {
            // 죽으면 힘이 빠져 늘어진다. 자세는 <b>살아 있는 것의 신호</b>이므로,
            // 시체가 공격 태세로 굳어 있으면 그 신호가 거짓이 된다.
            if (creatureState == CreatureState.Dead) return ScythePosture.Trailing;

            // <b>짐이 자리보다 세다.</b> 회수 중인 개체는 육지 수로를 지나 둥지로
            // 가므로 아래 육지 규칙에 그대로 걸리는데, 그러면 때리지 못하는 개체가
            // 가장 위험한 실루엣으로 보인다(<see cref="ScythePosture.Laden"/>).
            if (scythe == ScytheState.Retrieve) return ScythePosture.Laden;

            // 육지에 있다는 것 자체가 은폐 프로토콜을 버렸다는 뜻이다(<see cref="ScytheHabitat"/>).
            // 무엇을 하던 중인지는 묻지 않는다 — 평생 육지에 오지 않던 것이 올라온
            // 그 사실이 이미 종막의 압력이다.
            if (zone == HabitatZone.Inland) return ScythePosture.Furled;

            switch (scythe)
            {
                // 교전. 쫓는 것과 때리는 것은 플레이어 쪽에서 보면 같은 일이다.
                case ScytheState.Attack:
                    return ScythePosture.Furled;

                // 작업을 멈췄다. 발령·감지·어그로 여운·물러남이 전부 여기로 접힌다 —
                // 어느 쪽이든 사람 눈에는 <b>꼬리를 들었다</b> 하나로 보여야 한다.
                case ScytheState.Beware:
                    return ScythePosture.Raised;

                default:
                    return ScythePosture.Trailing;
            }
        }

        /// <summary>
        /// 꼬리가 지금 액면을 훑고 있는가.
        ///
        /// <b>자세와 접촉을 따로 묻는다.</b> 해안선에서는 지형이 수면 위로 올라와 있어
        /// 훑을 액면이 발밑에 없지만, 그렇다고 낫이 <b>경계에 들어간 것은 아니다</b> —
        /// 제 서식 범위 안에서 하던 일을 계속하고 있을 뿐이다. 둘을 한 값으로 묶으면
        /// 물가 지형의 생김새가 곧 경고를 내게 되고, 플레이어는 아무 일도 없는데
        /// 꼬리가 오르내리는 것을 본다. 빛은 접촉이 정하고 자세는 판단이 정한다.
        /// </summary>
        public static bool Skims(ScythePosture posture, HabitatZone zone) =>
            posture == ScythePosture.Trailing && zone == HabitatZone.Liquid;

        // 세기 눈금. 합이 아니라 <b>퍼진 넓이</b>가 위험도를 말한다 —
        // 작업 중에는 꼬리 전체와 수면에 걸쳐 흐르고, 공격 태세에서는 날 한 점에 모인다.

        /// <summary>펼친 꼬리의 호가 내는 빛.</summary>
        public const float ArcTrailing = 1f;

        /// <summary>든 꼬리의 호. 절반쯤 접혔으므로 선도 짧아진다.</summary>
        public const float ArcRaised = 0.45f;

        /// <summary>몸에 붙인 꼬리의 호. 거의 남지 않는다.</summary>
        public const float ArcFurled = 0.1f;

        /// <summary>작업 중인 날. 훑는 것이 일이지 베는 것이 일이 아니다.</summary>
        public const float BladeTrailing = 0.2f;

        /// <summary>든 날.</summary>
        public const float BladeRaised = 0.5f;

        /// <summary>몸에 붙어 응축된 날. 어둠 속에서 이것만 남는다.</summary>
        public const float BladeFurled = 1f;

        /// <summary>
        /// 짐을 든 꼬리의 호. 코어를 감아 쥐느라 반쯤 접히므로 든 것과 같다.
        /// </summary>
        public const float ArcLaden = ArcRaised;

        /// <summary>
        /// 짐을 든 날. <b>작업 중보다도 어둡지 않다</b> — 지금 이 꼬리는 무기가 아니라
        /// 손이고, 날이 밝게 응축되면 공격 태세로 잘못 읽힌다. 어둠 속에서 눈에 띄는
        /// 것은 날이 아니라 <b>물고 가는 코어의 자홍</b>이어야 한다(기획서 §2.1).
        /// </summary>
        public const float BladeLaden = BladeTrailing;

        /// <summary>이 자세·이 자리에서 무엇이 얼마나 빛나는가.</summary>
        public static ScytheGlow GlowFor(ScythePosture posture, HabitatZone zone)
        {
            float contact = Skims(posture, zone) ? 1f : 0f;

            switch (posture)
            {
                case ScythePosture.Trailing:
                    return new ScytheGlow(ArcTrailing, contact, BladeTrailing);

                case ScythePosture.Raised:
                    return new ScytheGlow(ArcRaised, contact, BladeRaised);

                // 짐을 들었다. <see cref="Skims"/>가 이미 false이므로 접점 빛은 0이고,
                // 그래서 액면 위를 지나도 <b>수면을 긋는 자국이 남지 않는다</b> —
                // 멀리서 보면 순찰과 구별된다.
                case ScythePosture.Laden:
                    return new ScytheGlow(ArcLaden, contact, BladeLaden);

                default:
                    return new ScytheGlow(ArcFurled, contact, BladeFurled);
            }
        }
    }
}
