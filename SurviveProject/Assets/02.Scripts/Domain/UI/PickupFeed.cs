using System.Collections.Generic;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>
    /// 방금 얻은 것을 알리는 목록의 <b>줄 하나</b>.
    ///
    /// 화면 조각이 아니라 값이다. 그리는 쪽(<c>PickupFeedView</c>)은 이 목록을 보고
    /// 자기 줄을 맞출 뿐이고, 언제 생기고 언제 사라지는가는 전부 여기 바깥에서 정해진다.
    /// </summary>
    public sealed class PickupFeedRow
    {
        internal PickupFeedRow(ItemDataSO item, int count, float addedAt, float expiresAt)
        {
            Item = item;
            Count = count;
            AddedAt = addedAt;
            ExpiresAt = expiresAt;
        }

        public ItemDataSO Item { get; }

        /// <summary>이 줄이 말하는 개수. 합쳐지면 늘어난다.</summary>
        public int Count { get; private set; }

        /// <summary>마지막으로 무언가 더해진 시각. 합칠 수 있는가를 이 값으로 잰다.</summary>
        public float AddedAt { get; private set; }

        /// <summary>이 시각이 되면 줄이 사라진다.</summary>
        public float ExpiresAt { get; private set; }

        internal void Merge(int count, float now, float lifetime)
        {
            Count += count;
            AddedAt = now;
            ExpiresAt = now + lifetime;
        }
    }

    /// <summary>
    /// <b>무엇을 얻었는지</b> 화면 구석에 잠깐 띄우는 목록의 규칙.
    ///
    /// <b>왜 순수부로 뺐는가.</b> "언제 합치고 언제 사라지는가"가 이 기능의 전부다.
    /// 그것이 <c>MonoBehaviour</c> 안에 있으면 재생 모드를 켜야만 확인할 수 있고,
    /// 5초 뒤에 사라지는지 보려면 5초를 실제로 기다려야 한다. 시각을 인자로 받으면
    /// (<c>Tick(now)</c>) 그 5초를 한 줄로 건너뛸 수 있다. <c>Time.time</c>을 여기서
    /// 직접 읽는 순간 그 이점이 사라지므로 <b>절대 읽지 않는다.</b>
    ///
    /// <b>합치는 이유.</b> 스크랩을 열 번 주우면 열 줄이 뜬다. 그러면 화면이 가려지고
    /// 정작 무엇을 얻었는지가 안 읽힌다. 같은 것은 한 줄에 모아 수만 늘린다.
    ///
    /// <b>합치는 범위 — 시간 창.</b> 합칠 대상을 "화면에 아직 떠 있는 같은 줄"로 하면
    /// 수명(<see cref="LifetimeSeconds"/>)이 곧 창이 되어, 한참 전에 주운 줄의 수가
    /// 슬그머니 늘어난다. 사람은 그 변화를 못 본다 — 이미 읽고 눈을 뗀 줄이기 때문이다.
    /// 그래서 창을 수명보다 짧게 따로 둔다. 창 밖이면 <b>새 줄</b>이 뜨고, 그것이
    /// "방금 또 얻었다"를 더 정확히 말한다.
    ///
    /// <b>합친 줄은 가장 최근 자리로 옮긴다.</b> 두 가지를 한꺼번에 얻는다.
    /// ① 방금 바뀐 줄이 눈이 머무는 자리(가장 새 줄)로 온다.
    /// ② 목록이 언제나 <b>최근 순</b>으로 정렬된 채로 남는다 — 그래서 만료는 언제나
    ///    앞에서부터 일어나고, 가운데 줄이 빠져 아래 줄들이 튀어 오르는 일이 없다.
    ///
    /// <b>얻은 것만 다룬다.</b> 잃은 것(소비·사망 드롭)은 이번 범위 밖이다. 다만
    /// 이 클래스에는 "얻었다"는 말이 한 군데도 없다 — 줄은 아이템과 수만 들고 있다.
    /// 나중에 잃은 것을 붙이려면 <see cref="PickupFeed"/>를 하나 더 두고 문장 키만
    /// 달리 주면 된다. 여기를 고칠 일은 없다.
    /// </summary>
    public sealed class PickupFeed
    {
        /// <summary>
        /// 한눈에 들어오는 줄 수. 이보다 많으면 목록을 읽는 것이 아니라 훑게 된다.
        /// 화면(왼쪽 아래)에서도 다섯 줄이 다른 요소에 닿지 않는 한계다.
        /// </summary>
        public const int DefaultMaxRows = 5;

        /// <summary>
        /// 한 줄이 떠 있는 시간(초). 짧은 줄 다섯을 읽기에 넉넉하고, 그 이상 두면
        /// 이미 지나간 일이 화면 구석을 계속 차지한다.
        /// </summary>
        public const float DefaultLifetimeSeconds = 5f;

        /// <summary>
        /// 같은 것을 합쳐 주는 시간 창(초).
        ///
        /// 스크랩 더미를 밟고 지나가며 E를 연타하는 동안의 간격은 1초 안쪽이고,
        /// 채집물은 한 번에 여러 개가 쏟아진다 — 그 한 뭉치가 한 줄로 읽혀야 한다.
        /// 3초를 넘기면 사람은 이미 다른 것을 보고 있으므로, 그때부터는 옛 줄의 수를
        /// 늘리는 것보다 새 줄을 띄우는 편이 정직하다.
        /// </summary>
        public const float DefaultMergeWindowSeconds = 3f;

        readonly List<PickupFeedRow> _rows = new List<PickupFeedRow>();

        public PickupFeed(int maxRows = DefaultMaxRows,
                          float lifetimeSeconds = DefaultLifetimeSeconds,
                          float mergeWindowSeconds = DefaultMergeWindowSeconds)
        {
            MaxRows = maxRows < 1 ? 1 : maxRows;
            LifetimeSeconds = lifetimeSeconds > 0f ? lifetimeSeconds : DefaultLifetimeSeconds;
            MergeWindowSeconds = mergeWindowSeconds < 0f ? 0f : mergeWindowSeconds;
        }

        public int MaxRows { get; }
        public float LifetimeSeconds { get; }
        public float MergeWindowSeconds { get; }

        /// <summary>오래된 것부터 새 것 순. 이 순서가 곧 화면의 위에서 아래다.</summary>
        public IReadOnlyList<PickupFeedRow> Rows => _rows;

        public int Count => _rows.Count;

        /// <summary>
        /// 목록이 바뀔 때마다 오르는 값. 그리는 쪽이 매 프레임 목록을 비교하지 않고
        /// 이 숫자 하나만 보면 되도록 둔다.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// 얻은 것을 한 건 적는다.
        ///
        /// <paramref name="now"/>는 <b>줄지 않아야 한다</b>(<c>Time.unscaledTime</c>).
        /// 목록이 최근 순으로 정렬돼 있다는 전제가 여기서 나온다.
        /// </summary>
        /// <returns>목록이 바뀌었으면 true.</returns>
        public bool Add(ItemDataSO item, int count, float now)
        {
            // 만료를 먼저 걷어 낸다. 안 그러면 이미 사라졌어야 할 줄에 합쳐져
            // 없던 줄이 되살아난다. 넣을 것이 없어도 시각은 들은 셈이므로
            // 이 걷어 내기는 그대로 한다.
            bool changed = Tick(now);

            // 없는 것을 0개 얻는 일은 없다. 조용히 넘긴다 — 여기서 예외를 던지면
            // 알림 하나 때문에 줍기가 통째로 죽는다.
            if (item == null || count <= 0) return changed;

            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                var row = _rows[i];

                // 최근 순이므로 창을 벗어난 줄을 만나면 그 앞은 볼 것도 없다.
                if (now - row.AddedAt > MergeWindowSeconds) break;
                if (row.Item != item) continue;

                row.Merge(count, now, LifetimeSeconds);
                MoveToNewest(i);
                Version++;
                return true;
            }

            _rows.Add(new PickupFeedRow(item, count, now, now + LifetimeSeconds));

            // 넘치면 가장 오래된 것부터 밀어낸다.
            while (_rows.Count > MaxRows) _rows.RemoveAt(0);

            Version++;
            return true;
        }

        /// <summary>
        /// 시각을 알려 준다. 다 산 줄이 있으면 걷어 낸다.
        /// </summary>
        /// <returns>걷어 낸 것이 있으면 true.</returns>
        public bool Tick(float now)
        {
            if (!Prune(now)) return false;
            Version++;
            return true;
        }

        public void Clear()
        {
            if (_rows.Count == 0) return;
            _rows.Clear();
            Version++;
        }

        // ── 알맹이 ───────────────────────────────────────────────

        /// <summary>
        /// 만료된 줄을 앞에서부터 걷는다. 목록이 최근 순이고 수명이 모두 같으므로
        /// 만료 순서도 앞에서부터다 — 뒤까지 훑을 필요가 없다.
        /// </summary>
        bool Prune(float now)
        {
            int removed = 0;
            while (_rows.Count > 0 && now >= _rows[0].ExpiresAt)
            {
                _rows.RemoveAt(0);
                removed++;
            }
            return removed > 0;
        }

        void MoveToNewest(int index)
        {
            int last = _rows.Count - 1;
            if (index >= last) return;

            var row = _rows[index];
            _rows.RemoveAt(index);
            _rows.Add(row);
        }
    }
}
