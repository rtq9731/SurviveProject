using UnityEngine;
using Survive.Combat;
using Survive.Core;
using Survive.Player;
using Survive.Vitals;

namespace Survive.World
{
    /// <summary>
    /// 섬 사이 바다에 몸을 담그고 있는 동안 살을 깎는다 (백로그 32).
    ///
    /// <b>얼마나 깎이는지는 여기서 정하지 않는다.</b> 그 규칙은 <see cref="MacroniumSea"/>에
    /// 있고 Unity 없이 시험된다. 이 컴포넌트가 하는 일은 지금 몸이 어떻게 잠겨 있는지를
    /// 재서 규칙에 묻고, 나온 값을 초에 한 번씩 세계에 반영하는 것뿐이다.
    ///
    /// <b>왜 <see cref="MacroniumContactService"/>와 따로 서는가.</b> 물질은 같지만 재는
    /// 대상이 다르다. 그쪽은 씬에 놓인 <see cref="MacroniumSurfaceZone"/>(짙은 층)의
    /// 표면 높이를 보고, 이쪽은 <see cref="WaterBody"/>(묽은 바다)에 잠긴 깊이를 본다.
    /// 한 컴포넌트에 두 판정을 넣으면 액면이 없는 씬에서도 바다 판정이 액면 코드를
    /// 지나가게 되어, 둘 중 하나를 고칠 때마다 다른 하나가 흔들린다.
    ///
    /// <b>왜 씬에 놓지 않고 스스로 붙는가.</b> <see cref="DeathDropService"/>·
    /// <see cref="MacroniumContactService"/>와 같은 이유다 — 플레이어 프리팹과 MainScene은
    /// 병합할 수 없는 단일 파일이라 여러 갈래로 나뉘어 일하는 동안 손대지 않는다.
    ///
    /// <b>질식과는 겹쳐서 들어간다.</b> 잠긴 채로 있으면 산소가 줄고(<see cref="SubmergedOxygenDrain"/>),
    /// 그와 <i>동시에</i> 여기서 체력이 깎인다. 산소가 0이 되면 <see cref="PlayerVitals"/>의
    /// 질식 피해가 그 위에 더해진다 — 둘은 서로를 대신하지 않는다. 숨을 참는 것과
    /// 살이 녹는 것은 다른 일이고, 깊이 잠수할수록 두 값이 함께 붙는 것이 맞다.
    ///
    /// <b>높은 데서 물에 빠지는 것은 여전히 무해하다</b>(R8의 낙하 대가 규칙). 입수 자체는
    /// 대가가 없고, 물에 잠긴 <i>다음</i>부터 이 서비스가 세기 시작한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MacroniumSeaService : MonoBehaviour
    {
        static MacroniumSeaService _instance;

        public static MacroniumSeaService Instance => _instance;

        /// <summary>가장 최근에 읽은 잠긴 정도. 검증에서 상태를 집기 위한 것이다.</summary>
        public static SeaImmersion LastImmersion { get; private set; }

        /// <summary>지금 살이 깎이고 있는가.</summary>
        public static bool IsCorroding { get; private set; }

        /// <summary>바다가 지금까지 넣은 피해의 합. 구간별로 빼서 보라고 둔 것이다.</summary>
        public static float TotalDamage { get; private set; }

        /// <summary>물어뜯은 횟수. "안 아팠다"와 "아예 닿지 않았다"를 가른다.</summary>
        public static int Bites { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            LastImmersion = SeaImmersion.Dry;
            IsCorroding = false;
            TotalDamage = 0f;
            Bites = 0;

            var go = new GameObject("MacroniumSeaService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MacroniumSeaService>();
        }

        PlayerVitals _vitals;
        Transform _body;
        PlayerSwimming _swim;
        PlayerLocomotion _loco;

        /// <summary>아직 물리지 않고 쌓아 둔 노출 시간(초).</summary>
        float _pending;

        void OnDisable() => Unhook();

        // 씬을 다시 올리면 지금 잡고 있던 플레이어가 사라진다.
        // DeathDropService와 같은 방식 — 사라졌으면 다시 잡는다.
        void Update()
        {
            if (!Acquire()) { Idle(); return; }

            var immersion = Read();
            LastImmersion = immersion;

            bool footing = _loco != null && _loco.IsGrounded;
            bool corroding = MacroniumSea.Corrodes(immersion, footing);
            IsCorroding = corroding;

            // 죽은 사람을 더 물지 않는다. 되살아나기 전까지 셈을 멈춘다 —
            // 안 그러면 시체가 물에 뜬 채로 가방을 몇 개씩 만든다.
            if (!corroding || _vitals.Health.IsEmpty) { _pending = 0f; return; }

            _pending += Time.deltaTime;
            if (_pending < MacroniumSea.BiteInterval) return;

            _pending -= MacroniumSea.BiteInterval;
            Bite();
        }

        void Idle()
        {
            LastImmersion = SeaImmersion.Dry;
            IsCorroding = false;
            _pending = 0f;
        }

        /// <summary>
        /// 지금 몸이 어떻게 잠겨 있는가.
        ///
        /// <see cref="PlayerSwimming"/>이 이미 재고 있는 값을 옮겨 적는다. 여기서 깊이를
        /// 다시 재면 임계가 둘이 되고, 둘이 어긋나는 순간 "헤엄치는데 안 아프다"거나
        /// 그 반대가 된다.
        /// </summary>
        SeaImmersion Read()
        {
            if (_swim == null) return SeaImmersion.Dry;

            switch (_swim.Current)
            {
                case PlayerSwimming.State.Swimming: return SeaImmersion.Swimming;
                case PlayerSwimming.State.Wading: return SeaImmersion.Wading;
                default: return SeaImmersion.Dry;
            }
        }

        bool Acquire()
        {
            if (_vitals != null && _body != null && _swim != null) return true;

            Unhook();
            if (!GameServices.TryGet<PlayerVitals>(out var found) || found == null) return false;

            _vitals = found;
            _body = found.GetComponentInParent<PlayerContext>()?.transform ?? found.transform;
            _swim = _body.GetComponentInChildren<PlayerSwimming>(true);
            _loco = _body.GetComponentInChildren<PlayerLocomotion>(true);
            return _swim != null;
        }

        void Unhook()
        {
            _vitals = null;
            _body = null;
            _swim = null;
            _loco = null;
            _pending = 0f;
        }

        /// <summary>
        /// 한 입 문다.
        ///
        /// 이미 있는 피해 창구(<see cref="PlayerDamageReceiver"/>)로 넣는다 — 그래야
        /// 피격 연출이 붙고, 체력이 0이 되면 기존 고리가 그대로 받는다:
        /// <see cref="PlayerVitals.Died"/> → <see cref="DeathDropService"/>가 가방을 남기고
        /// → <see cref="RespawnService"/>가 일으켜 세운다. 익사·액면 접촉과 같은 길이다.
        ///
        /// 가해자를 비워 둔다. 자기 몸에서 나온 피해로 보이면 문 앞에서 걸러진다
        /// (자가 타격 차단). 바다는 남이 때리는 것이 아니라 환경이 깎는 것이다.
        /// </summary>
        void Bite()
        {
            var receiver = _body.GetComponentInChildren<IDamageable>();
            if (receiver == null) return;

            Bites++;
            TotalDamage += MacroniumSea.BiteDamage;

            receiver.TakeDamage(new DamageInfo(
                MacroniumSea.BiteDamage, null, _body.position, Vector3.up));
        }
    }
}
