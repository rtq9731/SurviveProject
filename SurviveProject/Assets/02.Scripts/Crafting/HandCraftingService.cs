using UnityEngine;
using Survive.Core;
using Survive.Items;

namespace Survive.Crafting
{
    /// <summary>
    /// 손 제작 대기열. 몸에 붙어 다니므로 걸어 두고 걸어 다닐 수 있다.
    ///
    /// 제작대와 달리 완성품은 곧바로 소지품으로 들어간다 — 자기 손에서 만든 것을
    /// 다시 회수하러 갈 곳은 없다. 자리가 없으면 대기열이 멈춰 기다린다.
    ///
    /// 씬에 두지 않고 스스로 선다. 제작은 화면(UI)의 것이 아니라 세계의 것이라
    /// 소지품 창을 닫아도 계속 돌아야 하고, 어느 프리팹에 붙일지를 두고
    /// 고민할 성질도 아니다. RespawnService·DeathDropService가 쓰는 방식과 같다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HandCraftingService : MonoBehaviour
    {
        static HandCraftingService _instance;

        public static HandCraftingService Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("HandCraftingService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HandCraftingService>();
        }

        /// <summary>지금 걸려 있는 손 제작들.</summary>
        public CraftQueue Queue { get; } = new CraftQueue();

        void OnEnable() => GameServices.Register(this);

        void OnDisable()
        {
            GameServices.Unregister<HandCraftingService>();
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            if (Queue.IsEmpty) return;

            var inv = PlayerInventory;
            if (inv == null) return;

            CraftQueueService.Tick(Queue, Time.deltaTime, inv, powered: true);
            ReportProgress();
        }

        /// <summary>
        /// 채집·철거와 같은 게이지를 쓴다. 화면에 막대를 하나 더 만들 이유가 없다.
        /// 손으로 캐는 중이면 그쪽이 우선이라 서로 덮어쓰지 않는다
        /// (<see cref="Survive.UI.HoldProgressView"/>가 갈라 놓는다).
        /// </summary>
        void ReportProgress()
        {
            if (!GameServices.TryGet<Survive.UI.HoldProgressView>(out var view) || view == null) return;
            var job = Queue.Active;
            view.SetCraftProgress(job != null ? job.UnitProgress : 0f);
        }

        static Inventory PlayerInventory =>
            GameServices.TryGet<PlayerInventory>(out var pi) && pi != null ? pi.Inventory : null;

        /// <summary>
        /// 제작 UI가 부르는 창구. 재료는 여기서 빠진다.
        ///
        /// <paramref name="available"/>는 "지금 곁에 무엇이 있는가"다. 제작대 앞에서
        /// 손으로 만드는 것까지 막을 이유는 없으므로, 스테이션 요건은 걸 때 한 번만 본다 —
        /// 걸어 놓고 자리를 떠도 손에 든 일은 계속된다.
        /// </summary>
        public bool TryEnqueue(RecipeSO recipe, int count, StationType available = StationType.None)
        {
            var inv = PlayerInventory;
            if (inv == null) return false;
            return CraftQueueService.TryEnqueue(Queue, recipe, count, inv, available);
        }

        /// <summary>한 항목을 물린다. 완성되지 않은 것은 전부 돌아온다.</summary>
        public bool Cancel(int index)
        {
            var inv = PlayerInventory;
            if (inv == null) return false;

            bool ok = CraftQueueService.TryCancel(Queue, index, inv);
            if (ok) ReportProgress();
            return ok;
        }
    }
}
