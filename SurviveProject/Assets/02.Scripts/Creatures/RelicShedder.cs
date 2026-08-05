using System.Collections.Generic;
using UnityEngine;
using Survive.Core;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;
using Survive.Progression;

namespace Survive.Creatures
{
    /// <summary>
    /// 순찰 중 유물을 흘린다 (백로그 39, 스펙 §2 "낫이 순찰 경로에 가끔 떨어뜨림").
    ///
    /// <b>왜 처치 드롭이 아닌가.</b> 낫은 이기라고 놓은 상대가 아니다 — 걷기보다 빠르고
    /// 세 방이면 죽는다(백로그 30). 유물이 시체에서만 나오면 진행 장비의 열쇠가
    /// "낫을 죽여라"가 되고, 그러면 랜턴을 켜고 물러나는 것이 손해가 된다. 그래서
    /// 유물은 <b>그 영역에 머문 시간</b>에 붙는다. 위험한 곳에 오래 있었다는 사실
    /// 자체가 대가다.
    ///
    /// <b>무엇을 떨굴지는 여기서 정하지 않는다.</b> 미보유 우선(pity)과 "전부 가졌으면
    /// 그만"은 <see cref="RelicDropRule"/>의 순수 규칙이고, 이 부품이 하는 일은 셋뿐이다 —
    /// 때가 됐는지 재고, 지금 상태가 순찰인지 보고, 규칙이 고른 것을 바닥에 놓는다.
    ///
    /// 씬·프리팹을 고치지 않으려고 <see cref="CreatureBrain"/>이 런타임에 붙인다
    /// (<see cref="CreatureCollisionGuard"/>와 같은 관례).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CreatureBrain))]
    public class RelicShedder : MonoBehaviour
    {
        /// <summary>
        /// 굴리는 간격을 덮어쓴다. 0 이하면 에셋의 값을 쓴다.
        ///
        /// 검증용이다. 실제 주기는 사람이 1~2분을 견디라고 정한 값인데, E2E가 그
        /// 시간을 실시간으로 기다리면 시나리오 하나가 몇 분짜리가 된다. 대신 확률과
        /// pity 순서는 건드리지 않는다 — 그것이 이 작업에서 볼 규칙이다.
        /// </summary>
        public static float IntervalOverrideSeconds = 0f;

        /// <summary>지금까지 흘린 유물 수. 검증 하네스가 본다.</summary>
        public static int ShedCount { get; private set; }

        /// <summary>마지막으로 흘린 유물의 id. 검증 하네스가 pity 순서를 이걸로 본다.</summary>
        public static string LastShedItemId { get; private set; }

        /// <summary>시나리오마다 세기를 처음부터 시작한다.</summary>
        public static void ResetCounters()
        {
            ShedCount = 0;
            LastShedItemId = null;
        }

        CreatureBrain _brain;
        RelicShedSO _rule;
        PlayerInventory _player;

        readonly List<RelicOption> _options = new List<RelicOption>();
        readonly List<ItemDataSO> _items = new List<ItemDataSO>();

        float _nextRollTime;

        /// <summary>지금 이 개체가 흘릴 수 있는 것들. 검증이 데이터 배선을 확인할 때 본다.</summary>
        public IReadOnlyList<RelicOption> Options => _options;

        void Awake()
        {
            _brain = GetComponent<CreatureBrain>();

            var health = GetComponent<Survive.Combat.CreatureHealth>();
            _rule = health != null && health.Definition != null ? health.Definition.relicShed : null;

            if (_rule != null) _rule.FillOptions(_options, _items);
            _nextRollTime = Time.time + Interval;
        }

        float Interval =>
            IntervalOverrideSeconds > 0f ? IntervalOverrideSeconds
                                         : (_rule != null ? _rule.intervalSeconds : 25f);

        void Update()
        {
            if (_rule == null || _options.Count == 0) return;
            if (Time.time < _nextRollTime) return;

            _nextRollTime = Time.time + Interval;

            // 쫓거나 때리는 중에는 흘리지 않는다. 흘리는 것은 순찰의 일이고,
            // 싸움 중에 떨어지면 "쫓기다 주웠다"가 되어 위험을 감수한 보상이 아니게 된다.
            if (_brain == null) return;
            if (_brain.State != CreatureState.Wander && _brain.State != CreatureState.Idle) return;

            if (!사람이_보는_거리인가()) return;

            var inventory = 소지품();
            var ledger = BlueprintGate.Active;

            // 전부 가졌으면 아무것도 떨구지 않는다. 유물의 쓸모는 연구 하나뿐이라
            // 다 밝혀낸 뒤로는 바닥에 쌓이기만 한다.
            int 자리 = RelicDropRule.Pick(_options, inventory, ledger, Random.value);
            if (자리 < 0 || 자리 >= _items.Count) return;

            if (Random.value > _rule.chance) return;

            var item = _items[자리];
            if (item == null) return;

            ItemDropper.Drop(item, 1, transform.position + Vector3.up * 0.35f);
            ShedCount++;
            LastShedItemId = item.id;
        }

        /// <summary>
        /// 아무도 없는 곳에 떨궈 봐야 아무도 못 본다. 감지 반경 밖이어도 좋으니
        /// 사람이 이 영역 안에 있을 때만 굴린다 — "머물면 하나 본다"의 내용이 이것이다.
        /// </summary>
        bool 사람이_보는_거리인가()
        {
            if (_player == null) GameServices.TryGet(out _player);
            if (_player == null) return false;

            return Vector3.Distance(transform.position, _player.transform.position) <= _rule.witnessRadius;
        }

        Inventory 소지품()
        {
            if (_player == null) GameServices.TryGet(out _player);
            return _player != null ? _player.Inventory : null;
        }
    }
}
