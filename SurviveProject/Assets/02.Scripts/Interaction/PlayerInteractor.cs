using System;
using UnityEngine;
using Survive.Input;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>
    /// 카메라 전방을 훑어 상호작용 대상을 찾고 실행한다.
    ///
    /// 무엇을 겨눴는가는 <see cref="AimQuery"/>가 묻고 <see cref="AimSelection"/>이 정한다.
    /// 여기 남은 것은 입력·홀드·프롬프트뿐이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] Transform rayOrigin;      // 보통 카메라

        [Tooltip("손이 닿는 거리(m)")]
        [SerializeField] float detectDistance = 3f;

        [SerializeField] LayerMask interactableMask = ~0;

        /// <summary>
        /// 시선에서 이만큼 벗어난 것까지는 겨눈 것으로 봐준다(m).
        /// 작은 물건을 픽셀 단위로 겨누게 하면 그것대로 괴로우므로 여유를 남긴다.
        ///
        /// <b>0.3에서 0.2로 줄였다.</b> 씬의 상호작용 대상 59개를 여덟 자리에서
        /// 겨눠 본 실측(<c>Testing/AimAccuracyProbe</c>)이 근거다. 똑바로 겨눴을 때의
        /// 적중은 0.2와 0.3이 똑같았고(둘 다 472표본 중 466, 98.7%), <b>시선을 40도
        /// 비껴 놓았을 때만</b> 갈렸다 — 944표본에서 엉뚱한 것이 잡히는 수가
        /// 41(0.3)에서 21(0.2)로 절반이 됐다. 0.15까지 줄이면 그것이 20으로 하나 더
        /// 주는 대신 똑바로 겨눴을 때의 놓침이 2에서 3으로 늘어, 얻는 것보다 잃는 것이 크다.
        ///
        /// 손이 닿는 거리(3m)는 그대로 두었다. 2.5·3·3.5m를 같은 방식으로 재 봤지만
        /// 세 값의 적중률 차이가 0.2%p 안이라 바꿀 근거가 없었다.
        ///
        /// <b>왜 인스펙터 필드가 아닌가.</b> 이 값은 <c>05.Prefabs/Player.prefab</c>에
        /// 0.3으로 직렬화되어 있다. 필드로 두면 코드의 기본값을 고쳐도 프리팹의 값이
        /// 이기고, 이번 라운드는 그 프리팹을 고칠 수 없다. 필드를 없애면 직렬화된
        /// 값이 아예 읽히지 않으므로 언제나 여기서 나온다 —
        /// <c>ScrapCounterView.format</c>에서 같은 함정을 같은 수로 풀었다.
        /// </summary>
        const float AimTolerance = 0.2f;

        PlayerContext _player;
        Transform _playerRoot;
        readonly AimQuery _aim = new AimQuery();

        public IInteractable Current { get; private set; }

        /// <summary>지금 조준한 것의 계층. 윤곽을 그리는 쪽이 읽는다. 없으면 null.</summary>
        public Transform CurrentRoot { get; private set; }

        public event Action<string> PromptChanged;       // null이면 숨김
        public event Action<float> HoldProgressChanged;  // 0~1

        IHoldInteractable _holding;
        float _holdElapsed;
        string _lastPrompt;

        void Awake()
        {
            _player = GetComponentInParent<PlayerContext>();
            _playerRoot = _player != null ? _player.transform : transform.root;
            if (rayOrigin == null && Camera.main != null) rayOrigin = Camera.main.transform;

            _aim.MaxDistance = detectDistance;
            _aim.Radius = AimTolerance;
            _aim.Mask = interactableMask;
            _aim.Self = _playerRoot;
        }

        void OnEnable()
        {
            if (input == null) return;
            input.InteractEvent += BeginInteract;
            input.InteractCancelledEvent += CancelInteract;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.InteractEvent -= BeginInteract;
            input.InteractCancelledEvent -= CancelInteract;
        }

        void Update()
        {
            RefreshTarget();
            AdvanceHold();
        }

        void LateUpdate()
        {
            // 조준한 것에 옅은 윤곽을 그린다. Update가 대상을 정한 뒤라야 한다.
            AimOutline.Draw(CurrentRoot, rayOrigin);
        }

        void RefreshTarget()
        {
            if (rayOrigin == null) return;

            _aim.TryPick(rayOrigin.position, rayOrigin.forward,
                         out IInteractable found, out Transform root, out _);

            CurrentRoot = root;

            if (!ReferenceEquals(found, Current))
            {
                if (_holding != null) CancelInteract();
                Current = found;
            }

            // 상호작용할 수 없어도 프롬프트는 보여준다.
            // "곡괭이가 필요하다" / "좌클릭으로 부순다" 같은 안내가 그렇다 —
            // 아무것도 안 뜨면 대상이 아예 인식되지 않은 것으로 오해한다.
            // CanInteract는 대상이 플레이어 상태를 읽어 프롬프트를 만들 수 있게 먼저 부른다.
            string prompt = null;
            if (Current != null)
            {
                Current.CanInteract(_player);
                prompt = Current.InteractionPrompt;
            }

            if (prompt != _lastPrompt)
            {
                _lastPrompt = prompt;
                PromptChanged?.Invoke(prompt);
            }
        }

        /// <summary>E를 누르고 있는가. 연속 채집 판단에 쓴다.</summary>
        bool _interactHeld;

        void BeginInteract()
        {
            _interactHeld = true;
            if (Current == null || !Current.CanInteract(_player)) return;

            if (Current is IHoldInteractable hold && hold.HoldDuration > 0f)
            {
                _holding = hold;
                _holdElapsed = 0f;
            }
            else
            {
                Current.Interact(_player);
            }
        }

        void CancelInteract()
        {
            _interactHeld = false;
            if (_holding == null) return;
            _holding.OnHoldCancelled();
            _holding = null;
            _holdElapsed = 0f;
            HoldProgressChanged?.Invoke(0f);
        }

        /// <summary>
        /// 누른 채로 있으면 다음 대상을 이어서 캔다.
        /// 대상이 사라졌으면 다음 프레임의 RefreshTarget이 새 대상을 잡아 주므로,
        /// 여기서는 이번 프레임에 이미 잡혀 있는 것만 본다.
        /// </summary>
        void TryContinueHold()
        {
            RefreshTarget();

            if (Current is IHoldInteractable next && next.HoldDuration > 0f &&
                Current.CanInteract(_player))
            {
                _holding = next;
                _holdElapsed = 0f;
            }
        }

        void AdvanceHold()
        {
            if (_holding == null) return;

            _holdElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_holdElapsed / _holding.HoldDuration);
            _holding.OnHoldProgress(progress);
            HoldProgressChanged?.Invoke(progress);

            if (progress >= 1f)
            {
                var toComplete = _holding;
                _holding = null;
                _holdElapsed = 0f;
                HoldProgressChanged?.Invoke(0f);
                toComplete.Interact(_player);

                // 손을 떼지 않았으면 다음 것으로 이어서 캔다.
                // 잔해 하나 캘 때마다 E를 다시 눌러야 하면 열 개를 모으는 데
                // 손가락이 먼저 지친다. 조준만 옮기면 계속 캐진다.
                if (_interactHeld) TryContinueHold();
            }
        }
    }
}
