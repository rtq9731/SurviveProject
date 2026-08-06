using System;
using UnityEngine;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Survive.Core;
using Survive.Items;

namespace Survive.World
{
    /// <summary>
    /// 랜턴. 지하는 빛이 거의 공급되지 않으므로(세계관) 이것이 챕터 1의 핵심 압박이다.
    ///
    /// <b>끄는 길이 없다.</b> 랜턴을 가지고 있으면 켜져 있고, 배터리가 있는 동안
    /// 계속 닳는다. 예전에는 F로 켜고 껐다 — 그 스위치가 있으면 최적해가
    /// "어두운 데서는 꺼 두고 필요할 때만 켠다"가 되어, 어둠이 <b>비용</b>이 아니라
    /// <b>가끔 내는 요금</b>이 된다. 스위치를 없앤 대신 반경 티어라는 선택을
    /// 돌려주었다. 규칙과 수치는 전부 <see cref="LanternRule"/> 한 곳에 있다 —
    /// 여기에는 사본을 두지 않는다.
    ///
    /// 배터리가 다하면 불이 꺼지고 반경 밖과 같아진다. 그것이 이 게임의 유일한
    /// "꺼짐"이고, 되살리는 방법은 셋이다 — 발광 버섯 군락(무료, 거점),
    /// 화톳불 곁(무료, 거점), 배터리 셀(현장, 대가).
    /// </summary>
    [DisallowMultipleComponent]
    public class LanternController : MonoBehaviour, ILitZoneSource
    {
        [SerializeField] Light lampLight;
        [SerializeField] PlayerInventory inventory;

        // ── 배터리·빛 ────────────────────────────────────────────
        // 직렬화 필드가 아니다. 프리팹에 사본을 두면 LanternRule의 상수를 돌려도
        // 게임이 바뀌지 않는다 — 화톳불에서 실제로 그랬고(프리팹의 maxFuel 180이
        // 상수를 덮고 있었다), 프리팹은 병합할 수 없어 손대기도 어렵다.
        // 랜턴 반경·초당 소모는 아직 확정이 아니므로(기획서 §9 튜닝 3값)
        // 돌릴 자리가 한 곳이어야 한다. 그 한 곳이 LanternRule이다.
        static float MaxBattery => LanternRule.MaxBattery;
        static float BatteryPerCell => LanternRule.BatteryPerCell;

        [Header("피드백")]
        [Tooltip("배터리 부족 경고 시 재생")]
        [SerializeField] MMF_Player lowBatteryFeedback;

        [Tooltip("스크랩으로 충전할 때 재생")]
        [SerializeField] MMF_Player rechargeFeedback;

        float _battery;
        int _tier;
        bool _warning;
        Tween _flicker;

        public float Battery => _battery;
        public float BatteryNormalized => MaxBattery <= 0f ? 0f : _battery / MaxBattery;

        /// <summary>
        /// 지금 걸려 있는 랜턴의 티어. 0이면 아직 랜턴이 없다.
        /// 매 프레임 인벤토리에서 다시 읽는다 — <see cref="PlayerInventory.RestoreState"/>가
        /// <see cref="Inventory"/> 객체 자체를 갈아 끼우므로 변경 이벤트를 붙들고 있으면
        /// 불러오기 뒤에 옛 것을 보게 된다(PlayerTraversalGear가 같은 이유로 같은 선택을 했다).
        /// </summary>
        public int Tier => _tier;

        /// <summary>랜턴을 가지고 있는가. 화면(배터리 눈금)이 "보일 때"를 이것으로 정한다.</summary>
        public bool HasLantern => _tier > 0;

        /// <summary>
        /// 불이 들어와 있는가. <b>조작이 아니라 상태다</b> —
        /// 가졌는가와 남았는가만 본다(<see cref="LanternRule.IsLit"/>).
        /// </summary>
        public bool IsOn => LanternRule.IsLit(_tier, _battery);

        public event Action<float, float> BatteryChanged;   // (현재, 최대)

        // ── ILitZoneSource ───────────────────────────────────────
        // 화톳불(Survive.Building.Campfire)이 이미 같은 방식으로 자신을 내놓는다.
        // 랜턴도 광원이므로 같은 창구로 조회되어야 한다 — 그래야 "여기가 밝은가"를
        // 묻는 쪽이 화톳불인지 랜턴인지 알 필요가 없다. 지금 이것을 묻는 것은
        // 빛을 꺼리는 소비자(Survive.Creatures.CreatureBrain)다.
        //
        // 반경은 티어 반경을 그대로 쓴다. 배터리 경고 때 intensity가 트윈으로
        // 흔들리지만 여기에는 반영하지 않는다 — 판정 반경까지 깜빡임에 맞춰
        // 요동치면 포식자가 깜빡임 주기로 붙었다 떨어졌다 하고, 플레이어는
        // 무엇이 자신을 지켜 주는지 읽을 수 없게 된다. 켜졌는가/꺼졌는가만 본다.
        //
        // 중심은 램프의 자리다. 실제 Light 컴포넌트는 프리팹에서 Spot이지만
        // (Player.prefab의 lampLight, m_Type 0), 판정은 방향을 보지 않는 구(球)다 —
        // 이 게임의 랜턴은 전방위 빛 웅덩이로 다루기로 확정됐다.
        public Vector3 LitZoneCenter => lampLight != null ? lampLight.transform.position : transform.position;
        public float LitZoneRadius => LanternRule.RadiusForTier(_tier);
        bool ILitZoneSource.IsLit => IsOn;

        void Awake()
        {
            _battery = MaxBattery;
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (lampLight == null) lampLight = GetComponentInChildren<Light>(true);

            RefreshTier();
            ApplyLight();
        }

