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
    public class LanternController : MonoBehaviour, IOffsetLitSource
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

        // 램프를 매 프레임 앞으로 옮기므로, 밀기 전의 자리를 따로 기억해 둔다.
        // 램프 자신의 위치를 기준으로 다시 밀면 프레임마다 오프셋이 누적되어
        // 빛이 지평선까지 달아난다.
        Transform _lampPivot;
        Vector3 _lampRestLocal;
        Transform _body;

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

        // ── IOffsetLitSource ─────────────────────────────────────
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
        // 판정은 여전히 방향을 보지 않는 구(球)다. 달라진 것은 그 구의 중심이
        // 사람이 아니라 사람보다 조금 앞에 있다는 것뿐이다(LanternRule.LitCenter).

        /// <summary>
        /// 빛 웅덩이가 매달린 자리. 램프를 앞으로 밀기 <b>전</b>의 자리다.
        ///
        /// 램프의 현재 위치를 읽지 않는다 — 그것은 이미 밀려 있는 값이라,
        /// 그 위에 또 밀면 프레임마다 오프셋이 쌓인다.
        /// </summary>
        public Vector3 LitAnchor =>
            _lampPivot != null ? _lampPivot.TransformPoint(_lampRestLocal)
                               : (lampLight != null ? lampLight.transform.position : transform.position);

        /// <summary>
        /// 빛을 미는 쪽. <b>몸의 정면을 수평으로 눕힌 것</b>이다 — 자세한 근거는
        /// <see cref="ResolveBody"/>에 있다.
        /// </summary>
        public Vector3 LitForward =>
            LanternRule.Facing(_body != null ? _body.forward : transform.forward);

        public Vector3 LitZoneCenter =>
            LanternRule.LitCenter(LitAnchor, LitForward, LanternRule.OffsetForTier(_tier));

        public float LitZoneRadius => LanternRule.RadiusForTier(_tier);
        bool ILitZoneSource.IsLit => IsOn;

        /// <summary>
        /// 실제 광원. <b>검증이 화면과 판정이 같은 말을 하는지 보려고 읽는다.</b>
        /// 램프는 이 컴포넌트의 자식이 아니라 몸에 매달려 있어서(Player.prefab),
        /// 밖에서 이름으로 찾아다니게 두면 프리팹을 손볼 때 조용히 끊어진다.
        /// </summary>
        public Light Lamp => lampLight;

        void Awake()
        {
            _battery = MaxBattery;
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (lampLight == null) lampLight = GetComponentInChildren<Light>(true);

            if (lampLight != null)
            {
                _lampPivot = lampLight.transform.parent;
                _lampRestLocal = lampLight.transform.localPosition;
            }
            ResolveBody();

            RefreshTier();
            ApplyLight();
        }

        /// <summary>
        /// 빛이 <b>무엇을 따라 도는가</b>를 정한다. 이 게임 감각이 갈리는 자리다.
        ///
        /// <b>몸을 따르게 했다. 정확히는 몸의 yaw만 따르고 고개의 위아래는 버린다.</b>
        ///
        /// <b>1) 이 리그에서는 시점과 몸이 애초에 같이 돈다.</b>
        /// <see cref="Survive.Player.PlayerCameraRig"/>는 좌우(yaw)를 <b>몸</b>에 쓰고
        /// 위아래(pitch)만 카메라에 쓴다. 그러니 "고개를 돌려 지킨다"와 "몸을 돌려야
        /// 한다"는 좌우로는 같은 동작이고, 실제로 갈리는 것은 <b>위아래를 반영할
        /// 것인가</b> 하나뿐이다.
        ///
        /// <b>2) 위아래를 반영하면 설계가 거꾸로 뒤집힌다.</b> 기획서 §9는
        /// <b>채집·제작이 위험 행동</b>이 되기를 바란다 — 멈춰서 대상을 봐야 하기
        /// 때문이다. 그런데 pitch를 따르게 하면 <b>발밑을 내려다보는 순간 빛 웅덩이가
        /// 제 발치로 끌려와 등 뒤 사각이 사라진다.</b> 위험해야 할 자세가 가장 안전한
        /// 자세가 된다. 반대로 하늘을 보면 웅덩이가 앞으로 달아나 제 발밑이 캄캄해진다.
        /// 둘 다 플레이어가 배울 수 없는 규칙이다.
        ///
        /// <b>3) 사각은 발로 도망치는 것이지 눈으로 도망치는 것이 아니다.</b> 사각의
        /// 크기가 시선의 각도마다 달라지면 낫이 언제 파고들 수 있는지 읽을 수 없고,
        /// 그러면 "뒤를 확인한다"가 기술이 아니라 운이 된다.
        ///
        /// 램프가 <b>몸의 자식</b>으로 매달려 있는 것(Player.prefab의 LampLight)도 같은
        /// 말을 이미 하고 있다. 우리는 그 결에 오프셋만 얹는다.
        /// </summary>
        void ResolveBody()
        {
            // 이 컴포넌트는 카메라 아래에 달려 있어 제 transform이 위아래로 기운다.
            // 몸은 PlayerContext가 붙은 뿌리이고, 램프가 매달린 곳과 같은 자리다.
            var context = GetComponentInParent<Survive.Player.PlayerContext>();
            _body = context != null ? context.transform : _lampPivot;
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

        /// <summary>램프를 지금 상태에 맞춘다. 켜짐 여부·자리·반경·밝기가 전부 여기서 나온다.</summary>
        void ApplyLight()
        {
            if (lampLight == null) return;

            bool on = IsOn;
            lampLight.enabled = on;
            if (!on)
            {
                // 꺼졌으면 제자리로 되돌린다. 밀린 채로 남겨 두면 다시 켜지는
                // 프레임에 빛이 엉뚱한 데서 시작한다.
                lampLight.transform.localPosition = _lampRestLocal;
                _flicker?.Kill();
                _flicker = null;
                _warning = false;
                return;
            }

            // <b>실제 광원도 함께 민다.</b> 판정만 옮기고 보이는 빛을 두면 화면과
            // 규칙이 다른 말을 하게 되고, 그러면 플레이어는 등 뒤가 어둡다는 것을
            // 눈으로 배울 길이 없다 — 맞고 나서야 알게 되는데 그것이 곧 억울함이다.
            if (_body != null || _lampPivot != null)
                lampLight.transform.position = LitZoneCenter;

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
