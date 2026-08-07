namespace Survive.Creatures
{
    /// <summary>
    /// 낫 하나가 지금 무엇을 하는 중인가 (기획서 §4.5 "상태머신 — 넷", 스펙 §20).
    ///
    /// <b>범용 <see cref="CreatureState"/>와 무엇이 다른가.</b> 저쪽은 다섯 종이 함께 쓰는
    /// 어휘라 <c>Chase</c>·<c>Flee</c>처럼 <b>몸이 지금 하는 동작</b>을 말한다. 이쪽은
    /// 낫 하나의 어휘라 <b>은폐 프로토콜이 지금 어디까지 물러났는가</b>를 말한다.
    /// 같은 <c>Wander</c> 한 프레임이 저쪽에서는 "목적지로 걷는다" 하나지만, 이쪽에서는
    /// "제 일을 하는 중(Patrol)"과 "빛에 막혀 맴도는 중(Beware)"으로 갈린다.
    /// 그 구분이 없으면 꼬리가 둘을 같은 자세로 그린다.
    /// </summary>
    public enum ScytheState
    {
        /// <summary>
        /// 순찰. 제 일을 하는 중이다 — 물 위만 돌고 꼬리로 액면을 훑는다.
        /// </summary>
        Patrol,

        /// <summary>
        /// 따라붙기. 작업을 멈추고 사람을 본다. 아직 덤비지는 않는다.
        /// <b>시간으로 풀리지 않는다</b> — <see cref="ScytheFsm"/> 참조.
        /// </summary>
        Beware,

        /// <summary>
        /// 교전. 등 뒤 사각으로 파고들어 붙었다.
        /// </summary>
        Attack,

        /// <summary>
        /// 회수. 코어를 꼬리로 들고 둥지로 간다.
        ///
        /// <b>개체가 스스로 고르지 못한다.</b> 월드가 지정하는 통로로만 들어오고
        /// 나간다(<see cref="ScytheDirective"/>). 개체 전이 안의 평범한 가지로 넣으면
        /// 교전 중일 때 우선순위 다툼이 생긴다.
        /// </summary>
        Retrieve,
    }

    /// <summary>
    /// 월드가 개체에게 상태를 <b>강제로</b> 지정하는 통로 (스펙 §20).
    ///
    /// <b>왜 전이표 밖에 두는가.</b> 코어가 필드에 떨어지는 것은 개체가 관찰해서 아는
    /// 일이 아니라 <b>월드에서 벌어진 사건</b>이다. 그것을 개체 판단의 한 가지로 넣으면
    /// "교전 중인데 회수 지시가 왔다"에서 둘이 우선순위를 다투고, 그 다툼의 답을
    /// 개체마다 따로 적게 된다.
    /// </summary>
    public enum ScytheDirective
    {
        /// <summary>지시 없음. 개체가 제 전이표대로 돈다.</summary>
        None,

        /// <summary>회수하라. <b>무엇을 하던 중이든 이긴다.</b></summary>
        Retrieve,

        /// <summary>회수가 끝났다. 순찰로 돌려보낸다.</summary>
        Release,
    }

    /// <summary>
    /// 이번 프레임에 전이가 보는 것 전부. <b>전부 몸이 재어 온 값이고, 여기서 다시
    /// 재는 것은 하나도 없다</b> — 그것이 이 규칙을 씬 없이 전수로 확인할 수 있게 한다.
    /// </summary>
    public readonly struct ScytheSituation
    {
        /// <summary>고른 상대가 감지 반경 안인가 (<see cref="CreatureDecision.IsDetected"/>).</summary>
        public readonly bool Detected;

        /// <summary>
        /// 빛이 이번 판단을 어떻게 가로막는가.
        ///
        /// <b>여기가 §19와 물리는 자리다.</b> 등 뒤 사각인지를 따로 묻지 않고
        /// <see cref="CreatureDecision.JudgeLight"/>의 답을 그대로 받는다. 사각은 그 함수가
        /// 이미 <see cref="LightVerdict.Clear"/>로 답하고 있으므로, 여기서 사각을 한 번 더
        /// 판정하면 <b>같은 물음에 답하는 곳이 둘</b>이 되고 언젠가 서로 어긋난다
        /// (타겟 선정에서 세운 원칙과 같다 — <see cref="ThreatSelection.BlockedByLight"/>).
        /// </summary>
        public readonly LightVerdict Light;

        /// <summary>
        /// 따라잡고 있는가 — 스펙 §20의 "이동속도 조건".
        ///
        /// <b>속도 숫자를 여기서 들지 않는다.</b> 낫이 사람보다 빠른 것(6.2)은 정의에
        /// 적힌 값이고 튜닝으로 바뀐다. 규칙이 물어야 하는 것은 "지금 간격이 줄고
        /// 있는가"이므로, 재는 일은 몸에 두고 여기서는 답만 받는다. 이 조건이 없으면
        /// 사각에 들어선 그 프레임에 무조건 붙어, 달아나는 사람과 멈춰 선 사람이
        /// 같은 대접을 받는다.
        /// </summary>
        public readonly bool Closing;

        /// <summary>
        /// 사람이 <b>고정 조명</b>(화톳불·빛기둥) 안으로 들어갔는가.
        ///
        /// <b>임시 조명(랜턴)은 여기 들어오지 않는다.</b> 그 구분이 이 상태 기계에서
        /// 가장 큰 갈림이다 — 랜턴은 따라붙기를 풀지 못하고 고정 조명만 푼다.
        /// 구분의 근거는 데이터에 있다: 사람을 따라다니는 광원은 앞쪽만 지키므로
        /// 내주는 쪽이 있고(<c>IOffsetLitSource</c>), 고정 조명은 앞뒤가 없어 메운다.
        /// </summary>
        public readonly bool PlayerNearFixedLight;

        /// <summary>조명탄에 밀렸는가 (스펙 §8-3). 그 물건이 서기 전까지는 언제나 false다.</summary>
        public readonly bool PushedByFlare;

        /// <summary>
        /// 경계 등급. <b>월드가 소유하고 개체는 읽기만 한다</b>
        /// (<see cref="ScytheWatch"/>). 값으로 받는 것은 이 규칙이 순수 함수이기
        /// 때문이고, 그 값을 어디서 얻는지는 부르는 쪽의 사정이다.
        /// </summary>
        public readonly ScytheAlert Alert;

        public ScytheSituation(bool detected, LightVerdict light, bool closing,
                               bool playerNearFixedLight = false, bool pushedByFlare = false,
                               ScytheAlert alert = ScytheAlert.Calm)
        {
            Detected = detected;
            Light = light;
            Closing = closing;
            PlayerNearFixedLight = playerNearFixedLight;
            PushedByFlare = pushedByFlare;
            Alert = alert;
        }
    }

    /// <summary>
    /// 낫의 4상태 전이 규칙 (스펙 §20). <b>전부 순수 함수다.</b>
    ///
    /// <b>기존 <see cref="CreatureDecision"/>을 덮어쓰지 않는다.</b> 저것은 다섯 종이
    /// 함께 쓰고 있으므로 낫의 사정으로 고치면 나머지 넷의 감촉이 조용히 바뀐다.
    /// 이 파일은 그 위에 얹혀, 저쪽이 낸 답(<see cref="LightVerdict"/>·감지)을
    /// <b>낫의 어휘로 다시 읽는다</b>.
    ///
    /// <b>왜 시간으로 풀지 않는가.</b> 낫은 프로토콜로 도는 정비 유닛이라
    /// 「포기」라는 개념이 없다. 그리고 실무적으로 타이머는 <b>플레이어가 읽을 수
    /// 없는 규칙</b>이다 — 읽을 수 없으면 배울 수 없고, 배울 수 없으면 이 게임의
    /// 차별화 축(플레이어가 규칙을 배워서 이긴다)과 어긋난다. 따라붙기를 푸는 것은
    /// 눈에 보이는 사건 둘뿐이다: <b>감지에서 벗어나거나, 고정 조명으로 걸어 들어가거나.</b>
    /// </summary>
    public static class ScytheFsm
    {
        /// <summary>
        /// 이번 프레임의 다음 상태.
        ///
        /// <b>Retrieve로 가는 길이 여기 없다.</b> 그것은 <see cref="Apply"/>의 몫이고,
        /// 이 함수만 놓고 보면 회수는 존재하지 않는 상태다. 그것이 "개체가 스스로
        /// 고르지 못한다"의 내용이다.
        /// </summary>
        public static ScytheState Next(ScytheState current, in ScytheSituation situation)
        {
            // 회수 중인 개체는 제 판단을 하지 않는다. 넣은 것도 월드이고 빼는 것도
            // 월드다 — 여기서 빠져나갈 길을 하나라도 두면 짐을 든 채 덤비는 프레임이
            // 생기고, 그러면 "꼬리가 무기인데 짐을 들었다"는 형태의 약속이 깨진다.
            if (current == ScytheState.Retrieve) return ScytheState.Retrieve;

            switch (current)
            {
                case ScytheState.Patrol:
                    return Rouses(situation) ? ScytheState.Beware : ScytheState.Patrol;

                case ScytheState.Beware:
                    if (Releases(situation)) return ScytheState.Patrol;
                    if (Pounces(situation)) return ScytheState.Attack;
                    return ScytheState.Beware;

                case ScytheState.Attack:
                    // <b>놓쳤으면 교전이 아니다.</b> 표(§20)에는 조명탄과 랜턴만 적혀
                    // 있지만 그 둘만 두면 <b>어둠 속에서 한 번 붙은 개체가 영영 교전에
                    // 남는다</b> — 빛이 없으면 Light가 계속 Clear이기 때문이다. 배선하고
                    // 나서야 드러났다: 사람을 32m 밖으로 밀어내도 꼬리가 공격 태세로
                    // 굳은 채였다(「낫 꼬리」 평시 관찰창 5회 연속 실패). 교전은
                    // "붙었다"는 뜻이고, 감지도 되지 않는 것에 붙어 있을 수는 없다.
                    //
                    // 곧바로 순찰로 내리지 않는 것은 이 기계가 한 프레임에 한 단계만
                    // 가기 때문이다. 다음 프레임에 따라붙기가 같은 조건을 보고 순찰로
                    // 내린다 — 내려가는 길도 올라온 길과 같은 계단을 밟는다.
                    if (!situation.Detected) return ScytheState.Beware;

                    // 붙어 있던 것을 떼어내는 일은 조명탄의 몫이다(§8-3). 랜턴만으로는
                    // 이 전이가 일어나지 않는다 — 아래 빛 판정이 사각에서 Clear를 내므로,
                    // 등 뒤에 붙은 개체는 랜턴을 켜도 그대로 붙어 있다.
                    if (situation.PushedByFlare) return ScytheState.Beware;

                    // 빛이 다시 앞을 막았다. 사람이 몸을 돌려 사각을 없앴거나, 개체가
                    // 빛 웅덩이 안으로 들어왔거나 — 둘 다 "랜턴 반경에 다시 들어왔다"다.
                    if (situation.Light != LightVerdict.Clear) return ScytheState.Beware;

                    return ScytheState.Attack;

                default:
                    return current;
            }
        }

        /// <summary>
        /// 월드의 지시를 얹은 다음 상태. <b>지시가 언제나 전이표를 이긴다.</b>
        ///
        /// 지시가 없으면 <see cref="Next"/>와 글자 그대로 같다 — 그래야 통로를 따로
        /// 둔 것이 개체의 평소 판단에 아무것도 더하지 않는다.
        /// </summary>
        public static ScytheState Apply(ScytheState current, ScytheDirective directive,
                                        in ScytheSituation situation)
        {
            switch (directive)
            {
                // 교전 중이어도 이긴다. 우선순위를 여기 한 줄로 적어 두는 것이
                // 개체마다 다른 답을 적지 않게 하는 유일한 방법이다.
                case ScytheDirective.Retrieve: return ScytheState.Retrieve;

                // 회수가 끝나면 순찰로 돌아간다. 따라붙기로 돌려보내지 않는 것은
                // 코어를 내려놓은 자리가 둥지이고, 둥지는 사람에게서 먼 곳이기 때문이다.
                // 사람이 거기까지 따라왔으면 다음 프레임의 감지가 다시 올린다.
                case ScytheDirective.Release: return ScytheState.Patrol;

                default: return Next(current, situation);
            }
        }

        /// <summary>
        /// 이 상태에서 사람을 때릴 수 있는가.
        ///
        /// <b>짐을 든 꼬리는 무기가 아니다.</b> 기획서 §4.5는 회수 연출의 알맹이를
        /// "꼬리가 무기인데 짐을 들고 있으므로 공격할 수 없다는 것이 형태로 읽힌다"로
        /// 적었다. 형태로만 읽히고 규칙에서는 때릴 수 있으면 그 읽음이 거짓말이 된다.
        /// </summary>
        public static bool CanAttack(ScytheState state) => state == ScytheState.Attack;

        /// <summary>
        /// 이 상태에서 육지에 올라갈 수 있는가.
        ///
        /// 순찰만 물 위에 묶인다(§4.5 "이동 범위"). 나머지 셋은 전역이지만, 육지에
        /// 실제로 올라갈 수 있는지는 여전히 <see cref="ScytheHabitat.CanEnter"/>가
        /// 경계 등급으로 가른다 — <b>상태가 범위를 넓히고 등급이 그것을 허가한다.</b>
        /// 두 물음을 하나로 접으면 "따라붙는 중이면 평시에도 육지로 올라온다"가 되어
        /// A섬 내륙이 안전하다는 약속이 깨진다.
        /// </summary>
        public static bool RangesInland(ScytheState state) => state != ScytheState.Patrol;

        /// <summary>
        /// 순찰을 깨우는 것.
        ///
        /// <b>발령은 감지 없이도 깨운다.</b> 다섯이 전부 꼬리를 들고 오는 상태에서
        /// 한 마리가 아직 액면을 훑고 있으면 그 자세가 거짓말이 된다.
        /// </summary>
        static bool Rouses(in ScytheSituation s) =>
            s.Detected || s.Alert == ScytheAlert.Alarmed;

        /// <summary>
        /// 따라붙기를 푸는 것 — <b>시간은 여기 없다</b>.
        ///
        /// <b>발령 중에는 고정 조명도 풀지 못한다.</b> 등급은 월드가 건 것이고 고정
        /// 조명은 개체가 읽는 것이라, 월드가 깐 바닥을 개체 판단이 뚫으면 등급을
        /// 월드가 소유하는 뜻이 없어진다. 회수 지시가 전이표를 이기는 것과 같은 이유다.
        /// </summary>
        static bool Releases(in ScytheSituation s) =>
            s.Alert != ScytheAlert.Alarmed && (!s.Detected || s.PlayerNearFixedLight);

        /// <summary>
        /// 덤벼드는 조건 — <b>빛이 열려 있고 따라잡고 있을 때</b>.
        ///
        /// <b>"등 뒤 사각"을 여기서 다시 판정하지 않는다.</b> 랜턴이 켜져 있는 동안
        /// <see cref="LightVerdict.Clear"/>에 이르는 길은 사각뿐이므로
        /// (<see cref="CreatureDecision.JudgeLight"/>), 빛 판정을 그대로 받는 것이
        /// 곧 사각 조건이다. 그리고 랜턴이 꺼져 있으면 사각이라는 것 자체가 없는데
        /// 사각을 필수로 걸면 <b>불을 끈 사람이 오히려 안전해진다</b> — 규칙을 두 벌
        /// 쓰면 그런 구멍이 생긴다.
        /// </summary>
        static bool Pounces(in ScytheSituation s) =>
            s.Detected && s.Light == LightVerdict.Clear && s.Closing;

        /// <summary>
        /// 범용 상태로 도는 지금의 몸을 낫의 어휘로 읽는다.
        ///
        /// <b>이것은 두 번째 상태 기계가 아니라 통역이다.</b> 몸(<c>CreatureBrain</c>)은
        /// 아직 <see cref="CreatureDecision"/>으로 돌고 있고, 저장된 <see cref="ScytheState"/>가
        /// 없다. 저장하지 않고 매 프레임 읽으면 두 기계가 어긋날 자리가 아예 없다 —
        /// <see cref="ScytheStance"/>가 처음부터 쓰던 논법 그대로다.
        ///
        /// <b>회수는 절대 나오지 않는다.</b> 몸이 아는 것으로는 코어 사건을 알 수 없고,
        /// 알 수 있어서도 안 된다.
        /// </summary>
        /// <param name="creatureState">범용 상태. 몸이 지금 하는 동작.</param>
        /// <param name="detected">고른 상대가 감지 반경 안인가.</param>
        /// <param name="aggroLeft">남은 어그로. <b>방금까지 보고 있었다</b>는 여운이다.</param>
        /// <param name="alert">월드가 소유한 경계 등급.</param>
        public static ScytheState FromCreatureState(CreatureState creatureState, bool detected,
                                                    float aggroLeft, ScytheAlert alert)
        {
            // 쫓는 것과 때리는 것은 사람 쪽에서 보면 같은 일이다.
            if (creatureState == CreatureState.Chase || creatureState == CreatureState.Attack)
                return ScytheState.Attack;

            if (alert == ScytheAlert.Alarmed) return ScytheState.Beware;
            if (detected) return ScytheState.Beware;

            // 어그로가 곧 여운이다. 시야를 벗어난 순간 작업으로 돌아가면 경고가
            // 깜빡이는 등처럼 보인다.
            if (aggroLeft > 0f) return ScytheState.Beware;

            // 빛에 밀려 도는 프레임. 물러나는 중이면 작업 중이 아니다.
            if (creatureState == CreatureState.Flee) return ScytheState.Beware;

            return ScytheState.Patrol;
        }
    }
}
