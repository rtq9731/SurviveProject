using System.Collections.Generic;
using UnityEngine;
using Survive.Player;

namespace Survive.Creatures
{
    /// <summary>
    /// <b>둥지 — 코어가 놓인 자리</b> (기획서 §2.1 "둥지와 코어", §4.5 "경계 상태").
    ///
    /// <b>이 부품이 「월드 쪽 사건」이다.</b> <c>ScytheWatch.Set</c>과
    /// <c>ScytheMind.Order</c>는 오래 "월드가 부른다"고만 적힌 채 아무도 부르지
    /// 않았다. 여기가 그 부르는 쪽이다.
    ///
    /// <b>판단은 하나도 여기 없다.</b> 해제 조건도, 누가 회수하러 가는지도,
    /// 소프트락이 없다는 것도 전부 <see cref="NestRule"/>에 있다. 이 부품이 하는
    /// 일은 셋뿐이다 — 코어가 어디 있는지 재고, 규칙에 묻고, 답대로 통로를 부른다.
    ///
    /// <b>자리는 사람이 정한다.</b> 지상 지형이 아직 없으므로(스펙 §13) 지금은
    /// 검증이 세운다. 진짜 배치를 기다린다 — 둥지로 가는 길이 "액면이 뭍을 파고든
    /// 수로"라는 것까지 기획서에 적혀 있고, 그 수로가 생기는 날 이 부품을 거기
    /// 놓으면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NestSite : MonoBehaviour
    {
        /// <summary>코어를 문 개체가 둥지에 이만큼 다가오면 놓은 것으로 친다(m).</summary>
        const float DeliverRadius = 2f;

        /// <summary>다시 세어 보는 간격(초). 매 프레임 훑을 이유가 없다.</summary>
        const float CheckSeconds = 0.25f;

        static NestSite _instance;

        [SerializeField] Transform core;

        CoreWhere _where = CoreWhere.Nest;
        ScytheMind _retriever;
        Transform _player;
        float _sinceCheck;

        readonly List<ScytheMind> _scythes = new List<ScytheMind>();
        readonly List<float> _toCore = new List<float>();

        public static NestSite Instance => _instance;

        /// <summary>지금 코어가 어디 있는가. 검증이 값으로 읽는다.</summary>
        public CoreWhere Where => _where;

        /// <summary>코어를 물고 가는 개체. 없으면 null.</summary>
        public ScytheMind Retriever => _retriever;

        /// <summary>코어의 몸. 검증이 자리를 옮겨 보라고 연다.</summary>
        public Transform Core => core;

        /// <summary>둥지 반경 안인가. <see cref="NestRule"/>에 그대로 묻는다.</summary>
        public bool CoreAtHome =>
            core != null && NestRule.AtHome(core.position, transform.position);

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance != this) return;

            _instance = null;

