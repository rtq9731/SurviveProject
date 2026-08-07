using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 원장에 적힌 한 줄. <b>세계의 어떤 자리가 씬에 놓인 모습과 달라졌다</b>는 기록이다.
    ///
    /// <b>갈래마다 다른 스키마를 두지 않았다.</b> 다섯 갈래가 실제로 적는 것은
    /// 「없어졌는가 · 언제 그랬는가 · 얼마나 남았는가」 셋으로 덮인다. 갈래별
    /// 클래스를 만들면 갈래 하나가 늘 때마다 저장본 포맷이 흔들리고,
    /// 그것은 <see cref="Survive.Core.SaveEntry"/>가 이미 피하기로 한 것이다.
    /// </summary>
    [Serializable]
    public class WorldRecord
    {
        /// <summary>원장 안에서의 신원. <see cref="WorldId"/>가 짓는다.</summary>
        public string id;

        /// <summary>어떤 갈래인가. <see cref="WorldLedgerScope"/>의 이름들이다.</summary>
        public string kind;

        /// <summary>지금 없어져 있는가 — 다 캤거나, 꺼졌거나, 시들었거나.</summary>
        public bool gone;

        /// <summary>
        /// 그렇게 된 <b>세계 시각</b>(초). 프레임 시계가 아니라
        /// <see cref="WorldClock"/>의 초다 — 저장을 건너야 하기 때문이다.
        /// </summary>
        public float at;

        /// <summary>
        /// 갈래가 뜻을 정하는 한 값. 식물은 자란 단계, 화톳불이라면 남은 연료.
        /// 쓰지 않는 갈래는 0이다.
        /// </summary>
        public float amount;
    }

    /// <summary>
    /// 저장본의 <b>「세계」 절</b>. 이 절이 없던 시절의 저장본은 그냥 열린다 —
    /// <c>SaveService</c>가 모르는 열쇠를 건너뛰고, 열쇠가 없으면 아무것도
    /// 복원하지 않으므로 <b>씬이 놓아둔 그대로</b>가 된다. 그것이 옛 저장본에
    /// 대한 올바른 답이다.
    /// </summary>
    [Serializable]
    public class WorldLedgerState
    {
        /// <summary>이 세계를 세운 시각(초). 원장의 <c>at</c>들이 이 시계로 적혀 있다.</summary>
        public float clockSeconds;

        public List<WorldRecord> records = new List<WorldRecord>();
    }

    /// <summary>
    /// <b>원장 안에서의 신원을 짓는 곳.</b>
    ///
    /// <b>왜 자리인가.</b> 세계의 물건은 <b>여럿</b>이다 — 잔해가 스물몇, 군락의
    /// 갓이 여럿, 화톳불이 여럿. 그래서 <c>PlayerInventory</c>·<c>chapter_director</c>처럼
    /// 고정 문자열을 열쇠로 삼을 수 없다. 하나뿐인 것에는 고정 문자열이 맞지만,
    /// 여럿인 것에 그것을 쓰면 <b>나중 것이 앞의 것을 덮는다</b>.
    /// <c>StorageContainer</c>가 이미 같은 문제를 자리로 풀었다 —
    /// 옮길 수 없는 물건이라 자리가 곧 신원이다.
    ///
    /// <b>갈래를 앞에 붙인다.</b> 같은 자리에 식물과 채집물이 겹쳐 서 있을 수 있고,
    /// 그때 자리만으로는 둘이 같은 줄을 놓고 다툰다.
    ///
    /// <b>격자로 반올림한다.</b> 부동소수 좌표를 그대로 글자로 만들면 물리가
    /// 0.001m 밀어 놓은 것만으로 신원이 바뀌어 <b>저장할 때와 불러올 때의 열쇠가
    /// 달라진다</b>. 격자를 <see cref="Grid"/>로 둔 것은 그보다 촘촘하게 두 물건이
    /// 서는 일이 씬에 없기 때문이고, 그래도 겹치면 <see cref="WorldLedger"/>가
    /// 충돌로 잡아 조용히 덮어쓰지 않는다.
    /// </summary>
    public static class WorldId
    {
        /// <summary>자리를 반올림하는 격자(m).</summary>
        public const float Grid = 0.5f;

        /// <summary>이 갈래의, 이 자리에 선 것의 신원.</summary>
        public static string At(string kind, Vector3 position)
        {
            int x = Mathf.RoundToInt(position.x / Grid);
            int y = Mathf.RoundToInt(position.y / Grid);
            int z = Mathf.RoundToInt(position.z / Grid);
            return $"{kind}@{x}_{y}_{z}";
        }
    }

    /// <summary>
    /// <b>세계의 원장.</b> 다섯 갈래(소지품·게이지·원장·생물·세계) 중 마지막
    /// 하나에는 창구가 없었다 — 채집물 고갈과 재생, 식물의 단계, 군락의 갓이
    /// 전부 컴포넌트 필드에 살았고 <b>씬 로드로 사라졌다</b>. 여기가 그 창구다.
    ///
    /// <b>담는 것은 「달라진 것」뿐이다.</b> 씬이 놓아둔 그대로인 자리는 한 줄도
    /// 적지 않는다. 그렇게 두는 이유가 둘이다.
    /// <list type="number">
    /// <item><b>씬이 존재의 주인이다.</b> 무엇이 어디에 서 있는가는 사람이 씬에서
    ///   정하는 것이고, 원장이 그것까지 들면 씬을 고칠 때마다 저장본이 옛 세계를
    ///   되살린다.</item>
    /// <item><b>「다 캔 것」과 「돌아온 것」이 저절로 갈린다.</b> 돌아온 자리는
    ///   원장에서 <b>지워진다</b>. 그러면 다음 저장본에는 그 줄이 없고,
    ///   불러온 세계에서 그 자리는 다시 서 있다. 돌아오지 않는 것
    ///   (재생 주기 0)은 <c>gone</c>인 채로 남아 영영 비어 있다.</item>
    /// </list>
    ///
    /// <b>조용히 덮어쓰지 않는다.</b> 한 번의 훑기에서 같은 신원이 두 번 오면
    /// 뒤엣것을 버리고 충돌로 적는다. <c>SaveSnapshot.Add</c>가 같은 판단을
    /// 이미 해 두었다 — 열쇠 충돌은 저장 대상 쪽의 결함이고, 여기서 지워 버리면
    /// 그 결함이 저장본에서 사라져 추적할 수 없게 된다.
    ///
    /// Unity 없이 검증된다. 씬을 훑고 컴포넌트에 값을 되돌리는 일은
    /// <c>Survive.World.WorldLedgerService</c>가 한다.
    /// </summary>
    public sealed class WorldLedger
    {
        readonly Dictionary<string, WorldRecord> _byId = new Dictionary<string, WorldRecord>();
        readonly List<string> _order = new List<string>();
        readonly List<string> _conflicts = new List<string>();
        readonly HashSet<string> _seenInSweep = new HashSet<string>();
        bool _sweeping;

        /// <summary>적힌 줄 수.</summary>
        public int Count => _order.Count;

        /// <summary>이번 훑기에서 같은 신원이 두 번 온 자리들. 비어 있는 것이 정상이다.</summary>
        public IReadOnlyList<string> Conflicts => _conflicts;

        /// <summary>
        /// 훑기를 시작한다. 이 사이에 들어온 <see cref="Put"/>만 살아남고,
        /// <b>안 들어온 줄은 지워진다</b> — 그것이 「돌아온 자리는 원장에서
        /// 빠진다」를 공짜로 만든다.
        /// </summary>
        public void BeginSweep()
        {
            _sweeping = true;
            _seenInSweep.Clear();
            _conflicts.Clear();
        }

        /// <summary>
        /// 훑기를 닫는다. 이번에 아무도 적지 않은 줄은 버린다.
        /// </summary>
        public void EndSweep()
        {
            if (!_sweeping) return;
            _sweeping = false;

            for (int i = _order.Count - 1; i >= 0; i--)
            {
                if (_seenInSweep.Contains(_order[i])) continue;
                _byId.Remove(_order[i]);
                _order.RemoveAt(i);
            }
        }

        /// <summary>
        /// 한 줄을 적는다. 신원이 비었거나 <see cref="WorldLedgerScope"/>가 담지
        /// 않기로 한 갈래면 적지 않는다. 훑기 중에 같은 신원이 두 번 오면
        /// 뒤엣것을 버리고 충돌로 적는다.
        /// </summary>
        /// <returns>적었으면 true.</returns>
        public bool Put(WorldRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.id)) return false;
            if (!WorldLedgerScope.Carries(record.kind)) return false;

            if (_sweeping && !_seenInSweep.Add(record.id))
            {
                _conflicts.Add(record.id);
                return false;
            }

            if (!_byId.ContainsKey(record.id)) _order.Add(record.id);
            _byId[record.id] = record;
            return true;
        }

        /// <summary>
        /// 이 신원은 씬이 놓아둔 그대로라고 적는다 — 곧 <b>줄을 지운다</b>.
        /// 훑기 중이면 「봤지만 적을 것이 없다」로 세므로 충돌 판정에는 들어간다.
        /// </summary>
        public void PutUnchanged(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (_sweeping && !_seenInSweep.Add(id))
            {
                _conflicts.Add(id);
                return;
            }

            if (!_byId.Remove(id)) return;
            _order.Remove(id);
        }

        /// <summary>이 신원의 줄. 없으면 false — 곧 씬이 놓아둔 그대로다.</summary>
        public bool TryGet(string id, out WorldRecord record)
        {
            record = null;
            return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out record);
        }

        /// <summary>저장본에 실을 모양으로. 순서는 적힌 순서 그대로다.</summary>
        public WorldLedgerState Capture(float clockSeconds)
        {
            var state = new WorldLedgerState { clockSeconds = clockSeconds };
            for (int i = 0; i < _order.Count; i++)
                state.records.Add(_byId[_order[i]]);
            return state;
        }

        /// <summary>
        /// 저장본에서 읽어 앉힌다. <b><c>null</c>이면 비운다</b> —
        /// 「세계」 절이 없던 옛 저장본이 그 자리이고, 그때 올바른 세계는
        /// <b>씬이 놓아둔 그대로</b>다.
        /// </summary>
        public void Restore(WorldLedgerState state)
        {
            Clear();
            if (state?.records == null) return;

            for (int i = 0; i < state.records.Count; i++)
            {
                var r = state.records[i];
                if (r == null || string.IsNullOrEmpty(r.id)) continue;
                if (!WorldLedgerScope.Carries(r.kind)) continue;

                if (!_byId.ContainsKey(r.id)) _order.Add(r.id);
                _byId[r.id] = r;
            }
        }

        /// <summary>비운다.</summary>
        public void Clear()
        {
            _byId.Clear();
            _order.Clear();
            _conflicts.Clear();
            _seenInSweep.Clear();
            _sweeping = false;
        }
    }
}
