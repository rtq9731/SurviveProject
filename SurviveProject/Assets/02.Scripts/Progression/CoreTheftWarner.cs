using UnityEngine;
using Survive.Creatures;
using Survive.Localization;
using Survive.Narrative;
using Survive.Player;

namespace Survive.Progression
{
    /// <summary>
    /// 둥지에 다가온 것을 <b>재기만</b> 하고, 울릴지는 <see cref="CoreTheftWarning"/>에 묻는다
    /// (기획서 §4.5 "AI가 사전 경고한다").
    ///
    /// <b>왜 스스로 서는가.</b> 둥지는 아직 씬에 없다 — <c>NestSite</c>의 주석대로
    /// 지상 배치를 기다리는 중이고, 지금은 검증이 세운다. 경고를 씬의 무엇에 매달아
    /// 두면 그 무대가 서는 날까지 배선이 죽어 있고, 배선이 죽은 채로 있으면 아무도
    /// 그것을 시험하지 않는다. <c>UnlockService</c>·<c>HandCraftingService</c>와 같은
    /// 방식으로 스스로 서면 둥지가 어디에 서든 따라온다.
    ///
    /// <b>왜 통로가 <c>UnlockService.Announce</c>인가.</b> 화자가 같으면 줄을 세우는
    /// 자리도 같아야 한다. 여기서 자막판을 따로 잡으면 첫 습득 대사와 이 경고가
    /// 서로를 덮어쓴다. 그리고 그 통로는 원장이 잠그는 한 번짜리인데, 사전 경고는
    /// 원래 한 번짜리다 (<see cref="CoreTheftWarning"/>의 문서 주석).
    ///
    /// <b>왜 글자가 아니라 <see cref="SpokenLine"/>인가.</b> 대사 줄을 날것으로 넘기면
    /// 화면에 나가는 글자가 에셋에서 곧장 오고, 그 자리는 번역 표를 통째로 우회한다.
    /// <c>SpokenLine.Of(SequenceSO, int)</c>는 "큐를 쓰는 연출이 생기면 여기가 그
    /// 문이다"라고 적힌 채 부르는 쪽이 없었다 — 이것이 그 첫 부르는 쪽이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CoreTheftWarner : MonoBehaviour
    {
        /// <summary>다시 재 보는 간격(초). 걸어서 15m를 지나가는 데 몇 초가 걸린다.</summary>
        const float CheckSeconds = 0.25f;

        static CoreTheftWarner _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("CoreTheftWarner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CoreTheftWarner>();
        }

        /// <summary>지금 서 있는 것. 검증 하네스가 부를 자리가 필요해서 연다.</summary>
        public static CoreTheftWarner Instance => _instance;

        /// <summary>경고가 실제로 나갔는가. 검증 하네스가 본다.</summary>
        public bool Warned { get; private set; }

        /// <summary>
        /// 이 판에서 말한 적 없는 것으로 되돌린다. <b>원장은 건드리지 않는다.</b>
        ///
        /// 저장본을 불러와 부품이 새로 선 상태를 검증이 흉내 내는 자리다 —
        /// 그 상태에서도 조용해야 1회성의 주인이 원장이라는 말이 참이 된다.
        /// </summary>
        public void 기억을_지운다() => Warned = false;

        SequenceSO _line;
        PlayerContext _player;
        float _sinceCheck;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _line = Resources.Load<SequenceSO>(CoreTheftWarning.ResourceName);
            if (_line == null)
                Debug.LogWarning($"[CoreTheftWarner] Resources/{CoreTheftWarning.ResourceName}을(를) " +
                                 "찾지 못했습니다. 코어 사전 경고가 울리지 않습니다.");
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

            // 둥지가 없는 판에서는 아무 값도 치르지 않는다. 사람을 찾는 것도
            // 둥지가 선 뒤부터다 — 챕터 1의 대부분은 둥지 없이 흐른다.
            var nest = NestSite.Instance;
            if (nest == null) return;

            훑는다(nest);
        }

        /// <summary>
        /// 한 번 재 본다. 검증이 시계를 기다리지 않고 부를 수 있게 연다.
        /// </summary>
        /// <returns>이번에 경고가 나갔으면 true.</returns>
        public bool 훑는다(NestSite nest)
        {
            if (nest == null || Warned) return false;

            if (_player == null) _player = Object.FindAnyObjectByType<PlayerContext>(FindObjectsInactive.Exclude);
            if (_player == null) return false;

            // 코어가 아니라 <b>둥지</b>까지의 거리다. 코어는 물려 다니는 물건이라
            // 그 자리를 기준으로 삼으면 경고가 낫을 따라다니게 된다.
            float 거리 = CoreTheftWarning.PlaneDistance(_player.Transform.position,
                                                        nest.transform.position);
            if (!CoreTheftWarning.IsMoment(nest.CoreAtHome, 거리)) return false;

            return 울린다();
        }

        /// <summary>
        /// 원장에 한 번을 적고 그 한 번만 말한다. 원장이 먼저인 것은,
        /// 대사를 못 찾았을 때 다음 프레임에 또 시도해 로그를 도배하지 않기 위해서다.
        /// </summary>
        bool 울린다()
        {
            var service = UnlockService.Instance;
            if (service == null) return false;

            // 대사가 없으면 열쇠도 쓰지 않는다. 여기서 먼저 적어 버리면 에셋을
            // 되돌려 놓은 다음 판에서도 영영 조용하다 — 한 번짜리를 빈손으로 태운다.
            if (_line == null) return false;

            if (!CoreTheftWarning.TryClaim(service.Ledger))
            {
                // 저장본에 이미 적혀 있다. 다시 말하지는 않지만 이쪽도 조용해진다.
                Warned = true;
                return false;
            }

            Warned = true;
            service.Announce(SpokenLine.Of(_line, 0));
            return true;
        }
    }
}