            // 둥지가 사라지면 등급도 함께 내려온다. 남겨 두면 다음 판이
            // 발령으로 시작한다 — 정적 하나이므로 새는 자리가 여기다.
            ScytheWatch.Set(ScytheAlert.Calm);
        }

        /// <summary>
        /// 코어를 이 둥지에 매어 둔다. 검증이 무대를 세울 때 부른다.
        /// </summary>
        public void SetCore(Transform newCore)
        {
            core = newCore;
            _where = CoreAtHome ? CoreWhere.Nest : CoreWhere.Dropped;
            _retriever = null;
        }

        /// <summary>
        /// <b>세계가 알려 주는 사건.</b> 사람이 집었다 / 놓았다를 이쪽으로 넣는다.
        /// 자리를 옮기는 일은 <see cref="NestRule.Next"/>가 한다.
        /// </summary>
        public void Report(CoreEvent what)
        {
            var 다음 = NestRule.Next(_where, what);
            if (다음 == _where) return;

            _where = 다음;

            // 사람이 가로챘으면 물고 가던 개체는 더 이상 회수자가 아니다.
            if (_where == CoreWhere.Held) 회수를_거둔다();

            등급을_맞춘다();
        }

        void Update()
        {
            _sinceCheck += Time.deltaTime;
            if (_sinceCheck < CheckSeconds) return;
            _sinceCheck = 0f;

            훑는다();
        }

        /// <summary>한 번 살펴본다. 검증이 시계를 기다리지 않고 부를 수 있게 연다.</summary>
        public void 훑는다()
        {
            if (core == null) return;

            // 물고 가는 개체가 죽거나 사라졌으면 코어는 다시 바닥에 있는 것이다.
            if (_where == CoreWhere.Carried && _retriever == null)
                _where = CoreWhere.Dropped;

            // 물고 가는 중이면 코어를 그 개체에 붙여 둔다. 꼬리에 매달린 모양이다.
            if (_where == CoreWhere.Carried && _retriever != null)
                core.position = _retriever.transform.position + Vector3.up * 0.5f;

            // 둥지에 닿았으면 놓은 것으로 친다 — <b>사람이 들고 왔든 낫이 물어
            // 왔든 같다.</b> 손에 든 채로도 세는 것이 요점이다: 해제 조건은
            // "코어가 둥지에 있느냐" 하나이고, 거기 내려놓는 동작을 따로 요구하면
            // 조건이 둘이 된다. 사람이 되돌려 놓는 길과 낫이 물어다 놓는 길이
            // 같은 문으로 들어와야 규칙이 하나로 남는다.
            if (_where != CoreWhere.Nest && CoreAtHome)
                Report(CoreEvent.Delivered);

            if (_where == CoreWhere.Nest && _retriever != null) 회수를_거둔다();

            if (_where == CoreWhere.Dropped) 회수를_시킨다();

            등급을_맞춘다();
        }

        /// <summary>
        /// 등급을 코어의 자리에 맞춘다. <b>판단은 규칙이 하고 여기서는 옮기기만 한다.</b>
        /// </summary>
        void 등급을_맞춘다()
        {
            var 있어야할것 = NestRule.AlertFor(_where);
            if (ScytheWatch.Alert != 있어야할것) ScytheWatch.Set(있어야할것);
        }

        /// <summary>
        /// <b>하나에게만 시킨다.</b> 고르는 일은 <see cref="NestRule.PickRetriever"/>가 하고,
        /// 여기서는 그 자리에 지시를 얹는다.
        /// </summary>
        void 회수를_시킨다()
        {
            if (_retriever != null) return;

            낫들을_센다();
            if (_scythes.Count == 0) return;

            int 고른자리 = NestRule.PickRetriever(_toCore);
            if (고른자리 == NestRule.NoOne) return;

            _retriever = _scythes[고른자리];
            _retriever.Order(ScytheDirective.Retrieve);
            _where = NestRule.Next(_where, CoreEvent.PickedUpByScythe);
        }

        void 회수를_거둔다()
        {
            if (_retriever != null) _retriever.Order(ScytheDirective.Release);
            _retriever = null;
        }

        void 낫들을_센다()
        {
            _scythes.Clear();
            _toCore.Clear();

            foreach (var m in Object.FindObjectsByType<ScytheMind>(FindObjectsInactive.Exclude))
            {
                if (m == null) continue;

                _scythes.Add(m);
                _toCore.Add(Vector3.Distance(m.transform.position, core.position));
            }
        }

        /// <summary>
        /// 회수하는 개체가 흩어짐에서 빠지도록 그 자리를 알려 준다.
        /// 없으면 <see cref="ScytheCensus.NoOne"/>.
        /// </summary>
        public int 남길자리(IReadOnlyList<ScytheMind> 목록)
        {
            if (_retriever == null || 목록 == null) return ScytheCensus.NoOne;

            for (int i = 0; i < 목록.Count; i++)
                if (ReferenceEquals(목록[i], _retriever)) return i;

            return ScytheCensus.NoOne;
        }
    }
}
