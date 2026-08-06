using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Core;
using Survive.Items;
using Survive.Player;
using Survive.World;

namespace Survive.Instruments
{
    /// <summary>
    /// 레이더를 실제로 돌리는 얇은 껍데기 (챕터 1 스펙 §8-2).
    ///
    /// 규칙은 전부 <see cref="RadarScan"/>·<see cref="RadarDetection"/>에 있다. 여기 남는
    /// 것은 셋뿐이다 — 누가 갖고 있는가, 어디에 서 있는가, 전원을 어디서 빼는가.
    ///
    /// <b>씬을 고치지 않는다.</b> 스스로 선다 — <c>UnlockService</c>·<c>PickupFeedView</c>와
    /// 같은 관례다. 레이더는 판 전체의 상태이지 어느 씬의 물건이 아니다.
    ///
    /// <b>전원은 배터리 셀이다 — 랜턴과 같은 물건을 먹는다.</b> 통은 따로 두지만
    /// 넣는 것이 같으므로 다툼은 그대로다: 관측 한 번은 랜턴을 그만큼 못 켠다는 뜻이다.
    /// 랜턴의 통을 직접 뽑아 쓰지 않는 까닭은 <see cref="RadarItemSO.maxCharge"/>에 적어 두었다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RadarController : MonoBehaviour, ISaveable
    {
        /// <summary>
        /// 관측을 거는 키.
        ///
        /// F(랜턴) 바로 옆이다. 배터리를 먹는 장치 둘을 나란히 두면 손이 한 번에
        /// 외운다. 그리고 <b>WASD에서 멀다</b> — 움직이면 관측이 끊기는 장치라,
        /// 이동키 옆에 두면 잘못 눌러 배터리만 버리는 일이 잦아진다.
        /// </summary>
        public const Key ScanKey = Key.G;

        /// <summary>레이더 아이템의 id. 이것이 소지품에 없으면 아무 일도 없다.</summary>
        public const string ItemId = "radar";

        static RadarController _instance;
        public static RadarController Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("RadarController");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RadarController>();
        }

        // ── 상태 ─────────────────────────────────────────────────

        /// <summary>지금 돌고 있는(또는 방금 끝난) 관측. 없으면 null.</summary>
        public RadarScan Scan { get; private set; }

        /// <summary>마지막 관측이 잡아낸 것들. 끊겼으면 비어 있다.</summary>
        public IReadOnlyList<RadarContact> Readings => _readings;

        /// <summary>지금 관측 중인가. 화면과 검증 하네스가 본다.</summary>
        public bool IsScanning => Scan != null && Scan.IsRunning;

        /// <summary>마지막 관측이 실제로 먹은 배터리. 검증 하네스가 본다.</summary>
        public float LastDrawn { get; private set; }

        /// <summary>장치에 남은 전하. 셀을 끼워 채운다.</summary>
        public float Charge { get; private set; }

        /// <summary>마지막으로 관측을 걸며 끼운 셀의 수. 화면과 검증 하네스가 본다.</summary>
        public int LastCellsInserted { get; private set; }

        /// <summary>관측을 건 자리. 여기서 벗어나면 끊긴다.</summary>
        public Vector3 Origin { get; private set; }

        readonly List<RadarContact> _readings = new List<RadarContact>();

        PlayerContext _player;
        PlayerInventory _inventory;

        void OnEnable() => _instance = this;

        void OnDisable()
        {
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            Rebind();

            if (Scan != null && Scan.IsRunning) Advance(Time.deltaTime);

            var kb = Keyboard.current;
            if (kb == null) return;

            var key = kb[ScanKey];
            if (key != null && key.wasPressedThisFrame) Toggle();
        }

        void Rebind()
        {
            if (_player == null) _player = Object.FindAnyObjectByType<PlayerContext>(FindObjectsInactive.Exclude);
            if (_inventory == null) GameServices.TryGet<PlayerInventory>(out _inventory);
        }

        // ── 걸고 끊기 ────────────────────────────────────────────

        /// <summary>돌고 있으면 끊고, 아니면 건다.</summary>
        public void Toggle()
        {
            if (Scan != null && Scan.IsRunning) Scan.Abort();
            else Begin();
        }