        void OnEnable()
        {
            GameServices.Register(this);
            LitZoneRegistry.Register(this);
        }

        void OnDisable()
        {
            GameServices.Unregister<LanternController>();
            LitZoneRegistry.Unregister(this);
        }

        void OnDestroy() => _flicker?.Kill();

        void RefreshTier() => _tier = LanternRule.EquippedTier(inventory?.Inventory);

        void Update()
        {
            RefreshTier();

            // 랜턴이 아직 없다. 어둠을 그대로 견디는 구간이고, 배터리는 닳지 않는다 —
            // 만들기도 전에 시계가 도는 것은 압박이 아니라 버그다.
            if (_tier <= 0)
            {
                ApplyLight();
                return;
            }

            float prev = _battery;
            _battery = LanternRule.AfterDrain(_battery, _tier, Time.deltaTime);
            if (!Mathf.Approximately(prev, _battery)) BatteryChanged?.Invoke(_battery, MaxBattery);

            if (_battery <= 0f)
            {
                // 여분 셀을 챙겨 왔다면 여기서 갈아 끼운다. 어둠 속에서 창을 열어
                // 버튼을 찾게 하는 대신, "몇 개를 들고 갈 것인가"를 출발 전의 결정으로
                // 만든다 — 배터리가 시계라는 규칙은 그대로다.
                if (TryInsertBatteryCell()) return;

                // 갈아 끼울 것이 없으면 꺼진다. 이것이 이 게임의 유일한 "꺼짐"이고,
                // 플레이어가 고른 것이 아니라 <b>비용을 다 쓴 결과</b>다.
                ApplyLight();
                return;
            }

            RefreshWarning();
            ApplyLight();
        }

        void RefreshWarning()
        {
            bool danger = LanternRule.IsWarning(_tier, _battery);

            if (danger && !_warning)
            {
                _warning = true;
                lowBatteryFeedback?.PlayFeedbacks();

                // 꺼지기 직전의 깜빡임. 남은 배터리를 눈으로 알 수 있게 한다.
                if (lampLight != null)
                {
                    _flicker?.Kill();
                    _flicker = lampLight.DOIntensity(LanternRule.Intensity * 0.35f, 0.18f)
                                        .SetLoops(-1, LoopType.Yoyo)
                                        .SetEase(Ease.InOutQuad);
                }
            }
            else if (!danger && _warning)
            {
                _warning = false;
                lowBatteryFeedback?.StopFeedbacks();
                _flicker?.Kill();
                _flicker = null;
            }
        }

        /// <summary>램프를 지금 상태에 맞춘다. 켜짐 여부·반경·밝기가 전부 여기서 나온다.</summary>
        void ApplyLight()
        {
            if (lampLight == null) return;

            bool on = IsOn;
            lampLight.enabled = on;
            if (!on)
            {
                _flicker?.Kill();
                _flicker = null;
                _warning = false;
                return;
            }

            lampLight.range = LanternRule.RadiusForTier(_tier);
            if (_warning) return;                     // 깜빡이는 동안 밝기는 트윈에 맡긴다
            lampLight.intensity = LanternRule.Intensity;
        }

        /// <summary>발광 버섯 군락·화톳불 등에서 채운다.</summary>
        public void Recharge(float amount)
        {
            if (amount <= 0f) return;
            float prev = _battery;
            _battery = LanternRule.AfterRecharge(_battery, amount);
            if (Mathf.Approximately(prev, _battery)) return;

            BatteryChanged?.Invoke(_battery, MaxBattery);
            ApplyLight();
        }

        /// <summary>
        /// 배터리를 이만큼 쓴다. <see cref="Recharge"/>의 짝이다.
        ///
        /// <b>이것은 스위치가 아니다.</b> 끄는 입력이 없는 대신, "불이 없는 상태"에
        /// 이르는 길은 배터리를 다 쓰는 것 하나뿐이다. 세계가 배터리를 더 먹이는
        /// 장치(스펙 §5 경계 상태 등)와 검증이 그 하나뿐인 길을 지나야 하므로
        /// 창구를 열어 둔다.
        /// </summary>
        public void Drain(float amount)
        {
            if (amount <= 0f) return;
            float prev = _battery;
            _battery = Mathf.Max(0f, _battery - amount);
            if (Mathf.Approximately(prev, _battery)) return;

            BatteryChanged?.Invoke(_battery, MaxBattery);
            ApplyLight();
        }

        /// <summary>
        /// 배터리 셀을 하나 끼운다.
        ///
        /// 예전에는 스크랩을 그 자리에서 배터리로 바꿔 넣었다(1개당 20). 스크랩은
        /// 에너지를 <b>담고 있는</b> 매체이지 태우는 물건이 아니고, 담긴 것을 꺼내려면
        /// 열이 든다 — 그래서 이제는 화톳불 앞에서 시간을 들여 셀로 옮긴다.
        /// 현장에서 즉시 해결되던 것이 거점으로 돌아갈 이유가 되었다.
        /// 셀 하나가 채우는 양(<see cref="LanternRule.BatteryPerCell"/>)은 그 레시피가
        /// 먹는 스크랩 수 × 20으로 맞춰 두었으므로 총량은 그대로다.
        /// </summary>
        public bool TryInsertBatteryCell(int cells = 1)
        {
            if (inventory?.Inventory == null || cells <= 0) return false;
            if (_battery >= MaxBattery) return false;
            if (!inventory.Inventory.TryRemove(BatteryCellId, cells)) return false;

            Recharge(BatteryPerCell * cells);
            rechargeFeedback?.PlayFeedbacks();
            return true;
        }

        /// <summary>화톳불에서 뽑아내는 셀의 아이템 id.</summary>
        public const string BatteryCellId = "battery_cell";
    }
}
