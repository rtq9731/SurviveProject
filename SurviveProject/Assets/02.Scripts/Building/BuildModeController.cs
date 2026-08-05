using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Core;
using Survive.Player;

namespace Survive.Building
{
    /// <summary>
    /// 건설 모드의 입력.
    ///
    /// InputActions 에셋을 건드리지 않고 여기서 직접 읽는다. 퀵슬롯과 같은 판단이다 —
    /// 건설은 임시로 켜지는 모드라 액션맵을 하나 더 만들 만한 무게가 아니고,
    /// 키를 바꿀 때 손이 한 군데만 가면 된다.
    ///
    ///   B        건설 목록 열기/닫기
    ///   좌클릭    세운다
    ///   휠        회전
    ///   우클릭/ESC 취소
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildModeController : MonoBehaviour
    {
        [SerializeField] BuildPlacer placer;
        [SerializeField] Survive.UI.BuildMenuUI menu;

        [Tooltip("건설 중에는 도구를 휘두르지 않는다")]
        [SerializeField] Survive.Combat.MeleeSwing melee;

        [SerializeField] float rotateStepPerNotch = 15f;

        [Header("철거")]
        [Tooltip("R을 누르고 있으면 바라보는 구조물을 부순다")]
        [SerializeField] float demolishRange = 4f;

        [SerializeField] Transform rayOrigin;

        StructureDemolisher _demolishing;
        float _demolishElapsed;

        void Awake()
        {
            if (rayOrigin == null && Camera.main != null) rayOrigin = Camera.main.transform;
            if (placer == null) placer = GetComponentInParent<BuildPlacer>();
            if (melee == null) melee = GetComponentInParent<Survive.Combat.MeleeSwing>();
            if (menu == null) menu = Object.FindAnyObjectByType<Survive.UI.BuildMenuUI>(FindObjectsInactive.Include);
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            if (kb.bKey.wasPressedThisFrame) ToggleMenu();

            AdvanceDemolish(kb);

            if (placer == null || !placer.IsActive)
            {
                // 건설 모드가 아니면 곡괭이는 돌아와 있어야 한다.
                // Exit()에서만 되돌리면, 다른 경로로 배치가 취소됐을 때
                // 곡괭이가 조용히 죽은 채로 남는다.
                if (melee != null && !melee.enabled) melee.enabled = true;
                return;
            }

            // 건설 중에는 곡괭이가 나가면 안 된다
            if (melee != null && melee.enabled) melee.enabled = false;

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    placer.Rotate(Mathf.Sign(scroll) * rotateStepPerNotch);

                if (mouse.leftButton.wasPressedThisFrame) placer.TryBuild();
                if (mouse.rightButton.wasPressedThisFrame) Exit();
            }

            if (kb.escapeKey.wasPressedThisFrame) Exit();
        }

        /// <summary>
        /// R 홀드로 철거. 채집과 같은 몸짓이라 따로 배울 것이 없다.
        /// 탭이 아니라 홀드인 이유: 상자를 열려다 실수로 부수면 안 된다.
        /// </summary>
        void AdvanceDemolish(Keyboard kb)
        {
            if (!kb.rKey.isPressed)
            {
                if (_demolishing != null) StopDemolish();
                return;
            }

            var target = FindTarget();
            if (target == null || target != _demolishing)
            {
                _demolishing = target;
                _demolishElapsed = 0f;
                if (_demolishing == null) { StopDemolish(); return; }
            }

            _demolishElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_demolishElapsed / Mathf.Max(0.05f, _demolishing.HoldSeconds));
            ReportProgress(progress);

            if (progress < 1f) return;

            var player = GetComponentInParent<PlayerContext>();
            _demolishing.Demolish(player);
            StopDemolish();
        }

        StructureDemolisher FindTarget()
        {
            if (rayOrigin == null) return null;
            if (!Physics.Raycast(rayOrigin.position, rayOrigin.forward, out var hit,
                                 demolishRange, ~0, QueryTriggerInteraction.Collide))
                return null;
            return hit.collider.GetComponentInParent<StructureDemolisher>();
        }

        void StopDemolish()
        {
            _demolishing = null;
            _demolishElapsed = 0f;
            ReportProgress(0f);
        }

        /// <summary>채집과 같은 게이지를 쓴다. 새 UI를 만들 이유가 없다.</summary>
        void ReportProgress(float p)
        {
            if (Survive.Core.GameServices.TryGet<Survive.UI.HoldProgressView>(out var view))
                view.SetExternalProgress(p);
        }

        void ToggleMenu()
        {
            if (menu == null) return;

            if (menu.IsOpen) { menu.Close(); Exit(); }
            else menu.Open();
        }

        /// <summary>건설 모드에서 빠져나온다.</summary>
        public void Exit()
        {
            placer?.Cancel();
            if (melee != null) melee.enabled = true;
        }
    }
}
