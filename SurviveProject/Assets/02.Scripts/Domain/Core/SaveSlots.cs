using System;

namespace Survive.Core
{
    /// <summary>
    /// <b>어느 슬롯에 쓰는가.</b> 저장이 실제로 손대는 파일 하나를 정하는 규칙이고,
    /// 그래서 파일도 Unity도 모르는 순수 규칙으로 여기 서 있다.
    ///
    /// <b>왜 이것이 따로 필요했는가 (2026-08-07).</b> 저장 경로
    /// (<c>LocalLow/DefaultCompany/SurviveProject</c>)는 <b>이 기계의 에디터 전부가
    /// 공유한다</b> — 클론 셋과 사람이 직접 플레이하는 에디터가 같은
    /// <c>save_auto.json</c> 하나를 쓴다. 그런데 E2E 시나리오는 챕터 목표를 넘길 때마다
    /// 자동 저장을 건드리고(<c>SaveCoordinator.autoSaveOnObjective</c>), 어떤 시나리오는
    /// 시작하면서 저장본을 지우기까지 한다. <b>뒤에서 도는 검사가 사람이 하던
    /// 이어하기를 밟는다</b>는 뜻이고, 실제로 저장본이 커진 것을 보고 알아냈다.
    ///
    /// <b>끄지 않고 갈아 끼운다.</b> 자동 저장을 꺼 버리면 안전해지지만 그 순간
    /// <b>자동 저장 자체를 검증할 수 없게 된다</b> — 검사가 지나가지 않는 길은
    /// 곧 사각지대가 된다. 그래서 저장을 그대로 돌리되 <b>쓰는 자리만 바꾼다.</b>
    /// 검사가 도는 동안에는 기본 슬롯을 <b>요청해도</b> 전용 슬롯으로 간다.
    ///
    /// <b>왜 「하지 마라」가 아니라 「할 수 없다」인가.</b> 시나리오마다 슬롯 이름을
    /// 손으로 넘기는 규율은 이미 있었고(<c>E2ELander</c>·<c>E2EBlueprintUnlock</c>),
    /// 그것을 잊은 시나리오가 정확히 사고를 냈다. 규율이 아니라 길목에 두어야 한다.
    /// </summary>
    public static class SaveSlots
    {
        /// <summary>사람이 실제로 이어하는 슬롯. <b>검사는 여기에 한 바이트도 쓰지 않는다.</b></summary>
        public const string Default = "auto";

        /// <summary>격리 슬롯임을 이름만 보고 알 수 있게 하는 머리말.</summary>
        public const string IsolationPrefix = "e2e_";

        /// <summary>이름 없는 저장이 실제로 가는 곳. 격리 중이 아니면 <see cref="Default"/>다.</summary>
        public static string Active { get; private set; } = Default;

        /// <summary>지금 격리 중인가.</summary>
        public static bool IsIsolated => Active != Default;

        /// <summary>이 에디터가 쓸 격리 슬롯 이름. 세션마다 달라야 <b>동시에 도는 다른 클론과 겹치지 않는다.</b></summary>
        public static string IsolatedNameFor(int sessionId) =>
            IsolationPrefix + Math.Abs(sessionId).ToString();

        /// <summary>격리 슬롯인가. 이름만으로 판정한다.</summary>
        public static bool IsIsolationSlot(string slot) =>
            !string.IsNullOrEmpty(slot) && slot.StartsWith(IsolationPrefix, StringComparison.Ordinal);

        /// <summary>
        /// 지금부터 기본 슬롯 요청을 <paramref name="slot"/>으로 돌린다.
        ///
        /// 격리 슬롯이 아닌 이름은 <b>받지 않는다</b>. 기본 슬롯을 격리 슬롯이라 우기는
        /// 호출이 통과하면 이 규칙 전체가 아무 일도 하지 않는 장식이 된다.
        /// </summary>
        public static void Isolate(string slot)
        {
            if (string.IsNullOrEmpty(slot))
                throw new ArgumentException("격리 슬롯 이름이 비어 있다.", nameof(slot));

            if (!IsIsolationSlot(slot))
                throw new ArgumentException(
                    $"격리 슬롯은 '{IsolationPrefix}'로 시작해야 한다: '{slot}'. " +
                    "기본 슬롯을 격리 슬롯으로 삼을 수는 없다.", nameof(slot));

            Active = slot;
        }

        /// <summary>격리를 푼다. 사람의 저장으로 돌아간다.</summary>
        public static void Release() => Active = Default;

        /// <summary>
        /// 이 요청이 실제로 가는 슬롯.
        ///
        /// 이름을 안 준 요청은 <see cref="Active"/>로 가고, <b>기본 슬롯을 콕 집은
        /// 요청도 격리 중에는 <see cref="Active"/>로 간다.</b> 뒤엣것이 이 규칙의 핵심이다 —
        /// 시나리오가 <c>Save("auto")</c>를 부르든 <c>Delete()</c>를 부르든 사람의
        /// 저장본에는 닿지 못한다.
        ///
        /// 기본도 아니고 비어 있지도 않은 이름은 <b>그대로 둔다.</b> 이미 자기 슬롯을
        /// 쓰고 있는 시나리오의 두 슬롯을 하나로 합쳐 버리면 그 시나리오가 검증하던
        /// 「슬롯이 서로 다르다」가 사라진다.
        /// </summary>
        public static string Resolve(string requested)
        {
            if (string.IsNullOrEmpty(requested)) return Active;
            return requested == Default ? Active : requested;
        }

        /// <summary>
        /// 이 요청이 사람의 저장본을 건드리는가.
        ///
        /// 검사가 <b>음성 확인</b>에 쓰는 창구다 — 격리를 걸지 않으면 참이 되어야 한다.
        /// 그것이 참이 되지 않으면 이 규칙은 아무것도 막고 있지 않은 것이다.
        /// </summary>
        public static bool TouchesDefault(string requested) => Resolve(requested) == Default;

        /// <summary>슬롯 이름에 대응하는 파일 이름. <b>풀이하지 않는다</b> — 준 이름 그대로다.</summary>
        public static string FileNameOf(string slot) => "save_" + slot + ".json";
    }
}
