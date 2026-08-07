using System.Collections.Generic;
using Survive.World;

namespace Survive.Building
{
    /// <summary>
    /// 진행 원장에 적는 창구. <c>Survive.Progression.ChapterDirector</c>가 이미 이 모양이라
    /// 새 저장소를 만들지 않고 그것을 이 인터페이스로 감싼다.
    ///
    /// <b>왜 인터페이스인가.</b> 원장은 MonoBehaviour이고 세이브까지 물려 있다.
    /// 탑승 규칙이 그것을 직접 알면 "탄 뒤 챕터가 끝나는가"를 Unity 없이 시험할 수 없다.
    /// </summary>
    public interface IChapterLedger
    {
        int GetFlag(string key);
        void SetFlag(string key, int value);
    }

    /// <summary>탈 수 있는가, 못 타면 왜 못 타는가.</summary>
    public enum BoardingResult
    {
        /// <summary>탄다. 하강이 시작되고 그것이 챕터의 끝이다.</summary>
        Ok,

        /// <summary>아직 놓이지 않았다. 손에 든 채로는 탈 수 없다.</summary>
        NotPlaced,

        /// <summary>놓이긴 했는데 발밑이 짙은 구간이 아니다.</summary>
        NotOnLayer,

        /// <summary>층이 이 돌파정이 뚫을 수 있는 것보다 두껍다.</summary>
        TooThick,

        /// <summary>이미 떠났다. 한 번 내려간 것을 두 번 셀 수는 없다.</summary>
        AlreadyGone,
    }

    /// <summary>
    /// 놓인 돌파정에 <b>타는</b> 판정과, 탄 뒤 진행 원장에 남는 것 (스펙 §6).
    ///
    /// <b>배치와 탑승을 나눈 이유.</b> 놓는 것은 되돌릴 수 있고 타는 것은 되돌릴 수 없다.
    /// 한 함수에 넣으면 "놓자마자 챕터가 끝나는" 사고가 한 줄 차이로 생긴다 —
    /// 마지막 한 걸음은 실수로 밟는 것이 아니라 스스로 고르는 것이어야 한다
    /// (<see cref="MacroniumContact"/>가 하강 키를 요구하는 것과 같은 이유).
    ///
    /// <b>판정을 새로 만들지 않는다.</b> "이 층을 뚫을 수 있는가"는 다른 관문과 똑같이
    /// <see cref="EnvironmentThreat"/>가 답한다 — <see cref="MacroniumDescent.Evaluate"/>가
    /// 쓰는 바로 그 판정이다. 돌파정이 걸치는 물건에서 놓는 물건으로 바뀌어도
    /// <b>무엇을 뚫는가</b>는 달라지지 않았으므로, 여기서 규칙을 다시 적으면
    /// 두 벌이 되어 언젠가 갈라진다.
    ///
    /// <b>종막을 적는 열쇠는 하나다.</b> <see cref="DescendedFlag"/>가 그것이고,
    /// 챕터의 마지막 목표(<c>ch1_06_descent</c>)가 읽는 열쇠와 같아야 한다.
    /// 다르면 사람은 내려갔는데 목표는 영영 완료되지 않는다 —
    /// 화면에는 아무 오류도 뜨지 않는다. <c>BreachPodPlacementTests</c>가 그것을 못 박는다.
    /// </summary>
    public static class BreachPodLaunch
    {
        /// <summary>종막이 진행 원장에 남는 자리. 챕터의 마지막 목표가 이것을 읽는다.</summary>
        public const string DescendedFlag = "ch1_descended";

        /// <summary>
        /// 지금 탈 수 있는가. 원장은 <b>읽기만</b> 한다 — 판정에 부작용이 있으면
        /// 미리보기(프롬프트를 띄울지)와 실제 탑승이 어긋난다.
        /// </summary>
        /// <param name="placed">놓여 있는가.</param>
        /// <param name="alreadyGone">이 돌파정이 이미 떠났는가.</param>
        /// <param name="layer">발밑의 구간. 짙은 구간이어야 한다.</param>
        /// <param name="pod">이 돌파정이 감당하는 것. 걸친 것이 아니라 놓인 물건의 성능이다.</param>
        /// <remarks>
        /// <b>「이미 떠났다」가 제일 먼저다.</b> 떠난 돌파정은 자리도 층도 잃는다 —
        /// 그 상태를 「놓이지 않았다」로 답하면 화면은 아직 놓을 수 있다는 말을 하게 된다.
        /// 끝난 일은 끝났다고 답하는 것이 먼저다.
        ///
        /// <b>원장도 같이 본다.</b> 돌파정이 두 대 놓여 있어도 챕터는 한 번만 끝난다.
        /// 원장이 없으면(순수 문맥) <paramref name="alreadyGone"/> 하나로 판정한다.
        /// </remarks>
        public static BoardingResult Evaluate(bool placed, bool alreadyGone, HazardZone layer,
                                              IReadOnlyList<GearCapability> pod,
                                              IChapterLedger ledger)
        {
            if (alreadyGone) return BoardingResult.AlreadyGone;
            if (ledger != null && ledger.GetFlag(DescendedFlag) > 0) return BoardingResult.AlreadyGone;

            if (!placed) return BoardingResult.NotPlaced;
            if (layer.Hazard != EnvironmentHazard.MacroniumLayer) return BoardingResult.NotOnLayer;

            // 같은 판정을 쓴다. 장비가 없다는 답도 여기서는 "두껍다"로 읽힌다 —
            // 놓인 돌파정 자신이 곧 그 장비라, 없을 수가 없다.
            return EnvironmentThreat.CanPass(layer, pod)
                ? BoardingResult.Ok
                : BoardingResult.TooThick;
        }

        /// <summary>
        /// 탄다. <see cref="BoardingResult.Ok"/>일 때만 진행 원장에 종막을 적는다.
        ///
        /// <b>적는 것이 먼저다.</b> 하강 연출이 시작되면 화면이 덮여 목표가 넘어간 것을
        /// 확인할 길이 없어진다. 원장은 즉시, 연출은 그다음이다 —
        /// <c>DescentZone.Breach</c>가 잡아 둔 순서 그대로다.
        /// </summary>
        public static BoardingResult Board(bool placed, bool alreadyGone, HazardZone layer,
                                           IReadOnlyList<GearCapability> pod,
                                           IChapterLedger ledger)
        {
            var result = Evaluate(placed, alreadyGone, layer, pod, ledger);
            if (result != BoardingResult.Ok) return result;

            ledger?.SetFlag(DescendedFlag, 1);
            return result;
        }
    }
}
