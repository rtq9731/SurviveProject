using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 몸이 들고 있는 한 무더기 — 보관함 한 칸, 회수함 한 칸.
    ///
    /// <b>낙하물 줄과 다르다.</b> 바닥에 굴러다니는 것은 제 몸이 있어서 제 줄을
    /// 갖지만, 보관함 속의 것은 <b>몸이 없다</b> — 보관함이 사라지면 그 안의 것도
    /// 함께 사라지고 되찾을 자리가 없다. 그래서 이쪽은 자리를 적지 않는다.
    /// 자리는 자기를 담고 있는 몸의 것이다.
    /// </summary>
    [Serializable]
    public class SpawnStack
    {
        /// <summary><c>ItemDataSO.id</c>.</summary>
        public string id;

        public int count;
    }

    /// <summary>
    /// 몸에 걸어 두고 자리를 뜬 제작 하나.
    ///
    /// <b>개당 소요 시간을 함께 적는다.</b> <c>CraftJob</c>은 걸 때의
    /// <c>RecipeSO.craftSeconds</c>를 찍어 두고 그 값으로 산다 — 값을 다시 읽으면
    /// 밸런스를 돌린 날 이미 걸려 있던 제작의 값이 조용히 바뀐다. 사람이 걸 때
    /// 들은 값 그대로 끝나야 한다.
    /// </summary>
    [Serializable]
    public class SpawnJobRow
    {
        /// <summary><c>RecipeSO.id</c>.</summary>
        public string recipe;

        /// <summary>아직 완성되지 않은 개수. 재료는 걸 때 이미 전부 빠졌다.</summary>
        public int remaining;

        /// <summary>지금 만드는 <b>한 개</b>에 들인 시간(초).</summary>
        public float elapsed;

        /// <summary>개당 소요 시간(초). 걸 때 찍어 둔 값이다.</summary>
        public float unitSeconds;
    }

    /// <summary>
    /// 생성 목록에 적힌 한 줄. <b>씬에 없던 것이 지금 세계에 있다</b>는 기록이다.
    ///
    /// <b><see cref="WorldRecord"/>와 담는 것이 다르다.</b> 저쪽은 씬이 놓아둔
    /// 물건이 「어떻게 달라졌는가」를 적으므로 되살릴 몸이 이미 있다. 이쪽은
    /// 되살릴 몸이 <b>없어서</b> 무엇으로 다시 만드는지를 적어야 한다 —
    /// 그래서 <see cref="what"/>·<see cref="position"/>·<see cref="rotation"/>이
    /// 여기 있고 저쪽에는 없다.
    ///
    /// <b>갈래마다 다른 스키마를 두지 않은 것은 저쪽과 같다.</b> 건축물과 낙하물이
    /// 실제로 적는 것은 「무엇을 · 몇 개 · 어디에 · 어느 쪽을 보고」 넷으로 덮이고,
    /// 남는 것은 갈래가 뜻을 정하는 시각 둘뿐이다.
    /// </summary>
    [Serializable]
    public class SpawnRecord
    {
        /// <summary>어떤 갈래인가. <see cref="WorldLedgerScope"/>의 이름들이다.</summary>
        public string kind;

        /// <summary>
        /// <b>무엇으로 다시 만드는가.</b> 건축물이면 <c>BuildableSO.id</c>,
        /// 낙하물이면 <c>ItemDataSO.id</c>. 프리팹을 직접 가리키지 않는 이유는
        /// 저장본이 글자이기 때문이다 — 목록(<c>BuildCatalogSO</c>·
        /// <c>ItemDatabaseSO</c>)이 이 글자에서 프리팹을 찾아 준다.
        /// </summary>
        public string what;

        /// <summary>몇 개인가. 건축물은 언제나 1이고, 낙하물은 한 무더기의 수량이다.</summary>
        public int count;

        /// <summary>어디에 있는가.</summary>
        public Vector3 position;

        /// <summary>
        /// 어느 쪽을 보고 있는가. <b>낙하물은 언제나 무회전이다</b> —
        /// 떨어진 물건이 도는 각도는 <c>ItemDropper</c>가 눈에만 보이는 것으로
        /// 정해 둔 값이라(<c>Random.rotation</c>) 세계 상태가 아니다.
        /// </summary>
        public Quaternion rotation;

        /// <summary>
        /// 갈래가 뜻을 정하는 <b>절대 세계 시각</b> — 「이때 끝난다」.
        /// 화톳불이면 연료가 다 타는 시각이다. 쓰지 않는 갈래는 0이다.
        ///
        /// <b>남은 초가 아니라 끝나는 시각이다.</b> 남은 초를 적으면 불러온
        /// 순간부터 다시 세므로 저장해 둔 사이에 흐른 세계 시간이 통째로
        /// 사라진다 — <see cref="WorldLedgerScope"/>가 못 박은 규율 그대로다.
        /// </summary>
        public float until;

        /// <summary>
        /// 갈래가 뜻을 정하는 <b>절대 세계 시각</b> — 「이때부터다」.
        /// 화톳불이면 마지막으로 불이 붙은 시각이고, 부활 지점을 고르는
        /// <c>RespawnRule</c>이 그 값으로 「마지막 불」을 가린다. 안 적으면
        /// 불러온 화톳불이 전부 같은 시각에 지펴진 것이 되어 부활 지점이
        /// 아무 데나 잡힌다. 쓰지 않는 갈래는 0이다.
        /// </summary>
        public float since;

        // ── 몸에 딸린 것 ─────────────────────────────────────────
        //
        // <b>왜 딸림을 줄이 싣는가.</b> 보관함의 내용물·회수함·걸어 둔 제작은
        // 셋 다 「생성된 몸에 붙어 있는 상태」다. 각자 저장본에 제 절을 갖게 하면
        // 절의 주인과 몸의 주인이 갈라지고, 그 순간 <b>순서</b>가 정답을 정하게 된다 —
        // 몸이 아직 안 세워졌는데 내용물이 먼저 돌아오면 그것은 허공에 부어진다.
        // 줄이 제 딸림을 실으면 몸을 세우는 그 한 걸음 안에서 함께 앉으므로
        // 순서라는 문제 자체가 없어진다. 저장할 때의 창구도 하나가 된다.

        /// <summary>
        /// <b>몸이 보관하고 있는 것.</b> 보관함의 내용물이 여기 실린다.
        /// 몸이 없으면 갈 곳이 없는 물건들이다.
        /// </summary>
        public List<SpawnStack> holds = new List<SpawnStack>();

        /// <summary>
        /// <b>다 만들어 놓고 아직 안 가져간 것.</b> 스테이션의 회수함이다.
        /// <see cref="holds"/>와 목록을 나눈 이유는 <b>돌아갈 자리가 다르기</b>
        /// 때문이다 — 이쪽은 회수함, 저쪽은 보관 격자다. 한 목록에 담으려면
        /// 무더기마다 어느 쪽인지를 적어야 하고, 그 꼬리표가 빈 목록 하나보다 크다.
        /// </summary>
        public List<SpawnStack> output = new List<SpawnStack>();

        /// <summary>
        /// <b>걸어 두고 자리를 뜬 제작.</b> 규칙 §5.4가 「여러 개를 대기열에
        /// 걸어두고 자리를 뜰 수 있다」고 하는데, 저장이 그것을 지우면 자리를 뜨는
        /// 일이 <b>저장으로만</b> 금지되는 셈이 된다. 재료는 걸 때 이미 빠졌으므로
        /// 잃는 것은 시간이 아니라 물건이다.
        /// </summary>
        public List<SpawnJobRow> queued = new List<SpawnJobRow>();
    }

    /// <summary>
    /// 저장본에 실리는 <b>생성 목록</b>. 「세계」 절 안에 든다 —
    /// <see cref="WorldLedgerState"/>를 보라.
    /// </summary>
    [Serializable]
    public class SpawnLedgerState
    {
        public List<SpawnRecord> records = new List<SpawnRecord>();
    }

    /// <summary>
    /// <b>목록이 얼마나 담는가, 넘치면 무엇을 버리는가.</b>
    ///
    /// 상한이 없으면 저장본이 무한히 자란다. 그런데 <b>무엇을 버리는지가
    /// 규칙으로 서 있지 않으면</b> 넘친 순간의 손실이 설명되지 않는 사고가 된다.
    /// 그래서 갈래마다 버리는 쪽을 여기서 정한다.
    /// </summary>
    public static class SpawnLedgerRule
    {
        /// <summary>
        /// 건축물 상한. <b>정상 플레이로는 닿지 않는 수</b>다 —
        /// 조각 하나마다 자원이 들고, 이만큼 세우려면 거점이 아니라 도시가 된다.
        /// </summary>
        public const int StructureCap = 512;

        /// <summary>
        /// 낙하물 상한. 낙하물은 <b>줍지 않으면 계속 쌓이는</b> 유일한 갈래라
        /// 상한이 실제로 걸릴 수 있는 쪽이다. 한 판에서 바닥에 굴러다니는 것이
        /// 이보다 많으면 그것은 이미 주우러 갈 수 있는 양이 아니다.
        /// </summary>
        public const int DropCap = 128;

        /// <summary>
        /// 이 갈래가 목록에서 <b>제 줄</b>을 갖는가.
        ///
        /// <b>화톳불 연료는 갖지 않는다.</b> 연료는 몸이 아니라 몸의 상태이고,
        /// 그 몸은 건축물 줄이 이미 싣는다. 연료에 따로 줄을 주면 몸이 없는
        /// 줄이 생겨 「돌려줄 불이 없는 연료」가 저장본에 남는다 —
        /// 그것이 이 갈래를 <see cref="WorldLedger"/>에서 뺐던 이유였다.
        /// </summary>
        public static bool HasRow(string kind) =>
            kind == WorldLedgerScope.Structure || kind == WorldLedgerScope.Drop;

        /// <summary>
        /// 한 몸이 <b>몇 무더기</b>까지 싣는가 (보관 격자와 회수함 각각).
        ///
        /// <b>상한을 몸마다 두는 이유.</b> 저장본이 커지는 축은 「몸 몇 개」와
        /// 「몸 하나가 몇 칸」 둘인데, 앞은 <see cref="StructureCap"/>이 이미 막고
        /// 있고 뒤는 게임 자신이 이미 정해 둔 값이 있다 — 보관함 18칸, 회수함 4칸.
        /// 그 값보다 넉넉하게 잡아 두면 <b>정상 플레이로는 닿지 않고</b>, 닿았다면
        /// 그것은 칸 수를 늘리면서 이 규칙을 안 본 것이다.
        ///
        /// 둘을 곱한 것이 최악이다: 건축물 512 × (32 + 32)무더기.
        /// 무더기 하나가 서른 글자 남짓이니 저장본이 1MB를 넘지 않는다.
        /// </summary>
        public const int StacksPerBody = 32;

        /// <summary>
        /// 한 몸이 <b>몇 개의 제작</b>까지 싣는가.
        /// <c>CraftQueue.DefaultCapacity</c>(6)보다 넉넉해야 한다 —
        /// 게임이 걸도록 허락한 것을 저장이 잘라 내면 재료가 사라진다.
        /// </summary>
        public const int JobsPerBody = 16;

        /// <summary>
        /// 이 갈래가 <b>딸림</b>을 싣는가.
        ///
        /// <b>낙하물은 안 싣는다.</b> 바닥에 굴러다니는 물건은 자기가 곧 내용물이라
        /// 안에 무엇을 담지 않는다. 실을 자리를 만들어 두면 「가방을 떨궜다」 같은
        /// 것을 언젠가 거기 넣게 되는데, 그것은 낙하물이 아니라 그릇이고
        /// 그릇은 몸을 갖는 편이 낫다.
        /// </summary>
        public static bool CarriesAttachment(string kind) => kind == WorldLedgerScope.Structure;

        /// <summary>이 갈래를 몇 줄까지 담는가. 제 줄이 없는 갈래는 0이다.</summary>
        public static int CapFor(string kind)
        {
            if (kind == WorldLedgerScope.Structure) return StructureCap;
            if (kind == WorldLedgerScope.Drop) return DropCap;
            return 0;
        }

        /// <summary>
        /// 넘쳤을 때 <b>앞엣것(먼저 생긴 것)</b>을 남기는가.
        ///
        /// <b>건축물은 앞을 남긴다.</b> 사람이 가장 오래 산 거점이 앞에 있고,
        /// 그것을 잃는 것이 방금 놓은 벽 하나를 잃는 것보다 훨씬 나쁘다.
        /// 게다가 건축물이 상한에 닿는 것은 정상 플레이가 아니라 사고이므로,
        /// 이 규칙의 몫은 조용히 버리는 것이 아니라 <b>시끄럽게 알리는 것</b>이다
        /// (<see cref="SpawnLedger.Overflowed"/>).
        ///
        /// <b>낙하물은 뒤를 남긴다.</b> 사람이 지금 주우러 걸어가고 있는 것은
        /// 방금 떨어진 것이고, 앞엣것은 이미 지나쳐 잊은 것이다.
        /// 규칙 §5.7이 약속한 「대가는 시간뿐」이 지켜지는 쪽도 이쪽이다 —
        /// 방금 죽어서 떨군 것이 남아야 되찾으러 갈 수 있다.
        /// </summary>
        public static bool KeepsOldest(string kind) => kind == WorldLedgerScope.Structure;

        /// <summary>
        /// 남은 연료(초)를 <b>다 타는 절대 시각</b>으로 바꾼다.
        /// 목록에 적히는 값은 언제나 이쪽이다.
        /// </summary>
        public static float BurnsOutAt(float now, float fuelSeconds) => now + fuelSeconds;

        /// <summary>
        /// 다 타는 시각에서 <b>지금 남은 연료(초)</b>를 되짚는다.
        ///
        /// 저장해 둔 사이에 흐른 세계 시간만큼 줄어 있고, 이미 지났으면 0이다 —
        /// 곧 불러온 세계에서 그 불은 꺼진 채로 선다. <b>남은 초를 그대로
        /// 적었다면</b> 며칠을 건너뛰고 돌아와도 불이 그대로 타고 있었을 것이고,
        /// 그러면 「불을 지키는 일」이 저장·불러오기로 공짜가 된다.
        /// </summary>
        public static float FuelLeft(float burnsOutAt, float now) =>
            burnsOutAt - now > 0f ? burnsOutAt - now : 0f;
    }

    /// <summary>
    /// <b>생성 목록.</b> 세계 원장의 나머지 절반이다.
    ///
    /// <b>왜 원장과 다른 물건인가.</b> <see cref="WorldLedger"/>는 <b>씬이 놓은
    /// 것의 변화</b>를 담는다 — 담기는 것은 차이뿐이고, 존재의 주인은 씬이다.
    /// 이쪽은 <b>없던 것의 존재</b>를 담는다 — 존재의 주인이 목록 자신이다.
    /// 두 규율은 정반대라 한 물건에 넣으면 둘 중 하나가 반드시 깨진다.
    /// 「이번 훑기에 아무도 안 적은 줄은 버린다」가 저쪽에서는 <b>돌아온 자리</b>를
    /// 뜻하지만 이쪽에서는 <b>철거됐다</b>를 뜻하고, 「줄이 없으면 씬 그대로」가
    /// 저쪽에서는 <b>서 있다</b>지만 이쪽에서는 <b>없다</b>다.
    ///
    /// <b>그래도 창구는 하나다.</b> 저장본에서 이 목록은 「세계」 절 안에 든다
    /// (<see cref="WorldLedgerState.spawned"/>). 절을 따로 두면 두 절의 복원
    /// <b>순서</b>를 저장본이 정하게 되는데, 이 목록은 <see cref="WorldClock"/>과
    /// <see cref="WorldSeed"/>가 이미 앉은 뒤라야 옳게 되살아난다
    /// (연료가 다 타는 시각을 지금 시계와 견주기 때문이다).
    /// 한 절에 담으면 그 순서가 바람이 아니라 사실이 된다.
    ///
    /// <b>신원이 없다.</b> 원장은 신원이 있어야 했다 — 저장본의 줄을 씬에 이미
    /// 서 있는 물건과 <b>짝지어야</b> 했기 때문이다. 이쪽은 짝지을 상대가 없고
    /// 줄이 곧 몸을 만든다. 열쇠를 지으면 없는 문제를 푸는 셈이고, 게다가
    /// 지을 수 있는 열쇠는 자리뿐인데 자리는 이쪽에서 신원이 되지 못한다 —
    /// 같은 자리에 다시 지을 수 있고, 낙하물은 굴러다닌다.
    /// <b>차례가 신원이다.</b> 목록은 통째로 적히고 통째로 되살아난다
    /// (<c>DeathDropService</c>가 이미 같은 모양으로 돌고 있다).
    ///
    /// Unity 없이 검증된다. 몸을 거두고 다시 세우는 일은
    /// <c>Survive.World.SpawnLedgerStage</c>가 한다.
    /// </summary>
    public sealed class SpawnLedger
    {
        readonly List<SpawnRecord> _records = new List<SpawnRecord>();
        readonly List<string> _overflowed = new List<string>();
        int _trimmed;

        /// <summary>적힌 줄 수.</summary>
        public int Count => _records.Count;

        /// <summary>적힌 줄들. 차례가 곧 태어난 차례다.</summary>
        public IReadOnlyList<SpawnRecord> Records => _records;

        /// <summary>
        /// 상한에 걸려 <b>버려진 건축물</b>들. 비어 있는 것이 정상이고,
        /// 비어 있지 않으면 그것은 사고다 — 모는 쪽이 소리 내어 알린다.
        /// </summary>
        public IReadOnlyList<string> Overflowed => _overflowed;

        /// <summary>상한에 걸려 놓아 준 낙하물 수. 이쪽은 사고가 아니라 규칙이다.</summary>
        public int Trimmed => _trimmed;

        /// <summary>
        /// 몸마다의 상한에 걸려 <b>못 실은 딸림</b>의 수(무더기와 제작을 합친 것).
        /// 비어 있는 것이 정상이다 — 정상 플레이로 닿는 수가 아니므로
        /// 0이 아니면 그것은 칸 수를 늘리면서 규칙을 안 본 것이다.
        /// </summary>
        public int AttachmentTrimmed => _attachmentTrimmed;

        int _attachmentTrimmed;

        /// <summary>
        /// 훑기를 시작한다. <b>목록을 비운다</b> — 원장과 달리 이쪽은 매번
        /// 세계를 그대로 옮겨 적는 것이라, 지난번 줄을 남겨 둘 이유가 없다.
        /// 철거된 것과 주워진 것이 저절로 빠지는 자리가 여기다.
        /// </summary>
        public void BeginSweep()
        {
            _records.Clear();
            _overflowed.Clear();
            _trimmed = 0;
            _attachmentTrimmed = 0;
        }

        /// <summary>
        /// 한 줄을 적는다. 제 줄을 갖지 않는 갈래나 무엇으로 만드는지 모르는
        /// 줄은 적지 않는다.
        /// </summary>
        /// <returns>적었으면 true.</returns>
        public bool Put(SpawnRecord record)
        {
            if (record == null) return false;
            if (!SpawnLedgerRule.HasRow(record.kind)) return false;
            if (string.IsNullOrEmpty(record.what)) return false;
            if (record.count <= 0) return false;

            딸림을_다듬는다(record);

            _records.Add(record);
            return true;
        }

        /// <summary>
        /// 딸림에 몸마다의 상한을 먹이고, 실을 자격이 없는 갈래의 딸림은 지운다.
        ///
        /// <b>앞을 남긴다.</b> 격자의 앞칸이 사람이 먼저 넣은 것이고, 회수함도
        /// 먼저 완성된 것이 앞에 있다. 그리고 무엇보다 이 상한에 닿는 것은
        /// 정상 플레이가 아니므로 이 규칙의 몫은 조용히 고르는 것이 아니라
        /// <see cref="AttachmentTrimmed"/>로 <b>세어 두는 것</b>이다.
        /// </summary>
        void 딸림을_다듬는다(SpawnRecord record)
        {
            if (!SpawnLedgerRule.CarriesAttachment(record.kind))
            {
                record.holds = null;
                record.output = null;
                record.queued = null;
                return;
            }

            자른다(record.holds, SpawnLedgerRule.StacksPerBody);
            자른다(record.output, SpawnLedgerRule.StacksPerBody);
            자른다(record.queued, SpawnLedgerRule.JobsPerBody);
        }

        void 자른다<T>(List<T> list, int cap)
        {
            if (list == null || list.Count <= cap) return;

            _attachmentTrimmed += list.Count - cap;
            list.RemoveRange(cap, list.Count - cap);
        }

        /// <summary>
        /// 훑기를 닫고 <b>상한을 먹인다</b>. 갈래마다 남기는 쪽이 다르다
        /// (<see cref="SpawnLedgerRule.KeepsOldest"/>).
        /// </summary>
        public void EndSweep()
        {
            Trim(WorldLedgerScope.Structure);
            Trim(WorldLedgerScope.Drop);
        }

        void Trim(string kind)
        {
            int cap = SpawnLedgerRule.CapFor(kind);
            if (cap <= 0) return;

            int 있는것 = 0;
            for (int i = 0; i < _records.Count; i++)
                if (_records[i] != null && _records[i].kind == kind) 있는것++;

            int 버릴것 = 있는것 - cap;
            if (버릴것 <= 0) return;

            if (SpawnLedgerRule.KeepsOldest(kind))
            {
                // 앞을 남기므로 뒤에서부터 걷어 낸다.
                for (int i = _records.Count - 1; i >= 0 && 버릴것 > 0; i--)
                {
                    if (_records[i] == null || _records[i].kind != kind) continue;
                    _overflowed.Add(_records[i].what);
                    _records.RemoveAt(i);
                    버릴것--;
                }
                return;
            }

            // 뒤를 남기므로 앞에서부터 걷어 낸다. 지운 자리로 다음 것이 당겨 오므로
            // 걷어 낸 걸음에서는 색인을 올리지 않는다.
            for (int i = 0; i < _records.Count && 버릴것 > 0; )
            {
                if (_records[i] == null || _records[i].kind != kind) { i++; continue; }
                _records.RemoveAt(i);
                _trimmed++;
                버릴것--;
            }
        }

        /// <summary>저장본에 실을 모양으로. 차례는 적힌 차례 그대로다.</summary>
        public SpawnLedgerState Capture()
        {
            var state = new SpawnLedgerState();
            for (int i = 0; i < _records.Count; i++) state.records.Add(_records[i]);
            return state;
        }

        /// <summary>
        /// 저장본에서 읽어 앉힌다. <b><c>null</c>이면 비운다</b> —
        /// 생성 목록이 없던 옛 저장본이 그 자리이고, 그때 올바른 세계는
        /// <b>사람이 세운 것이 하나도 없는</b> 세계다. 실제로 그 저장본을 쓰던
        /// 세계에는 정말로 하나도 남아 있지 않았다(씬 로드로 사라졌다).
        ///
        /// 상한은 여기서도 먹인다 — 저장본은 사람이 고칠 수 있는 입력이다.
        /// </summary>
        public void Restore(SpawnLedgerState state)
        {
            BeginSweep();
            if (state?.records == null) return;

            for (int i = 0; i < state.records.Count; i++) Put(state.records[i]);
            EndSweep();
        }

        /// <summary>비운다.</summary>
        public void Clear() => BeginSweep();
    }
}