        /// <summary>
        /// 관측을 건다. 레이더가 없거나 전원이 끝까지 갈 만큼 없으면 걸리지 않는다.
        ///
        /// 모자라면 <b>그 자리에서 셀을 끼운다.</b> 어둠 속에서 창을 열어 버튼을 찾게
        /// 하는 대신 출발 전에 몇 개를 챙길 것인가로 결정을 옮긴다 — 랜턴이 이미
        /// 같은 처방을 쓴다(<c>LanternController.TryInsertBatteryCell</c>).
        /// </summary>
        public bool Begin()
        {
            Rebind();

            var radar = HeldRadar();
            if (radar == null) return false;

            var origin = PlayerPosition();
            if (origin == null) return false;

            var scan = radar.NewScan();
            LastCellsInserted = TopUp(radar, scan.FullCost);

            if (!scan.Begin(Charge)) return false;

            Scan = scan;
            Origin = origin.Value;
            LastDrawn = 0f;
            _readings.Clear();
            return true;
        }

        /// <summary>
        /// 한 번 값을 치를 만큼 셀을 끼운다. 실제로 끼운 수를 돌려준다.
        ///
        /// 한 개씩 빼는 이유: 여러 개를 한 번에 빼려다 실패하면 몇 개가 있었는지를
        /// 다시 세야 한다. 한 개씩이면 실패한 그 자리가 곧 있는 것의 전부다.
        /// </summary>
        int TopUp(RadarItemSO radar, float cost)
        {
            var inv = _inventory != null ? _inventory.Inventory : null;
            if (inv == null) return 0;

            int inserted = 0;
            while (Charge < cost)
            {
                if (RadarPowerRule.CellsNeeded(Charge, cost, radar.chargePerCell) <= 0) break;
                if (!inv.TryRemove(LanternController.BatteryCellId, 1)) break;

                Charge = RadarPowerRule.AfterInserting(Charge, 1, radar.chargePerCell, radar.maxCharge);
                inserted++;
            }
            return inserted;
        }

        /// <summary>사람이 껐다.</summary>
        public void Abort() => Scan?.Abort();

        /// <summary>결과를 닫고 다음 관측을 걸 수 있게 한다.</summary>
        public void Dismiss()
        {
            Scan?.Reset();
            Scan = null;
            _readings.Clear();
        }

        /// <summary>
        /// 시간을 흘린다. 검증 하네스가 프레임을 기다리지 않고 부를 자리가 필요해
        /// 열어 둔다.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            if (Scan == null || !Scan.IsRunning) return;

            var here = PlayerPosition() ?? Origin;
            float moved = Vector3.Distance(new Vector3(here.x, 0f, here.z),
                                           new Vector3(Origin.x, 0f, Origin.z));

            float draw = Scan.Tick(deltaSeconds, moved, Charge);
            if (draw > 0f)
            {
                Charge = Mathf.Max(0f, Charge - draw);
                LastDrawn = Scan.Drawn;
            }

            if (Scan.State == RadarScanState.Complete) Finish();
        }

        void Finish()
        {
            _readings.Clear();

            var candidates = RadarContactRegistry.Candidates(Origin);
            _readings.AddRange(RadarDetection.Sweep(Scan.Band, candidates));
        }

        // ── 바깥과 닿는 자리 ─────────────────────────────────────

        /// <summary>지금 소지품에 든 레이더. 없으면 null.</summary>
        public RadarItemSO HeldRadar()
        {
            var inv = _inventory != null ? _inventory.Inventory : null;
            if (inv == null) return null;

            foreach (var slot in inv.Slots)
            {
                if (slot == null || slot.IsEmpty) continue;
                if (slot.item is RadarItemSO radar && radar.id == ItemId) return radar;
            }
            return null;
        }

        Vector3? PlayerPosition()
        {
            if (_player != null && _player.Transform != null) return _player.Transform.position;
            return null;
        }

        /// <summary>
        /// 검증 하네스와 제작·디버그가 전하를 세운다. 게임 안에서 전하가 느는 길은
        /// 셀을 끼우는 것 하나뿐이다(<see cref="TopUp"/>).
        /// </summary>
        public void SetCharge(float value) => Charge = Mathf.Max(0f, value);

        // ── 저장 ─────────────────────────────────────────────────

        public string SaveKey => "radar_charge";

        public object CaptureState() => new RadarChargeState { charge = Charge };

        public void RestoreState(object state)
        {
            if (state is not RadarChargeState s) return;
            Charge = Mathf.Max(0f, s.charge);
        }
    }
}
