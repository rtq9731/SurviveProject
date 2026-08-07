using UnityEngine;
using Survive.Core;

namespace Survive.World
{
    /// <summary>
    /// <b>세계 상태의 창구.</b> 다섯 갈래 중 하나뿐 비어 있던 자리다 —
    /// 소지품은 <c>Inventory</c>, 게이지는 <c>Vital</c>, 잠금 해제는
    /// <c>UnlockLedger</c>, 생물은 개체마다 한 줄이 있었는데, <b>세계에는 아무것도
    /// 없었다.</b> 다 캔 자리도, 딴 갓도, 자란 단계도 컴포넌트 필드에 살다가
    /// 씬 로드로 사라졌고 저장 포맷에는 「세계」 절이 아예 없었다.
    ///
    /// <b>여기가 그 절이다.</b> 저장본의 <c>world</c> 열쇠 하나에 세계 전체가
    /// 실린다. 물건 열넷이 각자 <c>ISaveable</c>이 되는 길도 있었지만 그러면
    /// 창구가 열넷이 되고, 그것이 「상태 변경은 권한자를 지난다」가 막으려는
    /// 모양 그대로다. 물건이 하는 일은 <b>등록과 응답</b>뿐이다
    /// (<see cref="WorldLedgerRegistry"/>).
    ///
    /// <b>씬을 고치지 않는다.</b> MainScene은 병합할 수 없는 단일 파일이라
    /// <see cref="DayNightService"/>·<see cref="GlowGroveService"/>와 같은 방식으로
    /// 실행 시점에 스스로 붙는다. <c>SaveCoordinator.Collect()</c>는
    /// <c>FindObjectsByType</c>으로 훑으므로 <c>DontDestroyOnLoad</c>에 있어도
    /// 그대로 걸린다 — 시계가 이미 그 길로 저장되고 있다.
    ///
    /// <b>옛 저장본은 그냥 열린다.</b> <c>world</c> 열쇠가 없으면
    /// <c>SaveService</c>가 이 대상을 건너뛰고, 세계는 <b>씬이 놓아둔 그대로</b>
    /// 남는다. 그것이 「세계」 절이 없던 저장본에 대한 올바른 답이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldLedgerService : MonoBehaviour, ISaveable
    {
        /// <summary>저장본에서 이 절을 찾는 열쇠.</summary>
        public const string Key = "world";

        public static WorldLedgerService Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance != null) return;

