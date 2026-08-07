using System.Collections;
using UnityEngine;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Survive.Audio;
using Survive.Core;
using Survive.Crafting;
using Survive.Domain.Audio;
using Survive.Interaction;
using Survive.Items;
using Survive.Localization;
using Survive.Player;
using Survive.World;

namespace Survive.Building
{
    /// <summary>
    /// 화톳불. 거점을 표시하고, 랜턴을 채우고, 연료를 먹는다.
    ///
    /// 지하는 어둡고 랜턴 배터리는 유한하다. 발광 버섯 군락까지 매번 돌아가는 대신
    /// 자기 거점을 만들 수 있어야 건설에 이유가 생긴다 —
    /// 화톳불은 "여기가 내 자리다"를 세우는 첫 물건이다.
    ///
    /// 연료를 계속 넣어야 꺼지지 않는다. 켜 두면 알아서 되는 것이면
    /// 거점이 아니라 배경이 된다. 그 연료는 <b>버섯 목재</b>다 — 세계에서
    /// 유일하게 타는 물질이고, 그것을 얻으려면 거대 버섯을 베러 나가야 한다.
    /// 불을 지키는 일이 곧 밖으로 나가는 이유가 된다.
    ///
    /// <b>목재가 가장 많이 들어가는 곳이 여기다</b>(기획서 §5.3). 불이 꺼지면
    /// 이 자리는 <see cref="Survive.World.LitZoneRegistry"/>에서 빠져 밝은
    /// 구역이 사라지고, 부활 지점 후보에서도 빠진다. 그래서 목재 재고가
    /// 곧 안전 재고다. 수치는 전부
    /// <see cref="CampfireFuelRule"/> 한 곳에 있다 — 여기에는 사본을 두지 않는다.
    ///
    /// 그리고 이 불은 <b>에너지 추출기</b>이기도 하다. 스크랩은 타는 물건이 아니라
    /// 에너지를 <b>담고 있는</b> 매체다 — 불의 열은 그 안에 갇힌 것을 끌어내어
    /// 배터리 셀로 옮기는 손이다. 넣어 두고 떠났다가 셀을 받아 간다.
    /// 불이 꺼지면 추출도 멈춘다. 불을 지키는 일이 곧 생산을 지키는 일이 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class Campfire : MonoBehaviour, IInteractable, ILitZoneSource, ICraftStation
    {
        [SerializeField] Light flame;

        // ── 연료 ─────────────────────────────────────────────────
        // 직렬화 필드가 아니다. 프리팹에 사본을 두면 CampfireFuelRule의 상수를
        // 돌려도 게임이 바뀌지 않는다 — 실제로 그랬다(프리팹의 maxFuel 180이
        // 상수를 덮고 있었고, 프리팹은 병합할 수 없어 손대기도 어렵다).
        // 목재 밸런스는 아직 확정이 아니므로(상세기획서 §13) 돌릴 자리가
        // 한 곳이어야 한다. 그 한 곳이 CampfireFuelRule이다.
        static float MaxFuel => CampfireFuelRule.MaxFuelSeconds;
        static float FuelPerLog => CampfireFuelRule.SecondsPerLog;
        static int LogsPerRefuel => CampfireFuelRule.LogsPerRefuel;

        [Header("빛")]
        [SerializeField] float fullIntensity = 1.9f;
        [SerializeField] float fullRange = 10f;

        [Tooltip("불빛이 일렁이는 폭")]
        [SerializeField] float flickerAmount = 0.18f;

        [Header("랜턴 충전")]
        [Tooltip("불 곁에 있으면 랜턴이 초당 이만큼 찬다")]
        [SerializeField] float lanternRechargePerSecond = 8f;

        [SerializeField] float warmthRadius = 6f;

        [Header("피드백")]
        [SerializeField] MMF_Player refuelFeedback;

        [Header("소리")]
        [Tooltip("꺼진 불을 다시 지필 때 한 번. 비우면 소리 표의 campfireIgnite")]
        [SerializeField] AudioCueSO igniteCue;

        [Tooltip("타는 동안 계속. loop를 켠 cue라야 한다. 비우면 소리 표의 campfireLoop")]
        [SerializeField] AudioCueSO burningCue;

        float _fuel;
        Tween _flicker;
        AudioHandle _burning;

        readonly StationCraftQueue _work = new StationCraftQueue();

        public bool IsBurning => CampfireFuelRule.IsBurning(_fuel);
        public float FuelNormalized => MaxFuel <= 0f ? 0f : Mathf.Clamp01(_fuel / MaxFuel);

        /// <summary>남은 연료(초). 표와 검사가 "목재 N개 → T초"를 읽는 창구다.</summary>
        public float FuelSeconds => _fuel;

        /// <summary>
        /// 마지막으로 <b>불이 붙은</b> 세계 시각(<see cref="Survive.World.WorldClock"/>).
        ///
        /// <b>프레임 시계로 재지 않는다.</b> 이 값이 하는 일은 여러 화톳불 중
        /// 「마지막 것」을 고르는 비교인데, 프레임 시계는 씬 로드로 0이 된다 —
        /// 씬을 갈아 끼운 뒤에 지핀 불이 그 전에 지핀 불보다 <b>이르게</b> 보이고,
        /// 부활 지점이 엉뚱한 자리로 간다. 세계 시계는 씬을 건너서도 자란다.
        ///
        /// 세운 시각이 아니다 — 꺼졌다가 다시 지핀 불은 그때가 마지막이다.
        /// 부활 지점을 고르는 <see cref="Survive.World.RespawnRule"/>이 "마지막 화톳불"을
        /// 이 값으로 가린다. 사람이 마지막으로 머문 자리는 마지막으로 세운 곳이 아니라
        /// 마지막으로 불을 살린 곳이다.
        /// </summary>
        public float KindledAt { get; private set; }

        // ── ILitZoneSource ───────────────────────────────────────
        // 다른 시스템(P4의 습격 AI 등)이 "여기가 밝은가"를 물을 수 있도록
        // LitZoneRegistry에 자신을 내놓는다. 반경은 실제 빛이 닿는 fullRange를
        // 그대로 쓴다 — 게임플레이상 "밝다"와 눈에 보이는 빛이 따로 놀면
        // 플레이어가 판단할 수 없다.
        public Vector3 LitZoneCenter => flame != null ? flame.transform.position : transform.position;
        public float LitZoneRadius => fullRange;
        bool ILitZoneSource.IsLit => IsBurning;

        void Awake()
        {
            if (flame == null) flame = GetComponentInChildren<Light>(true);

            // 세우자마자 한 번은 타야 한다. 지어 놓고 연료부터 넣으라고 하면
            // 무엇을 지은 건지 알 수 없다. 다만 이것은 <b>불씨</b>이지 한 번 넣은
            // 몫이 아니다 — 용량의 비율로 두면 용량을 올릴 때마다 공짜 목재가
            // 함께 늘어난다(CampfireFuelRule.StarterLogs).
            _fuel = CampfireFuelRule.StarterFuelSeconds;
            ApplyLight();
        }

        void OnEnable() => LitZoneRegistry.Register(this);

        // 비활성화·철거(Destroy)·씬 언로드 모두 OnDisable을 거친다 —
        // 등록 해제를 여기 한 곳에만 두면 셋 다 저절로 해결된다.
        void OnDisable()
        {
            LitZoneRegistry.Unregister(this);

            // 꺼진 불에서 타는 소리가 계속 나면 안 된다. 다시 켜지면 ApplyLight가
            // 같은 자리에서 다시 건다.
            StopBurningLoop();
        }

        void Update()
        {
            if (_fuel > 0f)
            {
                _fuel = CampfireFuelRule.AfterBurn(_fuel, Time.deltaTime);
                if (_fuel <= 0f) ApplyLight();
            }

            if (IsBurning) WarmNearbyLantern();

            // 추출은 불이 살아 있는 동안에만 흐른다. 꺼지면 진행도를 그대로 둔 채
            // 멈춘다 — 되살리면 이어서 뽑아낸다.
            if (!_work.Queue.IsEmpty) _work.Tick(Time.deltaTime, IsBurning);
        }

        void WarmNearbyLantern()
        {
            if (!GameServices.TryGet<LanternController>(out var lantern)) return;
            if (lantern == null) return;

            float d = Vector3.Distance(transform.position, lantern.transform.position);
            if (d > warmthRadius) return;

            lantern.Recharge(lanternRechargePerSecond * Time.deltaTime);
        }

        void ApplyLight()
        {
            // 불이 붙은 순간을 여기서 적는다. ApplyLight는 처음 세울 때, 연료가 다 탔을 때,
            // 다시 지필 때 — 불의 상태가 바뀌는 모든 지점에서 불린다. 한 곳에 두면
            // 나중에 점화 경로가 하나 더 생겨도 저절로 따라온다.
            if (IsBurning) KindledAt = WorldClock.Seconds;

            // 타는 소리도 불의 상태에 붙는다. 여기 한 곳에 두면 꺼짐·되살림·철거가
            // 전부 같은 줄을 지난다. 소리가 없으면 무효한 손잡이가 돌아오고,
            // 그것을 멈추라고 해도 아무 일도 일어나지 않는다.
            ApplyBurningLoop();

            if (flame == null) return;

            flame.enabled = IsBurning;
            _flicker?.Kill();

            if (!IsBurning) return;

            flame.range = fullRange;
            flame.intensity = fullIntensity;

            // 일렁임. 고정된 밝기는 불로 안 보인다.
            _flicker = flame.DOIntensity(fullIntensity * (1f - flickerAmount), 0.35f)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetEase(Ease.InOutSine)
                            .SetLink(gameObject);
        }

        void OnDestroy()
        {
            _flicker?.Kill();
            StopBurningLoop();
        }

        /// <summary>불이 살아 있으면 타는 소리를 걸고, 꺼졌으면 거둔다.</summary>
        void ApplyBurningLoop()
        {
            if (!IsBurning) { StopBurningLoop(); return; }
            if (AudioService.IsPlaying(_burning)) return;

            var book = AudioService.Book;
            _burning = AudioService.PlayLoop(
                AudioCueBookSO.Or(burningCue, book != null ? book.campfireLoop : null),
                transform);
        }

        void StopBurningLoop()
        {
            AudioService.StopLoop(_burning);
            _burning = AudioHandle.None;
        }

        // ── 에너지 추출 스테이션 ─────────────────────────────────

        public StationType StationType => StationType.Campfire;
        public string StationName => Loc.T("Build", "campfire_name");
        public StationCraftQueue Work => _work;
        public bool IsPowered => IsBurning;

        public string PausedReason => IsBurning ? null : Loc.T("Build", "campfire_paused");

        /// <summary>
        /// 추출 화면에서 바로 불을 살린다.
        ///
        /// 연료 보급은 제작이 아니라서 레시피로 적을 수 없지만(만들어지는 물건이 없다),
        /// 불 앞에 선 사람이 가장 자주 하는 일이다. 화면을 닫았다 다시 열게 하면
        /// 추출을 걸어 놓고 불을 살리는 한 동작이 두 동작이 된다.
        /// </summary>
        public StationSideAction SideAction => _sideAction ?? (_sideAction = new StationSideAction(
            () =>
            {
                int pct = Mathf.RoundToInt(FuelNormalized * 100f);
                // 상한이 아니라 <b>이번에 실제로 들어갈 수</b>를 적는다. 가득 찬 불
                // 앞에서 "목재 5"라고 말해 놓고 아무것도 안 들어가면 거짓말이 된다.
                return Loc.F("Build", "campfire_refuel", LogsToTake(), pct);
            },
            () => LogsToTake() > 0,
            () => Refuel(PlayerBag())));

        /// <summary>이번에 넣을 수 있는 목재 수. 가진 것과 남은 자리 둘 다 본다.</summary>
        int LogsToTake() =>
            CampfireFuelRule.LogsToTake(PlayerWood(), LogsPerRefuel, _fuel, FuelPerLog, MaxFuel);

        StationSideAction _sideAction;

        static Inventory PlayerBag() =>
            GameServices.TryGet<PlayerInventory>(out var pi) && pi != null ? pi.Inventory : null;

        static int PlayerWood()
        {
            var bag = PlayerBag();
            return bag != null ? bag.CountOf(CampfireFuelRule.FuelItemId) : 0;
        }

        /// <summary>
        /// 불을 살린다. 들어가는 것은 <b>버섯 목재뿐</b>이다.
        ///
        /// 스크랩은 태우는 물건이 아니라 에너지를 담고 있는 매체다 —
        /// 이 불 앞에서 스크랩이 하는 일은 타는 것이 아니라 열에 짜여
        /// 배터리 셀로 옮겨 가는 것이다(<see cref="StationCraftQueue"/>).
        /// 태울 것과 뽑아낼 것을 같은 물질로 두면 둘 중 무엇이 일어날지
        /// 플레이어가 예측할 수 없다.
        /// </summary>
        public bool Refuel(Inventory inv)
        {
            if (inv == null) return false;

            // 가진 만큼만, 그리고 들어갈 만큼만 넣는다. 자리보다 많이 받으면
            // 상한에 잘려 배낭의 목재가 그대로 사라진다.
            int take = CampfireFuelRule.LogsToTake(inv.CountOf(CampfireFuelRule.FuelItemId),
                                                   LogsPerRefuel, _fuel, FuelPerLog, MaxFuel);
            if (take <= 0) return false;
            if (!inv.TryRemove(CampfireFuelRule.FuelItemId, take)) return false;

            bool wasOut = !IsBurning;
            _fuel = CampfireFuelRule.AfterRefuel(_fuel, take, FuelPerLog, MaxFuel);

            if (wasOut) ApplyLight();
            refuelFeedback?.PlayFeedbacks();

            // 점화음은 꺼져 있던 불을 살렸을 때만 난다. 이미 타는 불에 목재를 더
            // 넣은 것은 점화가 아니다 — 그때까지 불붙는 소리가 나면 무슨 일이
            // 일어났는지가 소리와 어긋난다.
            if (wasOut)
            {
                var book = AudioService.Book;
                AudioService.Play(
                    AudioCueBookSO.Or(igniteCue, book != null ? book.campfireIgnite : null),
                    transform.position);
            }

            return true;
        }

        // ── 저장본에서 되살아날 때 ───────────────────────────────

        /// <summary>
        /// 저장본이 적어 둔 불의 상태를 앉힌다. 생성 목록이 몸을 다시 세운
        /// 직후에 부른다 (<c>Survive.World.SpawnLedgerStage</c>).
        ///
        /// <b><see cref="KindledAt"/>을 함께 받는 이유.</b> 이 값은 여러 불 중
        /// 「마지막 것」을 고르는 비교에 쓰이고(<c>RespawnRule</c>), 안 받으면
        /// <see cref="ApplyLight"/>가 방금을 적는다 — 그러면 불러온 세계의
        /// 모든 불이 <b>같은 순간에 지펴진 것</b>이 되어 부활 지점이 아무 데나
        /// 잡힌다. 사람이 마지막으로 머문 자리가 저장을 못 건너는 것이다.
        ///
        /// <see cref="ApplyLight"/>가 그 값을 지금으로 덮으므로 <b>뒤에서</b>
        /// 되돌린다. 순서를 뒤집으면 아무 일도 일어나지 않는다.
        /// </summary>
        public void Adopt(float fuelSeconds, float kindledAt)
        {
            _fuel = Mathf.Clamp(fuelSeconds, 0f, MaxFuel);
            ApplyLight();
            KindledAt = kindledAt;
        }

        // ── 상호작용 ─────────────────────────────────────────────

        /// <summary>
        /// E 하나가 세 가지 일을 나눠 맡는다. 순서는 급한 것부터다 —
        /// 다 뽑아낸 것을 가져가고, 꺼진 불을 살리고, 그 다음이 추출 화면이다.
        ///
        /// 불을 살리는 것을 화면 안에만 두지 않은 이유: 어두운 데서 불이 꺼지면
        /// 창을 열어 버튼을 찾을 겨를이 없다. 그 순간의 E는 무조건 불이어야 한다.
        /// </summary>
        public string InteractionPrompt
        {
            get
            {
                if (_work.HasOutput)
                    return Loc.F("Build", "campfire_prompt_collect", _work.OutputCount);
                if (!IsBurning) return Loc.T("Build", "campfire_prompt_ignite");

                int pct = Mathf.RoundToInt(FuelNormalized * 100f);
                if (!_work.Queue.IsEmpty)
                {
                    float left = CraftQueueService.TotalSecondsLeft(_work.Queue);
                    return Loc.F("Build", "campfire_prompt_busy", CraftTimeText.Short(left), pct);
                }
                return Loc.F("Build", "campfire_prompt_idle", pct);
            }
        }

        /// <summary>
        /// 목재가 없어도 다가설 수 있어야 한다 — 뽑아 놓은 것을 가지러 오거나
        /// 추출을 걸러 오는 사람도 있다. 불을 지필 때만 목재가 필요하다.
        /// </summary>
        public bool CanInteract(PlayerContext player)
        {
            var inv = player?.Inventory?.Inventory;
            if (inv == null) return false;
            if (_work.HasOutput) return true;
            if (!IsBurning) return inv.Has(CampfireFuelRule.FuelItemId, 1);
            return true;
        }

        public void Interact(PlayerContext player)
        {
            var inv = player?.Inventory?.Inventory;
            if (inv == null) return;

            if (_work.HasOutput && _work.CollectInto(inv) > 0) return;

            // 꺼진 불 앞에서 E는 언제나 "불을 살린다"다.
            if (!IsBurning) { Refuel(inv); return; }

            var ui = UnityEngine.Object.FindAnyObjectByType<Survive.UI.CraftingUI>(FindObjectsInactive.Include);
            if (ui != null) ui.Open(this);
            else Debug.LogWarning("[Campfire] CraftingUI를 찾지 못했습니다.", this);
        }
    }
}
