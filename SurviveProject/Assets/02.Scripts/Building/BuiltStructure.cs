using System.Collections.Generic;
using UnityEngine;

namespace Survive.Building
{
    /// <summary>
    /// 플레이어가 세운 것임을 표시한다.
    ///
    /// 태그나 레이어 대신 컴포넌트를 쓴 이유: 무엇으로 지었는지(정의)를 같이
    /// 들고 있어야 나중에 철거 환급과 저장 복원을 할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuiltStructure : MonoBehaviour
    {
        [SerializeField] BuildableSO definition;

        public BuildableSO Definition => definition;

        /// <summary>
        /// <b>실행 중에 태어났는가.</b> 생성 목록이 담는 것은 이것뿐이다
        /// (<c>Survive.World.SpawnLedger</c>).
        ///
        /// 씬에 미리 놓인 건축물은 <b>씬이 존재의 주인</b>이라 목록에 실으면
        /// 안 된다 — 실었다가 불러오기가 거두고 다시 세우면, 씬을 고치는 날
        /// 저장본이 옛 씬의 물건을 되살린다. 그 경계를 <see cref="Setup"/>이
        /// 긋는다: 이 문을 지난 것만 「태어난 것」이다.
        /// </summary>
        public bool Spawned { get; private set; }

        public void Setup(BuildableSO d)
        {
            definition = d;
            Spawned = true;

            // 세우는 그 순간 경로에도 등록한다. 다음 프레임까지 미루면 그 사이에
            // 생물이 방금 세운 벽을 지나는 경로를 그대로 들고 달린다.
            StructureNavObstacle.Attach(gameObject);
        }

        /// <summary>
        /// 씬에 미리 놓인 건축물을 챙긴다. 이쪽은 <see cref="Setup"/>을 거치지 않는다.
        ///
        /// definition이 비어 있으면 손대지 않는다. 건설 미리보기(고스트)가 바로
        /// 그 상태다 — 프리팹의 definition은 비어 있고 BuildPlacer가 세울 때만 채운다.
        /// 유령이 NavMesh를 도려내면 아직 짓지도 않은 벽이 길을 막는다.
        /// </summary>
        void Start()
        {
            if (definition != null) StructureNavObstacle.Attach(gameObject);
        }

        // ── 살아 있는 것들 ───────────────────────────────────────
        //
        // 저장할 때 FindObjectsByType으로 훑지 않는 이유는 <c>LanderBase</c>와 같다 —
        // 세계에 물건이 많고, 저장은 자동 저장으로 자주 일어난다. 스스로 등록하면
        // 철거(Destroy)와 씬 언로드가 OnDisable 하나로 함께 풀린다.

        static readonly List<BuiltStructure> _active = new List<BuiltStructure>();

        /// <summary>지금 세계에 서 있는 건축물들. 등록된 차례가 곧 세운 차례다.</summary>
        public static IReadOnlyList<BuiltStructure> Active => _active;

        /// <summary>
        /// 재생을 켤 때마다 새 세계다. 도메인 리로드를 끈 에디터에서는 정적 목록이
        /// 앞 판을 살아서 넘어오고, 그러면 이미 파괴된 것들이 목록에 남아
        /// 저장본에 유령 줄을 만든다 (<c>WorldClock</c>과 같은 자리, 같은 이유).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void 판이_바뀌면_비운다() => _active.Clear();

        void OnEnable()
        {
            if (!_active.Contains(this)) _active.Add(this);
        }

        void OnDisable() => _active.Remove(this);
    }
}
