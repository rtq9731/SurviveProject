using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Testing
{
    /// <summary>
    /// 지형을 직접 두드려 걸어갈 길을 찾는다.
    ///
    /// 왜 NavMesh로는 모자란가: 굽고 난 뒤에 놓인 소품은 NavMesh가 모른다.
    /// 챕터 1의 첫 목표 앞에는 한때 외계 구조물이 서 있었는데(콜라이더 x −3.1..4.2,
    /// z 1.1..5.9, 높이 6m), NavMesh는 그 자리를 여전히 평지로 알고 있어 <b>벽을 관통하는</b>
    /// 직선 경로를 돌려주었다. 그 경로를 믿고 걸으면 벽에 코를 박고 옆걸음만 반복한다.
    /// 그 구조물은 걷어냈지만 버섯 기둥과 바위는 그대로라 같은 일이 그대로 벌어진다.
    ///
    /// 그래서 여기서는 캡슐이 실제로 들어가는지, 셀과 셀 사이가 실제로 뚫려 있는지를
    /// 물리 질의로 확인하며 A*로 길을 뚫는다. NavMesh보다 훨씬 느리므로
    /// <b>NavMesh 경로가 온전하지 않을 때만</b> 쓴다.
    /// </summary>
    public static class E2ETerrainPath
    {
        /// <summary>셀 한 변의 길이. 작을수록 정확하고 느리다.</summary>
        public const float CellSize = 0.6f;

        /// <summary>한 칸에서 올라설 수 있는 높이. 경사 45도 + 턱 여유를 셀 크기로 환산한 값.</summary>
        const float MaxStepUp = 0.5f;

        /// <summary>한 칸에서 떨어져도 되는 높이. 이보다 깊으면 낭떠러지로 본다.</summary>
        const float MaxDrop = 3f;

        /// <summary>한 프레임에 펼칠 노드 수. 더 크면 빠르지만 프레임이 튄다.</summary>
        const int NodesPerFrame = 250;

        /// <summary>마지막 탐색이 목표까지 온전히 닿았는가.</summary>
        public static bool LastPathComplete { get; private set; }

        /// <summary>마지막 탐색이 펼친 셀 수. 비용을 로그에 남길 때 쓴다.</summary>
        public static int LastExpandedCells { get; private set; }

        /// <summary>
        /// <paramref name="from"/>에서 <paramref name="to"/>까지 걸어갈 수 있는 길.
        ///
        /// 목표까지 닿지 못하면 <b>가장 가까이 간 곳까지의 길</b>을 돌려준다.
        /// 길이 아예 없으면 빈 목록이다. 부르는 쪽은 <see cref="LastPathComplete"/>로 구별한다.
        /// </summary>
        public static IEnumerator Find(Vector3 from, Vector3 to, Action<List<Vector3>> result,
                                       float searchRadius = 45f, int budget = 9000)
        {
            var probe = new Probe(from, searchRadius);
            var goalCell = probe.ToCell(to);

            yield return Search(probe, from,
                                c => probe.Heuristic(c, goalCell),
                                c => c == goalCell || probe.Heuristic(c, goalCell) <= CellSize * 1.5f,
                                result, budget);
        }

        /// <summary>
        /// 볼륨 <b>안까지</b>의 길.
        ///
        /// 한 점을 목표로 잡고 안 되면 다른 점을 잡는 식으로는 오래 걸리고
        /// 자주 실패한다 — 볼륨 안에서 설 수 있는 자리가 수백 곳인데 그중
        /// 걸어서 닿는 곳이 어디인지는 걸어 봐야 알기 때문이다.
        /// 목표를 "이 볼륨 안 아무 데나"로 두면 탐색 한 번으로 끝난다.
        /// </summary>
        public static IEnumerator FindInto(Vector3 from, Bounds volume, Action<List<Vector3>> result,
                                           float searchRadius = 60f, int budget = 12000)
        {
            var probe = new Probe(from, searchRadius);

            // 가장자리 칸을 목표로 삼으면, 도착 판정 반경만큼 못 미친 자리가
            // 그대로 볼륨 밖이 된다 — "도착했는데 안 들어갔다"가 된다.
            // 목표를 안쪽으로 물려 잡아 도착하면 확실히 안이 되게 한다.
            var inner = volume;
            float inset = Mathf.Min(1.5f, Mathf.Min(volume.size.x, volume.size.z) * 0.25f);
            inner.size = new Vector3(Mathf.Max(0.2f, volume.size.x - inset * 2f), volume.size.y,
                                     Mathf.Max(0.2f, volume.size.z - inset * 2f));

            float ToVolume(Vector2Int c)
            {
                var w = probe.ToWorld(c);
                var near = inner.ClosestPoint(w);
                return Vector2.Distance(new Vector2(w.x, w.z), new Vector2(near.x, near.z));
            }

            yield return Search(probe, from, ToVolume, c => inner.Contains(probe.ToWorld(c)),
                                result, budget);
        }

        static IEnumerator Search(Probe probe, Vector3 from, Func<Vector2Int, float> heuristic,
                                  Func<Vector2Int, bool> isGoal, Action<List<Vector3>> result,
                                  int budget)
        {
            LastPathComplete = false;
            LastExpandedCells = 0;

            var startCell = probe.ToCell(from);
            if (float.IsNaN(probe.GroundAt(startCell)))
            {
                // 서 있는 자리조차 표본이 잡히지 않으면(공중·물속) 길을 그릴 수 없다.
                result(new List<Vector3>());
                yield break;
            }

            var came = new Dictionary<Vector2Int, Vector2Int>();
            var cost = new Dictionary<Vector2Int, float> { [startCell] = 0f };
            var open = new SimpleHeap();
            open.Push(startCell, heuristic(startCell));

            var best = startCell;
            float bestH = heuristic(startCell);
            int expanded = 0, sinceYield = 0;
            bool reached = false;

            while (open.Count > 0 && expanded < budget)
            {
                var cur = open.Pop();
                expanded++;

                float h = heuristic(cur);
                if (h < bestH) { bestH = h; best = cur; }

                if (isGoal(cur))
                {
                    best = cur;
                    reached = true;
                    break;
                }

                foreach (var next in probe.Neighbours(cur))
                {
                    float step = cost[cur] + probe.StepCost(cur, next);
                    if (cost.TryGetValue(next, out float known) && known <= step) continue;

                    cost[next] = step;
                    came[next] = cur;
                    open.Push(next, step + heuristic(next));
                }

                if (++sinceYield >= NodesPerFrame) { sinceYield = 0; yield return null; }
            }

            LastPathComplete = reached;
            LastExpandedCells = expanded;

            var cells = new List<Vector2Int>();
            var walk = best;
            cells.Add(walk);
            while (came.TryGetValue(walk, out var prev)) { walk = prev; cells.Add(walk); }
            cells.Reverse();

            var points = new List<Vector3>(cells.Count);
            foreach (var c in cells) points.Add(probe.ToWorld(c));

            result(Simplify(points));
        }

        /// <summary>
        /// 같은 방향으로 이어지는 점들을 지운다.
        /// 0.6m마다 하나씩 남기면 구간이 수십 개가 되고, 구간마다 도착 판정을
        /// 다시 하느라 정작 걷는 시간이 사라진다.
        /// </summary>
        static List<Vector3> Simplify(List<Vector3> points)
        {
            if (points.Count <= 2) return points;

            var outp = new List<Vector3> { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 a = points[i] - outp[outp.Count - 1];
                Vector3 b = points[i + 1] - points[i];
                a.y = 0f; b.y = 0f;
                if (a.sqrMagnitude < 1e-4f || b.sqrMagnitude < 1e-4f) continue;

                // 방향이 꺾이는 곳과 높이가 크게 바뀌는 곳만 남긴다
                bool turn = Vector3.Angle(a, b) > 12f;
                bool climb = Mathf.Abs(points[i + 1].y - outp[outp.Count - 1].y) > 0.8f;
                if (turn || climb) outp.Add(points[i]);
            }
            outp.Add(points[points.Count - 1]);
            return outp;
        }

        // ── 지형 두드리기 ────────────────────────────────────────

        /// <summary>
        /// 격자 한 칸이 설 수 있는 자리인지, 옆 칸으로 넘어갈 수 있는지를 물리로 묻는다.
        /// 같은 칸을 여러 번 묻게 되므로 결과를 담아 둔다 — 캐시가 없으면
        /// 한 번 탐색에 물리 질의가 수만 번 나간다.
        /// </summary>
        sealed class Probe
        {
            readonly Dictionary<Vector2Int, float> _ground = new Dictionary<Vector2Int, float>();
            readonly HashSet<Collider> _mine = new HashSet<Collider>();
            readonly Vector3 _origin;
            readonly float _radius, _height, _range;

            static readonly Vector2Int[] Dirs =
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 1), new Vector2Int(1, -1),
                new Vector2Int(-1, 1), new Vector2Int(-1, -1),
            };

            public Probe(Vector3 origin, float range)
            {
                _origin = origin;
                _range = range;

                var player = E2EHarness.Player;
                foreach (var c in player.GetComponentsInChildren<Collider>(true)) _mine.Add(c);

                var cc = player.GetComponent<CharacterController>();
                _radius = cc != null ? cc.radius : 0.5f;
                _height = cc != null ? cc.height : 1.8f;
            }

            public Vector2Int ToCell(Vector3 w) => new Vector2Int(
                Mathf.RoundToInt((w.x - _origin.x) / CellSize),
                Mathf.RoundToInt((w.z - _origin.z) / CellSize));

            public Vector3 ToWorld(Vector2Int c)
            {
                float y = GroundAt(c);
                return new Vector3(_origin.x + c.x * CellSize,
                                   float.IsNaN(y) ? _origin.y : y + _height * 0.5f,
                                   _origin.z + c.y * CellSize);
            }

            Vector3 Flat(Vector2Int c) =>
                new Vector3(_origin.x + c.x * CellSize, 0f, _origin.z + c.y * CellSize);

            public float Heuristic(Vector2Int a, Vector2Int b) =>
                Vector3.Distance(Flat(a), Flat(b));

            public float StepCost(Vector2Int a, Vector2Int b)
            {
                float flat = Vector3.Distance(Flat(a), Flat(b));
                // 오르막은 실제로도 느리고, 무엇보다 미끄러져 내려올 위험이 있다.
                float climb = Mathf.Max(0f, GroundAt(b) - GroundAt(a));
                return flat + climb * 2f;
            }

            /// <summary>이 칸의 지면 높이. 설 수 없으면 NaN.</summary>
            public float GroundAt(Vector2Int c)
            {
                if (_ground.TryGetValue(c, out float cached)) return cached;

                float y = Measure(c);
                _ground[c] = y;
                return y;
            }

            float Measure(Vector2Int c)
            {
                var flat = Flat(c);
                if (Mathf.Abs(flat.x - _origin.x) > _range || Mathf.Abs(flat.z - _origin.z) > _range)
                    return float.NaN;

                var hits = Physics.RaycastAll(new Vector3(flat.x, _origin.y + 10f, flat.z),
                                              Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
                float groundY = float.NaN, nearest = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    if (_mine.Contains(hits[i].collider)) continue;
                    if (hits[i].distance >= nearest) continue;
                    nearest = hits[i].distance;
                    groundY = hits[i].point.y;
                }
                if (float.IsNaN(groundY)) return float.NaN;

                // 사람 몸통이 들어가는가. 이걸 빼면 바위 속이나 소품 안을 길이라고 답한다.
                var p0 = new Vector3(flat.x, groundY + _radius + 0.06f, flat.z);
                var p1 = new Vector3(flat.x, groundY + _height - _radius + 0.06f, flat.z);
                var overlap = Physics.OverlapCapsule(p0, p1, _radius * 0.9f, ~0,
                                                     QueryTriggerInteraction.Ignore);
                for (int i = 0; i < overlap.Length; i++)
                    if (!_mine.Contains(overlap[i])) return float.NaN;

                return groundY;
            }

            public IEnumerable<Vector2Int> Neighbours(Vector2Int c)
            {
                float cy = GroundAt(c);
                if (float.IsNaN(cy)) yield break;

                for (int i = 0; i < Dirs.Length; i++)
                {
                    var n = c + Dirs[i];
                    float ny = GroundAt(n);
                    if (float.IsNaN(ny)) continue;

                    float diagonal = Dirs[i].x != 0 && Dirs[i].y != 0 ? 1.42f : 1f;
                    if (ny - cy > MaxStepUp * diagonal) continue;
                    if (cy - ny > MaxDrop) continue;
                    if (!Passable(c, cy, n, ny)) continue;

                    yield return n;
                }
            }

            /// <summary>두 칸 사이가 실제로 뚫려 있는가.</summary>
            bool Passable(Vector2Int a, float ay, Vector2Int b, float by)
            {
                Vector3 from = Flat(a); from.y = ay + _radius + 0.06f;
                Vector3 to = Flat(b); to.y = by + _radius + 0.06f;

                Vector3 seg = to - from;
                float len = seg.magnitude;
                if (len < 1e-3f) return true;

                var hits = Physics.CapsuleCastAll(from, from + Vector3.up * (_height - 2f * _radius),
                                                  _radius * 0.85f, seg / len, len, ~0,
                                                  QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits.Length; i++)
                    if (!_mine.Contains(hits[i].collider)) return false;

                return true;
            }
        }

        // ── 아주 작은 우선순위 큐 ────────────────────────────────

        /// <summary>
        /// A*에 쓸 최소 힙. .NET Standard 2.1에는 PriorityQueue가 없고,
        /// 매번 목록을 정렬하면 셀 수천 개에서 눈에 띄게 느려진다.
        /// </summary>
        sealed class SimpleHeap
        {
            readonly List<Vector2Int> _items = new List<Vector2Int>();
            readonly List<float> _keys = new List<float>();

            public int Count => _items.Count;

            public void Push(Vector2Int item, float key)
            {
                _items.Add(item);
                _keys.Add(key);

                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_keys[parent] <= _keys[i]) break;
                    Swap(parent, i);
                    i = parent;
                }
            }

            public Vector2Int Pop()
            {
                var top = _items[0];
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _keys[0] = _keys[last];
                _items.RemoveAt(last);
                _keys.RemoveAt(last);

                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, small = i;
                    if (l < _items.Count && _keys[l] < _keys[small]) small = l;
                    if (r < _items.Count && _keys[r] < _keys[small]) small = r;
                    if (small == i) break;
                    Swap(small, i);
                    i = small;
                }
                return top;
            }

            void Swap(int a, int b)
            {
                (_items[a], _items[b]) = (_items[b], _items[a]);
                (_keys[a], _keys[b]) = (_keys[b], _keys[a]);
            }
        }
    }
}
