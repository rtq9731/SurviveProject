using System;
using System.Collections.Generic;
using UnityEngine;
using Survive.Building;
using Survive.Core;
using Survive.Interaction;
using Survive.Localization;
using Survive.Player;
using Survive.Progression;
using Survive.World;

namespace Survive.Vehicles
{
    /// <summary>
    /// 놓인 돌파정 하나. <b>여기에 타면 챕터 1이 끝난다</b> (스펙 §6).
    ///
    /// <b>건축물도 아니고 손에 드는 도구도 아니다.</b> 둘의 성질을 나눠 갖는다 —
    /// 놓을 자리를 판정하는 쪽은 건축에서 빌리고(<see cref="BreachPodPlacement"/>),
    /// 놓인 뒤에 타는 쪽은 탈것이다(<see cref="BreachPodLaunch"/>).
    /// 이 컴포넌트는 그중 <b>놓인 뒤</b>를 든다.
    ///
    /// <b>판정은 여기 없다.</b> 이 껍데기가 하는 일은 발밑의 층과 자기 성능을 규칙에
    /// 넘기고, 참이라는 답이 오면 하강을 한 번만 트는 것뿐이다 —
    /// <c>DescentZone</c>이 같은 모양으로 서 있다.
    ///
    /// <b>연출과 챕터 종료는 새로 만들지 않는다.</b> 짙은 층을 다 내려갔을 때 벌어지는
    /// 일은 이미 <c>DescentZone.Breach</c>가 안다 — 암전, 다음 씬, 종막 신호까지.
    /// 여기서 그것을 다시 적으면 종막이 두 벌이 되고, 두 벌은 언젠가 갈라진다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BreachPod : MonoBehaviour, IInteractable
    {
        /// <summary>어느 층 위에 놓였는가. 탈 때 이 층을 뚫는다.</summary>
        DescentZone _layer;

        /// <summary>이 돌파정이 감당하는 층 두께(m). 놓기 전에 아이템이 들고 있던 값이다.</summary>
        float _capacity;

        readonly List<GearCapability> _pod = new List<GearCapability>(1);

        bool _launched;

        /// <summary>탄 적이 있는가. 한 번 떠난 것을 두 번 셀 수는 없다.</summary>
        public bool HasLaunched => _launched;

        /// <summary>발밑의 층. 검증이 들여다본다.</summary>
        public DescentZone Layer => _layer;

        /// <summary>몇 대가 떠났는가. 검증이 "한 번만 끝나는지"를 보는 값이다.</summary>
        public static int Launches { get; private set; }

        /// <summary>탄 순간. 하강이 시작되기 직전에 울린다.</summary>
        public static event Action<BreachPod> Launched;

        /// <summary>세워진 것 전부. 겹침 판정이 이 목록을 본다.</summary>
        static readonly List<BreachPod> _all = new List<BreachPod>();
        public static IReadOnlyList<BreachPod> All => _all;

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        void OnDisable() => _all.Remove(this);

        /// <summary>놓을 때 값을 넣는다. <c>DescentZone.Setup</c>과 같은 자리다.</summary>
        public void Setup(DescentZone layer, float capacity)
        {
            _layer = layer;
            _capacity = Mathf.Max(0f, capacity);

            _pod.Clear();
            _pod.Add(new GearCapability(TraversalGear.BreachPod, _capacity));
        }

        /// <summary>지금 탈 수 있는가. 프롬프트와 실제 탑승이 같은 답을 쓰게 한다.</summary>
        public BoardingResult Evaluate() =>
            BreachPodLaunch.Evaluate(_layer != null, _launched, Zone(), _pod, Ledger());

        HazardZone Zone() =>
            _layer != null ? _layer.Zone : new HazardZone(EnvironmentHazard.None, 0f);

        // ── 상호작용 ────────────────────────────────────────────

        public string InteractionPrompt => Loc.T("Build", "pod_prompt_board");

        public bool CanInteract(PlayerContext player) => Evaluate() == BoardingResult.Ok;

        public void Interact(PlayerContext player) => Board();

        /// <summary>
        /// 탄다. 원장에 종막을 적고 하강을 튼다.
        ///
        /// <b>적는 것이 먼저다.</b> 암전이 시작되면 목표가 넘어간 것을 확인할 길이
        /// 없어진다. 순서를 규칙 쪽(<see cref="BreachPodLaunch.Board"/>)이 들고 있다.
        /// </summary>
        public BoardingResult Board()
        {
            var result = BreachPodLaunch.Board(_layer != null, _launched, Zone(), _pod, Ledger());
            if (result != BoardingResult.Ok) return result;

            _launched = true;
            Launches++;

            Debug.Log($"[BreachPod] 돌파정이 짙은 층으로 떠났다 — " +
                      $"윗면 {_layer.TopY:F2}, 두께 {_layer.Zone.Magnitude:F1}m, 용량 {_capacity:F1}m", this);

            Launched?.Invoke(this);

            // 종막은 층이 든다. 여기서 암전과 다음 씬을 다시 적으면 두 벌이 된다.
            // Breach()가 같은 열쇠에 같은 값을 한 번 더 쓰지만 그것은 같은 기록이고,
            // 열쇠가 하나라는 것은 BreachPodPlacementTests가 못 박는다.
            _layer.Breach();
            return result;
        }

        /// <summary>진행 원장. 아직 없으면 null이고, 규칙은 null을 견딘다.</summary>
        static IChapterLedger Ledger() =>
            GameServices.TryGet<ChapterDirector>(out var director) && director != null
                ? new ChapterDirectorLedger(director)
                : null;

        /// <summary>검증이 실행 사이에 상태를 비운다.</summary>
        public static void ResetCounters() => Launches = 0;

        /// <summary>
        /// <see cref="ChapterDirector"/>를 규칙이 아는 모양으로 감싼다.
        ///
        /// 원장 쪽에 인터페이스를 직접 붙이지 않는 이유: 그쪽은 진행·세이브까지
        /// 든 MonoBehaviour이고, 이 라운드가 손댈 자리가 아니다. 어댑터 한 겹이면
        /// 규칙은 Unity를 모르고, 원장은 규칙을 모른다.
        /// </summary>
        sealed class ChapterDirectorLedger : IChapterLedger
        {
            readonly ChapterDirector _director;
            public ChapterDirectorLedger(ChapterDirector director) => _director = director;

            public int GetFlag(string key) => _director.GetFlag(key);
            public void SetFlag(string key, int value) => _director.SetFlag(key, value);
        }
    }
}