            var go = new GameObject("WorldLedgerService");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<WorldLedgerService>();
        }

        readonly WorldLedger _ledger = new WorldLedger();

        /// <summary>원장. 검증이 안을 들여다볼 때 쓴다.</summary>
        public WorldLedger Ledger => _ledger;

        /// <summary>
        /// 마지막 불러오기에서 되돌려 준 줄 수. 검증이 "정말 이어졌는가"를 집는다.
        /// </summary>
        public int LastRestored { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            WorldLedgerRegistry.Arrived -= OnOwnerArrived;
            WorldLedgerRegistry.Arrived += OnOwnerArrived;
        }

        void OnDestroy()
        {
            WorldLedgerRegistry.Arrived -= OnOwnerArrived;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 늦게 태어난 물건에게도 제 줄을 건넨다. 군락의 갓과 거대 버섯 그루터기는
        /// 실행 시점에 붙으므로 불러오기보다 뒤에 설 수 있다 — 그때 아무도
        /// 말을 걸어 주지 않으면 그 자리만 씬 그대로 서 있게 된다.
        ///
        /// <b>불러온 적이 없으면 건네지 않는다.</b> 갓 시작한 판에서 물건마다
        /// "너는 원장에 없다"고 말해 주는 것은 의미가 없고, 씬의 초기값을
        /// 지우는 사고만 만든다.
        /// </summary>
        void OnOwnerArrived(IWorldStateOwner owner)
        {
            if (owner == null || !_hasLoaded) return;
            if (_ledger.TryGet(owner.WorldId, out var record)) owner.RestoreWorld(record);
        }

        bool _hasLoaded;

        // ── 저장 ─────────────────────────────────────────────────

        public string SaveKey => Key;

        /// <summary>
        /// 등록된 것들을 한 번 훑어 원장을 다시 쓴다.
        ///
        /// <b>이번 훑기에 안 들어온 줄은 지워진다</b>(<see cref="WorldLedger.EndSweep"/>).
        /// 그래서 「다시 찬 자리」가 저절로 원장에서 빠지고, 불러온 세계에서
        /// 그 자리는 다시 서 있다. <b>「다 캔 것」과 「돌아온 것」을 가르는 일이
        /// 이 한 줄</b>이다 — 따로 표를 두지 않는다.
        /// </summary>
        public object CaptureState()
        {
            _ledger.BeginSweep();

            var owners = WorldLedgerRegistry.Owners;
            for (int i = 0; i < owners.Count; i++)
            {
                var owner = owners[i];
                if (owner == null) continue;

                string id = owner.WorldId;
                if (string.IsNullOrEmpty(id)) continue;

                var record = owner.CaptureWorld();
                if (record == null) { _ledger.PutUnchanged(id); continue; }

                record.id = id;
                _ledger.Put(record);
            }

            _ledger.EndSweep();

            if (_ledger.Conflicts.Count > 0)
                Debug.LogError($"[WorldLedger] 같은 신원이 둘 이상이다 " +
                               $"({_ledger.Conflicts.Count}건): " +
                               string.Join(", ", _ledger.Conflicts) +
                               " — 세계 물건은 여럿이므로 자리로 신원을 짓는다. " +
                               $"{WorldId.Grid}m보다 가깝게 겹쳐 세우지 말 것.", this);

            return _ledger.Capture(WorldClock.Seconds, WorldSeed.Value);
        }

        public void RestoreState(object state)
        {
            if (state is not WorldLedgerState s) return;

            // <b>시계의 주인은 여기가 아니다.</b> 세계 시각을 소유하고 저장본에
            // 싣는 자리는 <see cref="DayNightService"/> 하나다 — 원장은 그 시계의
            // 초로 시각을 적을 뿐이고, 그것이 「재생 시각이 세계 원장에 실린다」의 뜻이다.
            //
            // 다만 <b>기록된 시각보다 시계가 뒤에 있으면 앞으로 민다.</b> 원장의
            // 시각들은 저장하던 순간의 것이라 시계보다 미래일 수 없는데, 두 항목의
            // 복원 순서는 저장본이 정한다. 뒤처진 채로 두면 <c>now - at</c>이 음수가
            // 되어 그 자리들이 영영 안 돌아온다.
            if (WorldClock.Now < s.clockSeconds) WorldClock.Restore(s.clockSeconds);

            // <b>시드도 세계 상태다.</b> 시각과 같은 절에 실려 같은 문으로 돌아온다 —
            // 그래야 불러온 세계에서 저 덤불이 저장하기 전과 같은 것을 떨군다.
            //
            // <b>0이면 앉히지 않는다.</b> 0은 「시드를 적기 전의 저장본」이라는 뜻이고
            // (WorldSeed.Fresh는 0을 내지 않는다), 그때 옛 저장본 전부를 시드 0의
            // 같은 세계로 만들어 버리는 것보다 이번 판의 시드를 그대로 두는 편이 낫다.
            if (s.seed != 0) WorldSeed.Restore(s.seed);

            _ledger.Restore(s);
            _hasLoaded = true;

            int n = 0;
            var owners = WorldLedgerRegistry.Owners;
            for (int i = 0; i < owners.Count; i++)
            {
                var owner = owners[i];
                if (owner == null) continue;

                _ledger.TryGet(owner.WorldId, out var record);
                owner.RestoreWorld(record);
                if (record != null) n++;
            }

            LastRestored = n;
        }
    }
}
