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
    /// <b>절 하나에 원장이 둘 있다.</b> <see cref="Ledger"/>는 씬이 놓은 것의
    /// <b>변화</b>를 담고, <see cref="Spawns"/>는 씬에 없던 것의 <b>존재</b>를
    /// 담는다. 규율이 정반대라 한 물건에 넣을 수 없었지만
    /// (<see cref="SpawnLedger"/>에 자세히 적었다), 창구는 하나여야 한다 —
    /// 그래야 시계·시드·변화·존재의 복원 <b>순서</b>가 저장본의 줄 순서에
    /// 달리지 않고 이 파일 안에서 정해진다.
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

        readonly SpawnLedger _spawns = new SpawnLedger();

        /// <summary>
        /// <b>생성 목록</b> — 원장의 나머지 절반. 원장이 「씬이 놓은 것의 변화」를
        /// 담는다면 이쪽은 「없던 것의 존재」를 담는다. <b>두 물건인데 창구가
        /// 하나인</b> 이유는 <see cref="SpawnLedger"/>에 적어 두었다.
        /// </summary>
        public SpawnLedger Spawns => _spawns;

        /// <summary>
        /// 마지막 불러오기에서 되돌려 준 줄 수. 검증이 "정말 이어졌는가"를 집는다.
        /// </summary>
        public int LastRestored { get; private set; }

        /// <summary>마지막 불러오기에서 <b>다시 세운</b> 것의 수(생성 목록 쪽).</summary>
        public int LastRespawned { get; private set; }

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

            var state = _ledger.Capture(WorldClock.Seconds, WorldSeed.Value);

            // 「없던 것의 존재」는 같은 절에 함께 실린다. 절을 가르면 두 절의
            // 복원 순서를 저장본이 정하게 되는데, 생성 목록은 시계와 시드가
            // 앉은 뒤라야 옳게 되살아난다 — 그 순서를 여기서 사실로 만든다.
            SpawnLedgerStage.Sweep(_spawns);
            state.spawned = _spawns.Capture();

            if (_spawns.Overflowed.Count > 0)
                Debug.LogError($"[SpawnLedger] 건축물이 상한 " +
                               $"{SpawnLedgerRule.StructureCap}줄을 넘어 " +
                               $"{_spawns.Overflowed.Count}개를 못 실었다: " +
                               string.Join(", ", _spawns.Overflowed) +
                               " — 정상 플레이로 닿는 수가 아니다.", this);

            // 딸림도 조용히 사라지지 않는다. 여기 걸렸다는 것은 보관함의 칸 수나
            // 대기열 길이를 늘리면서 SpawnLedgerRule을 안 봤다는 뜻이다.
            if (_spawns.AttachmentTrimmed > 0)
                Debug.LogError($"[SpawnLedger] 몸에 딸린 것 {_spawns.AttachmentTrimmed}개를 " +
                               $"못 실었다 (몸마다 무더기 {SpawnLedgerRule.StacksPerBody}개 · " +
                               $"제작 {SpawnLedgerRule.JobsPerBody}개까지) — " +
                               "칸 수를 늘렸으면 상한도 함께 올려야 한다.", this);

            return state;
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

            // <b>생성 목록은 맨 뒤다.</b> 시계와 시드가 앉은 뒤라야 화톳불의
            // 「다 타는 시각」을 지금과 견줄 수 있고, 원장을 먼저 돌려야 되살아난
            // 건축물이 이미 정리된 세계 위에 선다.
            _spawns.Restore(s.spawned);
            LastRespawned = SpawnLedgerStage.Rebuild(_spawns);
        }
    }
}
