using System.Collections.Generic;
using UnityEngine;
using Survive.Player;
using Survive.World;

namespace Survive.Creatures
{
    /// <summary>
    /// <b>세계에 낫을 실제로 세우는 몸</b> (기획서 §4.5 "경계 상태", 스펙 §5).
    ///
    /// <b>지금까지 규칙만 있고 몸이 없었다.</b> 서식 범위도 4상태도 밤낮도 다 섰는데
    /// 세계에 낫이 한 마리도 없어서, 밤이 되어도 아무 일이 일어나지 않았다. 개체를
    /// 만드는 것은 E2E뿐이었다. 이 부품이 그 자리를 메운다.
    ///
    /// <b>등급이 수를 정하고, 이 부품은 그 수를 맞추기만 한다.</b> 판단은
    /// <see cref="ScytheCensus"/>에 있고 여기서는 세고 만들고 지운다 — 이 저장소가
    /// 판단과 몸을 가르는 결 그대로다.
    ///
    /// <b>밤에 만들지 않는다.</b> 낫은 밤에 <b>다니는</b> 것이지 밤에 <b>생기는</b> 것이
    /// 아니다. 낮에 지웠다가 밤에 다시 만들면 그것은 자리를 옮긴 것이 아니라
    /// <b>다른 물건</b>이 되고, 도감·관측이 세는 「본 적 있다」와 유물 굴림 시계가
    /// 매일 처음부터 시작한다(스펙 §8에서 같은 이유로 같은 결정을 했다).
    /// 그래서 개체는 늘 있고, <b>낮에는 액면 위로 물러날 뿐</b>이다
    /// (<see cref="ScytheHabitat.CanEnter(HabitatZone, ScytheAlert, bool)"/>).
    /// 밤에 「나타나는」 것은 물러나 있던 것이 해안선까지 돌아오는 일이다.
    ///
    /// <b>어디에 세우는가 — 재등장이 이미 푼 물음이다.</b> "빛 밖이고, 사람 눈에
    /// 안 들어오고, 갈 수 있는 자리"는 <see cref="ScytheReappearance"/>가 답한다.
    /// 새 축을 만들지 않고 그것을 그대로 쓴다 — 규칙이 둘이면 두 규칙이 언젠가
    /// 서로 다른 자리를 고른다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScytheSpawner : MonoBehaviour
    {
        /// <summary>
        /// 세우는 개체에 붙는 이름표. 이 부품이 <b>제가 만든 것만</b> 센다.
        ///
        /// 로마자인 것은 규율이다 — 화면에 나가는 코드에 한글 리터럴을 두지 않는다
        /// (<c>LocSentenceGateTests</c>). 이 이름은 사람이 볼 글이 아니라 계층 창의
        /// 표식이므로 표로 옮길 것도 아니다.
        /// </summary>
        public const string SpawnedName = "Scythe(Spawned)";

        /// <summary>수를 맞춰 보는 간격(초). 매 프레임 셀 이유가 없다.</summary>
        const float CheckSeconds = 1f;

        /// <summary>
        /// 사람에게서 이만큼 떨어진 고리 위에 세운다(m).
        ///
        /// <b>감지 반경(14m)보다 넉넉히 멀어야 한다.</b> 세우자마자 감지되면 사람은
        /// 「나타났다」가 아니라 「덮쳤다」를 겪는다. 그리고 목격 반경(40m) 안이어야
        /// 유물 굴림이 도는 자리에 선다 — 그 둘 사이가 이 값이 사는 띠다.
        /// </summary>
        const float SpawnRadius = 28f;

        /// <summary>고리 위에서 자리를 몇 방향 찾아보는가.</summary>
        const int CandidateCount = 24;

        static ScytheSpawner _instance;

        readonly List<ScytheMind> _mine = new List<ScytheMind>();
        readonly List<ReappearSpot> _spots = new List<ReappearSpot>(CandidateCount);
        readonly List<Vector3> _spotPositions = new List<Vector3>(CandidateCount);
        readonly List<float> _distances = new List<float>();

        GameObject _prefab;
        Transform _player;
        float _sinceCheck;

        /// <summary>지금 이 부품이 들고 있는 개체 수. 검증이 값으로 확인한다.</summary>
        public int Alive
        {
            get
            {
                청소한다();
                return _mine.Count;
            }
        }

        /// <summary>이번 판에 세운 총 횟수. 늘고 주는 것을 검증이 센다.</summary>
        public int SpawnedTotal { get; private set; }

        /// <summary>이번 판에 물린 총 횟수.</summary>
        public int DespawnedTotal { get; private set; }

        /// <summary>마지막으로 세운 자리. 실측이 좌표로 읽는다.</summary>
        public Vector3 LastSpawnPoint { get; private set; }

        public static ScytheSpawner Instance => _instance;

        /// <summary>
        /// 스스로 선다. 씬을 고치지 않고 붙이는 것은 이 저장소의 관례다
        /// (<c>CreatureBrain</c>이 부품을 붙이는 것과 같은 이유 — 씬·프리팹은
        /// 병합할 수 없는 단일 파일이다).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void 스스로_선다()
        {
            if (_instance != null) return;

            var go = new GameObject("ScytheSpawner");
            DontDestroyOnLoad(go);
            go.AddComponent<ScytheSpawner>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            var book = Resources.Load<CreatureSpawnBookSO>(CreatureSpawnBookSO.ResourceName);
            _prefab = book != null ? book.Scythe : null;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            _sinceCheck += Time.deltaTime;
            if (_sinceCheck < CheckSeconds) return;
            _sinceCheck = 0f;

            수를_맞춘다();
        }

        /// <summary>
        /// 지금 있어야 할 수와 실제 수를 맞춘다. <b>이 함수가 이 부품의 전부다.</b>
        /// 검증이 시계를 기다리지 않고 부를 수 있게 열어 둔다.
        /// </summary>
        public void 수를_맞춘다()
        {
            청소한다();

            if (_player == null)
            {
                var ctx = Object.FindAnyObjectByType<PlayerContext>(FindObjectsInactive.Exclude);
                if (ctx != null) _player = ctx.transform;
            }
            if (_player == null || _prefab == null) return;

            int 모자란수 = ScytheCensus.ShortfallUnder(_mine.Count, ScytheWatch.Alert);
            for (int i = 0; i < 모자란수; i++) 하나_세운다();

            int 남는수 = ScytheCensus.SurplusOver(_mine.Count, ScytheWatch.Alert);
            if (남는수 > 0) 흩어_보낸다(남는수);
        }

        /// <summary>죽었거나 누가 지운 개체를 목록에서 뺀다.</summary>
        void 청소한다() => _mine.RemoveAll(m => m == null);

        void 하나_세운다()
        {
            if (!자리를_고른다(out var 자리)) return;

            var go = Instantiate(_prefab, 자리, Quaternion.identity);
            go.name = SpawnedName;

            var mind = go.GetComponent<ScytheMind>();
            if (mind == null) mind = go.AddComponent<ScytheMind>();

            _mine.Add(mind);
            SpawnedTotal++;
            LastSpawnPoint = 자리;
        }

        /// <summary>
        /// 수가 줄 때. <b>먼 것부터 사라진다</b> — 고르는 일은 Domain이 한다
        /// (<see cref="ScytheCensus.PickDespawn"/>).
        /// </summary>
        void 흩어_보낸다(int 몇마리)
        {
            _distances.Clear();
            for (int i = 0; i < _mine.Count; i++)
                _distances.Add(Vector3.Distance(_mine[i].transform.position, _player.position));

            // <b>코어를 물고 가는 개체는 흩어지지 않는다.</b> 그것이 §4.5의 그림이고,
            // 거리만 보면 둥지에 닿은 그 개체가 사람에게서 가장 멀어 먼저 지워진다.
            int 남길것 = NestSite.Instance != null
                       ? NestSite.Instance.남길자리(_mine)
                       : ScytheCensus.NoOne;

            var 물릴것 = ScytheCensus.PickDespawn(_distances, 몇마리, 남길것);

            // 뒤에서부터 지운다. 앞에서 지우면 남은 자리 번호가 밀린다.
            물릴것.Sort();
            for (int i = 물릴것.Count - 1; i >= 0; i--)
            {
                var m = _mine[물릴것[i]];
                _mine.RemoveAt(물릴것[i]);
                if (m != null) Destroy(m.gameObject);
                DespawnedTotal++;
            }
        }

        /// <summary>
        /// 세울 자리 하나. <b>재등장 규칙을 그대로 쓴다</b> — 빛 밖이고, 눈에 안 들어오고,
        /// 갈 수 있는 자리라는 물음이 똑같기 때문이다.
        ///
        /// 각도 하한은 0으로 둔다. 재등장은 <b>있던 자리에서 크게 돌아야</b> 하지만
        /// 세우는 것은 있던 자리가 없다 — 하한을 그대로 걸면 사람의 정면 반대편만
        /// 자리가 되어, 마릿수가 늘 때 전부 한쪽에 몰린다.
        /// </summary>
        bool 자리를_고른다(out Vector3 자리)
        {
            자리 = Vector3.zero;

            Vector3 사람 = _player.position;
            var eye = Camera.main;

            _spots.Clear();
            _spotPositions.Clear();

            for (int i = 0; i < CandidateCount; i++)
            {
                float rad = i * (360f / CandidateCount) * Mathf.Deg2Rad;
                var p = new Vector3(사람.x + Mathf.Sin(rad) * SpawnRadius,
                                    사람.y,
                                    사람.z + Mathf.Cos(rad) * SpawnRadius);

                if (!액면인가(ref p)) continue;
                if (이미_누가_있는가(p)) continue;

                _spotPositions.Add(p);
                _spots.Add(new ReappearSpot(p, LitZoneRegistry.IsLit(p), 눈에_드는가(eye, p)));
            }

            int 고른것 = ScytheReappearance.Pick(사람, 사람, _spots, minTurnDegrees: 0f);
            if (고른것 == ScytheReappearance.Stay) return false;

            자리 = _spotPositions[고른것];
            return true;
        }

        /// <summary>
        /// 그 자리에 이미 다른 낫이 서 있는가.
        ///
        /// <b>이것이 없으면 다섯이 한 자리에 겹친다.</b> 자리를 고르는 규칙은
        /// 결정적이라(같은 입력에 같은 답) 부를 때마다 같은 자리를 내놓는다 —
        /// 재등장에서는 「있던 자리에서 크게 돌아라」가 그 역할을 했지만, 세울 때는
        /// 있던 자리가 없다. 실측으로 다섯이 전부 한 점에 섰다.
        ///
        /// <b>간격은 감지 반경이다.</b> 새 수를 만들지 않는다 — 서로의 감지 반경
        /// 안에 세우지 않는다는 것이 곧 "따로 있는 개체로 보인다"의 내용이고,
        /// 그 값은 정의(SO)에 이미 있어 튜닝이 바뀌면 함께 움직인다.
        /// </summary>
        bool 이미_누가_있는가(Vector3 p)
        {
            float 간격 = 서로의_간격();

            for (int i = 0; i < _mine.Count; i++)
            {
                if (_mine[i] == null) continue;
                if (Vector3.Distance(_mine[i].transform.position, p) < 간격) return true;
            }

            return false;
        }

        /// <summary>낫끼리 떨어져야 할 거리(m). 정의의 감지 반경을 그대로 쓴다.</summary>
        float 서로의_간격()
        {
            if (_prefab == null) return 14f;

            var def = _prefab.GetComponent<Survive.Combat.CreatureHealth>()?.Definition;
            return def != null ? def.detectRadius : 14f;
        }

        /// <summary>
        /// 이 수평 자리가 액면 위인가. 맞으면 높이를 액면에 맞춰 준다.
        ///
        /// <b>액면만 고른다.</b> 낫은 액면에 붙어 사는 몸이고(<see cref="ScytheHabitat"/>),
        /// 육지에 세우면 태어나자마자 서식지 밖이라 돌아가기부터 한다.
        /// </summary>
        bool 액면인가(ref Vector3 p)
        {
            if (!WaterBody.TryGetSurfaceAt(new Vector3(p.x, 0f, p.z), out float 수면))
                return false;

            p.y = 수면 + 0.6f;
            return true;
        }

        /// <summary>
        /// 그 자리가 지금 사람 눈에 들어오는가. <b>세우는 것이 보이면 안 된다</b> —
        /// 어둠에서 나타나는 것과 눈앞에서 생겨나는 것은 다른 일이다.
        /// 판정은 <c>ScytheMind</c>가 재등장에서 쓰는 것과 같은 문법이다.
        /// </summary>
        bool 눈에_드는가(Camera eye, Vector3 p)
        {
            if (eye == null) return false;

            Vector3 to = p - eye.transform.position;
            float dist = to.magnitude;
            if (dist < 0.01f) return true;

            if (Vector3.Angle(eye.transform.forward, to) > eye.fieldOfView * 0.5f * 1.4f)
                return false;

            return !Physics.Raycast(eye.transform.position, to.normalized, dist * 0.95f,
                                    ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>세운 것을 전부 물린다. 검증이 무대를 비울 때 쓴다.</summary>
        public void 전부_치운다()
        {
            for (int i = 0; i < _mine.Count; i++)
                if (_mine[i] != null) DestroyImmediate(_mine[i].gameObject);

            _mine.Clear();
        }
    }
}
