using System.Linq;
using UnityEngine;
using Survive.Building;
using Survive.Crafting;
using Survive.Items;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 화톳불과 랜턴을 <b>세우는 손</b>. 여러 시나리오가 같은 일을 하고 있어 한곳에 모았다.
    ///
    /// <b>배치 절차는 여기서 보지 않는다.</b> 「짓는 것이 실제로 되는가」는
    /// <c>E2EBaseBuilding</c>·<c>E2ERespawn</c>이 진짜 조작으로 이미 본다.
    /// 여기 필요한 것은 <b>타고 있는 불 하나</b>뿐이므로 프리팹을 바로 놓는다 —
    /// 그것까지 매번 조작으로 지으면 다른 것을 재려던 시나리오가 건축 시나리오가 된다.
    /// </summary>
    public static class E2ECampfire
    {
        /// <summary>
        /// 사람 앞 <paramref name="거리"/>미터에 타는 불을 하나 놓는다.
        /// <b>지면에 붙인다</b> — 공중에 뜬 불은 밝은 구역의 중심이 발밑이 아니게 되어
        /// "몇 미터 안이 밝은가"가 통째로 어긋난다.
        /// </summary>
        public static Campfire 세운다(float 거리 = 2.2f)
        {
            var catalog = Resources.FindObjectsOfTypeAll<BuildCatalogSO>().FirstOrDefault();
            var def = catalog?.entries?.FirstOrDefault(b => b != null && b.id == "campfire");
            if (def?.prefab == null) return null;

            var 사람 = E2EHarness.Player.transform;
            var 앞 = 사람.forward;
            앞.y = 0f;
            if (앞.sqrMagnitude < 1e-4f) 앞 = Vector3.forward;

            // 발밑은 <see cref="E2EHarness.TryGroundY"/>에게 묻는다. 직접 위에서 아래로
            // 쏘면 <b>자기 몸을 먼저 맞는다</b> — 사람 코앞 2.2m는 캡슐 반경 안에 걸릴 수
            // 있고, 그러면 정수리 높이를 지면으로 알고 불을 사람 위에 얹는다.
            var 자리 = 사람.position + 앞.normalized * 거리;
            if (E2EHarness.TryGroundY(자리, out float 발밑)) 자리.y = 발밑;

            var go = Object.Instantiate(def.prefab, 자리, Quaternion.identity);
            return go.GetComponentInChildren<Campfire>(true);
        }

        /// <summary>인벤토리에 랜턴이 없으면 넣는다. 지니는 순간 스스로 켜진다.</summary>
        public static void 랜턴을_준다()
        {
            var player = E2EHarness.Player;
            var pack = player != null ? player.GetComponentInChildren<PlayerInventory>(true) : null;
            if (pack?.Inventory == null || pack.Database == null) return;
            if (pack.Inventory.CountOf(LanternRule.ItemId) > 0) return;

            var def = pack.Database.GetById(LanternRule.ItemId);
            if (def != null) pack.Inventory.TryAdd(def, 1);
        }
    }
}
