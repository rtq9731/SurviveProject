using System;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Core;

namespace Survive.Vitals
{
    /// <summary>
    /// 플레이어의 게이지들을 보유하고 매 프레임 갱신한다.
    /// 산소가 0이면 체력이 깎인다 (질식).
    ///
    /// <b>둘이었던 것이 넷이 되었다 (2026-08-07, 스펙 §6).</b> 수분과 식량이 붙으면서
    /// 「필드 두 개를 손으로 만든다」가 더는 서지 않는다. 그래서 <b>목록으로 열었다</b> —
    /// 이름 있는 창구(<see cref="Health"/>·<see cref="Oxygen"/>·<see cref="Hydration"/>·
    /// <see cref="Food"/>)는 그대로 두되, 그것들이 전부 <see cref="All"/>에 들어 있고
    /// <see cref="TryGet"/>으로 아이디로도 집힌다. 다음에 하나가 더 붙을 때
    /// 고칠 자리는 <see cref="Awake"/> 한 줄이다.
    ///
    /// <b>수분·식량의 정의만 왜 <c>Resources</c>에서 읽는가.</b> 체력·산소는 플레이어
    /// 프리팹의 직렬화 필드에 꽂혀 있는데, <b>프리팹은 병합할 수 없는 단일 파일</b>이라
    /// 여러 갈래가 동시에 도는 동안 손대지 않는다는 규율이 있다
    /// (<see cref="Survive.World.MacroniumSeaService"/>가 스스로 서는 것과 같은 이유다).
    /// 그래서 새로 붙는 둘은 <c>08.Data/Vitals/Resources/</c>에서 읽는다 —
    /// <c>DiscoveryBook</c>·<c>AudioCueBook</c>이 이미 쓰는 길이다.
    /// 프리팹을 열 수 있는 날이 오면 네 개를 같은 방식으로 맞추면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerVitals : MonoBehaviour
    {
        [SerializeField] VitalDefinitionSO healthDefinition;
        [SerializeField] VitalDefinitionSO oxygenDefinition;

        [Tooltip("산소가 0일 때 초당 깎이는 체력")]
        [SerializeField] float suffocationDamagePerSecond = 5f;

        [Header("피드백")]
        [Tooltip("산소가 경고선 아래로 처음 떨어질 때 재생. 경고음·화면 가장자리 맥동")]
        [SerializeField] MMF_Player oxygenWarningFeedback;

        [Tooltip("산소가 경고선 위로 회복될 때 재생")]
        [SerializeField] MMF_Player oxygenRecoveredFeedback;

        [Range(0f, 1f)]
        [Tooltip("이 비율 아래로 내려가면 경고. 챕터 1의 핵심 압박 장치")]
        [SerializeField] float oxygenWarningThreshold = 0.2f;

        [Tooltip("사망 시 재생")]
        [SerializeField] MMF_Player deathFeedback;

        readonly List<IOxygenModifier> _oxygenModifiers = new List<IOxygenModifier>();

        readonly List<Vital> _all = new List<Vital>();
        readonly Dictionary<string, Vital> _byId = new Dictionary<string, Vital>();

        public Vital Health { get; private set; }
        public Vital Oxygen { get; private set; }

        /// <summary>수분. 줄어드는 일과 채우는 일은 <see cref="SustenanceService"/>가 한다.</summary>
        public Vital Hydration { get; private set; }

        /// <inheritdoc cref="Hydration"/>
        public Vital Food { get; private set; }

        /// <summary>이 몸이 가진 게이지 전부. 세이브·화면·검증이 훑는 자리다.</summary>
        public IReadOnlyList<Vital> All => _all;

        /// <summary>아이디로 집는다. 없으면 거짓 — <b>조용히 새 게이지를 만들지 않는다.</b></summary>
        public bool TryGet(string id, out Vital vital) => _byId.TryGetValue(id, out vital);

        public event Action Died;

        bool _deathReported;
        bool _warning;

        void Awake()
        {
            _all.Clear();
            _byId.Clear();

            Health    = Add("health", healthDefinition, 100f);
            Oxygen    = Add("oxygen", oxygenDefinition, 100f);
            Hydration = Add(Sustenance.HydrationId, Load(Sustenance.HydrationId), Sustenance.MaxValue);
            Food      = Add(Sustenance.FoodId,      Load(Sustenance.FoodId),      Sustenance.MaxValue);
        }

        Vital Add(string id, VitalDefinitionSO def, float defaultMax)
        {
            var vital = Create(def, defaultMax);
            _all.Add(vital);
            _byId[id] = vital;
            return vital;
        }

        /// <summary>
        /// <c>Resources</c>에서 정의를 읽는다. 에셋 이름이 곧 경로다.
        ///
        /// 못 찾아도 던지지 않는다 — 정의가 없으면 <see cref="Create"/>가 기본 최대치로
        /// 게이지를 세우고, 그 편이 「수분이라는 것이 아예 없는 몸」보다 낫다.
        /// 에셋이 실제로 있는지는 <c>SustenanceTests</c>가 검사한다.
        /// </summary>
        static VitalDefinitionSO Load(string id)
        {
            // 파일 이름은 첫 글자만 대문자다 (Hydration.asset / Food.asset).
            string file = char.ToUpperInvariant(id[0]) + id.Substring(1);
            return Resources.Load<VitalDefinitionSO>(file);
        }

        static Vital Create(VitalDefinitionSO def, float defaultMax)
        {
            if (def == null) return new Vital(defaultMax, defaultMax);
            return new Vital(def.maxValue, def.startValue);
        }

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<PlayerVitals>();

        void Update()
        {
            float dt = Time.deltaTime;

            Oxygen.Modify(CurrentOxygenRate() * dt);

            // 질식은 환경 피해와 겹쳐서 들어간다. 매크로늄 바다에 잠긴 채 숨까지 다하면
            // 살이 녹는 값(MacroniumSeaService)과 이 값이 함께 붙는다 — 둘 중 하나가
            // 다른 하나를 대신하지 않는다. 숨을 참는 것과 살이 녹는 것은 다른 일이다.
            if (Oxygen.IsEmpty)
                Health.Modify(-suffocationDamagePerSecond * dt);
            else if (healthDefinition != null && healthDefinition.passiveRatePerSecond != 0f)
                Health.Modify(healthDefinition.passiveRatePerSecond * dt);

            RefreshOxygenWarning();

            if (Health.IsEmpty && !_deathReported)
            {
                _deathReported = true;
                deathFeedback?.PlayFeedbacks();
                Died?.Invoke();
            }
            // 체력이 돌아오면 다음 사망을 다시 보고한다. 걸쇠를 풀지 않으면
            // 한 판에 한 번만 죽을 수 있고, 사망 드롭도 첫 죽음에서만 걸린다.
            else if (!Health.IsEmpty && _deathReported)
            {
                _deathReported = false;
            }
        }

        void RefreshOxygenWarning()
        {
            bool danger = Oxygen.Normalized <= oxygenWarningThreshold;

            if (danger && !_warning)
            {
                _warning = true;
                oxygenWarningFeedback?.PlayFeedbacks();
            }
            else if (!danger && _warning)
            {
                _warning = false;
                oxygenWarningFeedback?.StopFeedbacks();
                oxygenRecoveredFeedback?.PlayFeedbacks();
            }
        }

        /// <summary>
        /// 되살아난다. 체력과 산소를 가득 채우고 사망 걸쇠를 그 자리에서 푼다.
        ///
        /// <b>왜 둘 다 채우는가.</b> 체력만 채우면 익사로 죽은 사람은 산소 0인 채로
        /// 되살아나 곧바로 다시 깎이기 시작한다 — 부활한 자리에서 몇 초 만에 또 죽는
        /// 고리가 된다. 부분 회복은 그 고리를 늘릴 뿐 끊지 못하므로, 잃은 것은
        /// 죽은 자리에 남은 가방이 대신 짊어지게 두고 몸은 온전히 돌려준다.
        ///
        /// 걸쇠를 여기서 직접 푸는 이유는 <see cref="Update"/>가 다음 프레임에 풀어 주기를
        /// 기다리면 그 한 프레임 동안 <see cref="Died"/>가 다시 걸릴 여지가 남기 때문이다.
        ///
        /// <b>수분과 식량은 채우지 않는다 (2026-08-07).</b> 그 둘은 죽음과 아무 상관이
        /// 없다 — 바닥을 쳐도 죽지 않으므로(<see cref="Sustenance"/>) 「되살아났으니
        /// 배가 부르다」로 이을 근거가 없다. 게다가 되살아나는 자리는 화톳불이고
        /// 거점은 호수 옆이라, 채워야 한다면 <b>걸어가서 채우면 된다.</b> 여기서
        /// 채워 버리면 죽음이 보급이 되어 「돌아갈 이유」가 죽음으로 대체된다.
        /// </summary>
        public void Revive()
        {
            Health.Modify(Health.Max);
            Oxygen.Modify(Oxygen.Max);
            _deathReported = false;
        }

        public float CurrentOxygenRate()
        {
            float baseRate = oxygenDefinition != null ? oxygenDefinition.passiveRatePerSecond : -1f;
            return OxygenRate.Calculate(baseRate, _oxygenModifiers);
        }

        public void RegisterOxygenModifier(IOxygenModifier modifier)
        {
            if (modifier != null && !_oxygenModifiers.Contains(modifier))
                _oxygenModifiers.Add(modifier);
        }

        public void UnregisterOxygenModifier(IOxygenModifier modifier)
            => _oxygenModifiers.Remove(modifier);
    }
}
