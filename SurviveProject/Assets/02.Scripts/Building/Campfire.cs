using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Survive.Audio;
using Survive.Core;
using Survive.Crafting;
using Survive.Domain.Audio;
using Survive.Interaction;
using Survive.Items;
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
    /// 그리고 이 불은 <b>에너지 추출기</b>이기도 하다. 스크랩은 타는 물건이 아니라
    /// 에너지를 <b>담고 있는</b> 매체다 — 불의 열은 그 안에 갇힌 것을 끌어내어
    /// 배터리 셀로 옮기는 손이다. 넣어 두고 떠났다가 셀을 받아 간다.
    /// 불이 꺼지면 추출도 멈춘다. 불을 지키는 일이 곧 생산을 지키는 일이 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class Campfire : MonoBehaviour, IInteractable, ILitZoneSource, ICraftStation
    {
        [SerializeField] Light flame;

        [Header("연료")]
        [Tooltip("가득 찼을 때의 연료. 초 단위로 탄다")]
        [SerializeField] float maxFuel = CampfireFuelRule.MaxFuelSeconds;

        // 이름이 바뀌어도 이미 저장된 프리팹 값을 잃지 않게 옛 이름을 적어 둔다.
        // 프리팹은 병합할 수 없는 파일이라 이 작업에서 손대지 않는다.
        [Tooltip("목재 하나가 주는 연료(초)")]
        [FormerlySerializedAs("fuelPerScrap")]
        [SerializeField] float fuelPerLog = CampfireFuelRule.SecondsPerLog;

        [Tooltip("한 번에 넣는 목재 수")]
        [FormerlySerializedAs("scrapPerRefuel")]
        [SerializeField] int logsPerRefuel = CampfireFuelRule.LogsPerRefuel;

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
        public float FuelNormalized => maxFuel <= 0f ? 0f : Mathf.Clamp01(_fuel / maxFuel);

        /// <summary>
        /// 마지막으로 <b>불이 붙은</b> 시각(<see cref="Time.time"/>).
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
            // 무엇을 지은 건지 알 수 없다.
            _fuel = maxFuel * 0.5f;
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
            if (IsBurning) KindledAt = Time.time;

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
        public string StationName => "화톳불";
        public StationCraftQueue Work => _work;
        public bool IsPowered => IsBurning;

        public string PausedReason => IsBurning ? null : "불이 꺼져 에너지 추출이 멈췄다";

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
                return $"연료 넣기 (버섯 목재 {logsPerRefuel})  ·  연료 {pct}%";
            },
            () => PlayerWood() > 0,
            () => Refuel(PlayerBag())));

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

            // 가진 만큼만 넣는다.
            int take = CampfireFuelRule.LogsToTake(inv.CountOf(CampfireFuelRule.FuelItemId),
                                                   logsPerRefuel);
            if (take <= 0) return false;
            if (!inv.TryRemove(CampfireFuelRule.FuelItemId, take)) return false;

            bool wasOut = !IsBurning;
            _fuel = CampfireFuelRule.AfterRefuel(_fuel, take, fuelPerLog, maxFuel);

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
                if (_work.HasOutput) return $"[E] 화톳불에서 {_work.OutputCount}개 회수";
                if (!IsBurning) return "[E] 화톳불 지피기";

                int pct = Mathf.RoundToInt(FuelNormalized * 100f);
                if (!_work.Queue.IsEmpty)
                {
                    float left = CraftQueueService.TotalSecondsLeft(_work.Queue);
                    return $"[E] 화톳불 (에너지 추출 중 {CraftTimeText.Short(left)}, 연료 {pct}%)";
                }
                return $"[E] 화톳불 (연료 {pct}%)";
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
