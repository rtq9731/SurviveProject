using System.Collections.Generic;
using UnityEngine;
using Survive.Core;
using Survive.World;

namespace Survive.Building
{
    /// <summary>
    /// 착륙선. <b>첫 거점이자 최종 개조 대상</b>이다(기획서 §5.16).
    ///
    /// 광부는 호수 근방에 스스로 측량하고 내려앉았고, 그 배가 곧 거점이다.
    /// 그리고 <b>최종 목표가 이 배다</b> — 성간 이동 수단을 확보한다는 것이
    /// 이 배를 고치는 일이다(세계관 §7). 못 떠나는 이유 넷이 첫 화면부터
    /// 물건으로 서 있다(<see cref="LanderSystem"/>).
    ///
    /// <b>세 축이 여기서 만난다.</b>
    /// <list type="bullet">
    /// <item><b>부활</b> — 타는 화톳불이 하나도 없을 때 받아 주는 본거지다.
    /// 화톳불과 어떻게 갈리는지는 <see cref="HomeBaseRule"/>에 적어 두었다</item>
    /// <item><b>저장</b> — 개조 진행과 처리장치가 모아 둔 것이 저장본을 넘어간다</item>
    /// <item><b>개조</b> — 자리만 열려 있고 채워지지 않았다(<see cref="LanderRefit"/>)</item>
    /// </list>
    ///
    /// <b>이 컴포넌트는 씬에 놓이는 물건이다. 스스로 서지 않는다.</b>
    /// <see cref="Survive.Vitals.DeathDropService"/>나 <c>DayNightService</c>처럼
    /// 실행 시점에 자기를 설치하지 않는 이유는, 배는 <b>자리가 있는 물건</b>이기
    /// 때문이다. 모델도 자리도 사람의 몫이고 지상 지형은 아직 서지 않았다
    /// (기획서 §13, 「지상 지형 전면 재작업」). 임의의 좌표에 스스로 서면
    /// 그 좌표가 곧 가짜 설계가 된다 — 놓이기 전까지는 세계에 배가 없고,
    /// 없는 동안 부활은 오늘과 똑같이 시작 지점으로 간다.
    ///
    /// <b>규칙은 여기 없다.</b> 어느 거점에서 일어설지는 <see cref="HomeBaseRule"/>이
    /// Unity 없이 답하고, 이 컴포넌트는 좌표를 내주고 상태를 적을 뿐이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class LanderBase : MonoBehaviour, ISaveable, IWaterProcessor
    {
        /// <summary>저장본에서 이 항목을 찾는 열쇠.</summary>
        public const string Key = "lander";

        static readonly List<LanderBase> _all = new List<LanderBase>();

        /// <summary>지금 세계에 선 착륙선들. 설계상 하나지만 목록으로 둔다 —
        /// 프롤로그가 갈리면 두 개가 한 프레임 겹칠 수 있고, 그때 조용히 하나가
        /// 사라지는 것보다 둘이 보이는 편이 낫다.</summary>
        public static IReadOnlyList<LanderBase> Active => _all;

        /// <summary>본거지 노릇을 하는 배. 없으면 null이다.</summary>
        public static LanderBase Current => _all.Count > 0 ? _all[0] : null;

        [Tooltip("일어설 자리. 비우면 배 앞쪽으로 물러선 곳을 쓴다")]
        [SerializeField] Transform standPoint;

        [Tooltip("standPoint가 없을 때 배 앞으로 물러설 거리")]
        [SerializeField] float standOff = 3f;

        readonly LanderRefitLedger _refit = new LanderRefitLedger();

        /// <summary>개조 장부. 오늘은 어떤 계통도 열려 있지 않다.</summary>
        public LanderRefitLedger Refit => _refit;

        /// <summary>
        /// 배 안이 아니라 배 곁. 화톳불에서 <b>불 곁</b>에 세우는 것과 같은 이유다 —
        /// 콜라이더 안에서 일어서면 첫 프레임에 밀려난다.
        /// </summary>
        public Vector3 HomeStandPoint =>
            standPoint != null ? standPoint.position
                               : transform.position + transform.forward * standOff;

        /// <summary>규칙에 넘길 꼴로 줄인 것.</summary>
        public HomeBase AsHomeBase() => new HomeBase(HomeStandPoint, isActiveAndEnabled);

        /// <summary>
        /// 지금 세계의 본거지. 배가 없으면 <see cref="HomeBase.None"/>이다 —
        /// 부르는 쪽이 null을 가리지 않아도 되도록 여기서 한 번에 답한다.
        /// </summary>
        public static HomeBase CurrentHome()
        {
            var lander = Current;
            return lander != null ? lander.AsHomeBase() : HomeBase.None;
        }

        void OnEnable()
        {
            if (!_all.Contains(this)) _all.Add(this);

            // 저장 대상 등록을 SaveCoordinator.Collect()에만 맡기지 않는다.
            // 그 훑기는 씬이 올라온 뒤 한 번뿐이라, 나중에 생긴 배는 저장본에서
            // 통째로 빠진다. 등록은 중복을 걸러 주므로 두 번 불려도 안전하다.
            if (GameServices.TryGet<SaveService>(out var save) && save != null)
                save.Register(this);
        }

        void OnDisable()
        {
            _all.Remove(this);

            // 사라진 배를 목록에 남겨 두면 다음 저장이 <b>파괴된 객체</b>에 상태를
            // 묻고, 불러오기는 그 유령을 먼저 찾아 새 배를 지나친다.
            if (GameServices.TryGet<SaveService>(out var save) && save != null)
                save.Unregister(this);
        }

        // ── 처리장치 ────────────────────────────────────────────
        //
        // 속도와 통 크기는 여기서 정하지 않는다. 이유는 IWaterProcessor에 적어 두었다 —
        // 수분 축을 세우는 쪽이 Configure로 넣는다.

        float _unitsPerSecond;
        float _capacity;
        float _pooled;
        bool _configured;

        public bool Configured => _configured;
        public float UnitsPerSecond => _unitsPerSecond;
        public float Capacity => _capacity;
        public float Pooled => _pooled;

        /// <summary>
        /// 수분 축이 값을 정해 넣는 창구. 정해지기 전에는 아무것도 모이지 않는다.
        /// </summary>
        public void Configure(float unitsPerSecond, float capacity)
        {
            _unitsPerSecond = Mathf.Max(0f, unitsPerSecond);
            _capacity = Mathf.Max(0f, capacity);
            _configured = _unitsPerSecond > 0f && _capacity > 0f;
            _pooled = Mathf.Min(_pooled, _capacity);
        }

        public float Draw(float units)
        {
            if (units <= 0f) return 0f;

            float taken = Mathf.Min(units, _pooled);
            _pooled -= taken;
            return taken;
        }

        void Update()
        {
            if (!_configured || _pooled >= _capacity) return;
            _pooled = Mathf.Min(_capacity, _pooled + _unitsPerSecond * Time.deltaTime);
        }

        // ── 저장 ────────────────────────────────────────────────
        //
        // 무엇이 들어가는가 — <b>플레이 중에 자라는 것만</b> 들어간다.
        // 자리는 씬이 가지므로 적지 않는다. 씬이 배를 옮긴 날 저장본이 옛 좌표로
        // 되돌려 놓으면 지형 밖에 배가 선다.
        //
        // 세이브 포맷은 움직이지 않는다. 저장본은 열쇠-값 목록이고 불러올 때
        // 모르는 열쇠는 건너뛴다 — 항목을 하나 더하는 것은 덧붙임이지 형식 변경이
        // 아니다. 이 열쇠가 없는 옛 저장본은 그냥 열리고, 그때는 아무것도 고치지
        // 않은 배로 남는다.

        [System.Serializable]
        public class State
        {
            /// <summary>고쳐 둔 계통의 이름들. 오늘은 언제나 비어 있다.</summary>
            public List<string> refit = new List<string>();

            /// <summary>처리장치가 모아 둔 양. 자리를 비운 사이에도 자라므로 이어져야 한다.</summary>
            public float pooled;
        }

        public string SaveKey => Key;

        public object CaptureState() => new State
        {
            refit = _refit.Capture(),
            pooled = _pooled,
        };

        public void RestoreState(object state)
        {
            if (state is not State s) return;

            _refit.Restore(s.refit);

            // 통 크기가 아직 안 들어왔으면 자르지 않는다 — 0으로 자르면 값이
            // 들어오기 전에 저장한 사람의 물이 통째로 사라진다.
            _pooled = _configured ? Mathf.Clamp(s.pooled, 0f, _capacity) : Mathf.Max(0f, s.pooled);
        }
    }
}
