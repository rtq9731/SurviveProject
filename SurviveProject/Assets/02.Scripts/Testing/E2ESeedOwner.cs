using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Survive.Core;
using Survive.Harvesting;
using Survive.Items;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>난수에 주인이 섰다.</b>
    ///
    /// 지금까지 이 세계의 난수는 주인이 <b>0</b>이었다 — <c>Random.InitState</c>
    /// 0건, <c>System.Random</c>은 다섯 곳 전부 <c>new System.Random()</c>(시각 시드).
    /// 그래서 <b>같은 판을 두 번 돌려도 같은 것이 나오지 않았다</b>. 「낫 꼬리」
    /// 36% 플레이크가 그 성질이었고, 전리품 표 14개가 전부 수량 범위를 갖고
    /// 6개가 확률 항목을 가지므로 같은 성질이 열 자리에 깔려 있었다.
    ///
    /// 여기서 셋을 본다.
    /// <list type="number">
    /// <item><b>같은 씨앗으로 두 번 돌리면 전리품이 같다</b> (3회 연속).
    ///   이것이 오늘의 이득을 증명하는 자리다.</item>
    /// <item><b>씨앗이 다르면 세계가 갈린다</b> — 시드 하나가 세계를
    ///   똑같이 만드는 것이 아니라, 세계가 같아야 같아진다.</item>
    /// <item><b>씨앗이 저장을 건넌다</b> — 불러온 세계의 저 덤불이 저장하기
    ///   전과 같은 것을 떨군다.</item>
    /// </list>
    ///
    /// <b>기본 슬롯을 밟지 않는다.</b> 세 클론과 사람의 에디터가 같은 저장 경로를
    /// 쓴다. 이 검사는 자기 슬롯만 쓰고 끝나면 지운다.
    /// </summary>
    public static class E2ESeedOwner
    {
        /// <summary>이 검사 전용 슬롯.</summary>
        const string 슬롯 = "e2e_seed";

        const int 씨앗 = 20260807;
        const int 다른씨앗 = 4242;

        static readonly List<string> _표 = new List<string>();

        public static IEnumerator FullRun()
        {
            _표.Clear();

            yield return 준비();
            yield return 같은_씨앗은_같은_전리품을_준다();
            yield return 씨앗이_다르면_세계가_갈린다();
            yield return 씨앗이_저장을_건넌다();
            yield return 뒷정리();

            E2EHarness.Log("");
            E2EHarness.Log("═══ 실측표 ═══");
            foreach (var line in _표) E2EHarness.Log("  " + line);
            E2EHarness.Log("=== 시드 주인 완주 ===");
        }

        static void 적는다(string 항목, string 값) => _표.Add($"{항목} | {값}");

        // ── 준비 ────────────────────────────────────────────────

        static SaveCoordinator 저장소;
        static int 처음시드;

        static IEnumerator 준비()
        {
            int 잠든것 = E2EHarness.SleepWildCreatures();
            E2EHarness.Log($"  야생 생물 {잠든것}마리를 재웠다");

            저장소 = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Include);
            E2EHarness.Assert(저장소 != null, "SaveCoordinator가 씬에 있다");
            저장소?.Collect();

            처음시드 = WorldSeed.Value;
            E2EHarness.Assert(WorldSeed.Started,
                "재생을 켠 것만으로 세계에 씨앗이 섰다 (아무도 안 불러도 주인이 있다)");
            E2EHarness.Assert(처음시드 != 0, "그 씨앗은 0이 아니다");
            E2EHarness.Log($"  이번 판의 씨앗 {처음시드}");

            적는다("판이 스스로 세운 씨앗", 처음시드.ToString());
            yield return null;
        }

        static IEnumerator 뒷정리()
        {
            저장소?.Delete(슬롯);

            // 검사가 앉힌 세계를 원래대로 돌려놓는다. 이어 도는 시나리오가
            // 이 씨앗을 물려받으면 그쪽이 재는 것이 자기 세계가 아니게 된다.
            WorldSeed.Restore(처음시드);

            E2EHarness.Log("  검사용 슬롯을 지웠다 (기본 슬롯은 건드리지 않았다)");
            yield return null;
        }

        // ── 1. 같은 씨앗 → 같은 전리품 ──────────────────────────

        /// <summary>
        /// <b>세 번 연속으로 본다.</b> 한 번 같은 것은 우연일 수 있다 —
        /// 특히 전리품 표는 확률 항목이 빠지면 「둘 다 빈손」으로도 같아지므로,
        /// 세 판을 돌려 <b>적어도 한 판은 실제로 무언가 나왔는지</b>도 함께 본다.
        /// </summary>
        static IEnumerator 같은_씨앗은_같은_전리품을_준다()
        {
            E2EHarness.Log("— 같은 씨앗으로 두 번 돌리면 전리품이 같다 (3회 연속) —");

            int 빈손이_아닌_판 = 0;
            string 마지막 = "";

            for (int 회 = 1; 회 <= 3; 회++)
            {
                var node = 노드("LooseScrap");
                E2EHarness.Assert(node != null, $"{회}회차: 아직 안 캔 흩어진 잔해가 있다");
                if (node == null) yield break;

                var 자리 = node.transform.position;
                var 자세 = node.transform.rotation;

                WorldSeed.Restore(씨앗);
                string 첫판 = 캔다(node);
                yield return null;
                E2EHarness.Assert(node.IsDepleted, $"{회}회차: 잔해를 캤다");

                // 물체를 죽이고 같은 자리에 새로 세운다 = 씬을 다시 읽는 것과 같다.
                // 필드에 살던 것(굴림 횟수까지)이 그때 통째로 사라진다.
                var 새노드 = 다시_세운다(node, 자리, 자세);
                yield return null;
                E2EHarness.Assert(새노드 != null && !새노드.IsDepleted,
                    $"{회}회차: 같은 자리에 아무것도 모르는 노드가 새로 섰다");
                if (새노드 == null) yield break;

                WorldSeed.Restore(씨앗);
                string 둘째판 = 캔다(새노드);
                yield return null;

                E2EHarness.AssertEqual(둘째판, 첫판,
                    $"{회}회차: 같은 씨앗·같은 자리에서 같은 것이 나왔다");

                if (첫판.Length > 0) 빈손이_아닌_판++;
                마지막 = 첫판;
                E2EHarness.Log($"  {회}회차 {자리.x:F1},{자리.z:F1} → [{첫판}]");
            }

            // 확률 항목이 셋 다 빠지면 「둘 다 빈손」으로도 같아진다. 그것으로는
            // 아무것도 증명되지 않으므로, 적어도 한 판은 실제로 무언가 나와야 한다.
            E2EHarness.Assert(빈손이_아닌_판 > 0,
                $"세 판 중 {빈손이_아닌_판}판에서 실제로 무언가 나왔다 (빈손끼리 같은 것은 증명이 아니다)");

            적는다("같은 씨앗 · 같은 자리",
                   $"3회 연속 같은 전리품 (빈손이 아닌 판 {빈손이_아닌_판}/3, 마지막 [{마지막}])");
        }

        // ── 2. 씨앗이 다르면 갈린다 ─────────────────────────────

        /// <summary>
        /// <b>노드를 캐지 않고 본다.</b> 실제 전리품 표(에셋)를 세계 곳곳의
        /// <b>진짜 자리</b>에서 굴려, 씨앗을 바꾸면 얼마나 갈리는지 센다.
        /// 캐어 보는 것보다 표본이 훨씬 크고, 씬의 노드를 축내지 않는다.
        /// </summary>
        static IEnumerator 씨앗이_다르면_세계가_갈린다()
        {
            E2EHarness.Log("— 씨앗이 다르면 세계가 갈린다 —");

            var 자리들 = 모든노드()
                .Where(n => n.Definition != null && n.Definition.drops != null)
                .Take(24)
                .Select(n => (표: n.Definition.drops, 자리: n.transform.position))
                .ToArray();

            E2EHarness.Assert(자리들.Length >= 8,
                $"전리품 표를 가진 노드가 {자리들.Length}곳 있다 (8곳 이상이면 잰다)");
            if (자리들.Length < 8) yield break;

            WorldSeed.Restore(씨앗);
            var 이쪽 = 자리들.Select(t => 굴린다(t.표, t.자리)).ToArray();

            // 같은 씨앗으로 다시 굴리면 한 줄도 안 달라진다.
            WorldSeed.Restore(씨앗);
            var 또이쪽 = 자리들.Select(t => 굴린다(t.표, t.자리)).ToArray();
            E2EHarness.Assert(이쪽.SequenceEqual(또이쪽),
                "같은 씨앗으로 다시 굴린 세계는 한 자리도 안 달라졌다");

            // 한 세계 안에서는 자리마다 다르다 — 시드 하나가 세계를 똑같이 만들지 않는다.
            int 모양수 = 이쪽.Distinct().Count();
            E2EHarness.Assert(모양수 > 1,
                $"자리 {자리들.Length}곳에서 나온 모양이 {모양수}가지다 " +
                "(1가지면 모든 채집물이 같은 것을 떨군다는 뜻이다)");

            WorldSeed.Restore(다른씨앗);
            var 저쪽 = 자리들.Select(t => 굴린다(t.표, t.자리)).ToArray();

            int 갈린수 = 이쪽.Where((v, i) => v != 저쪽[i]).Count();
            E2EHarness.Assert(갈린수 > 0,
                $"씨앗을 바꾸니 {자리들.Length}곳 중 {갈린수}곳이 갈렸다 (0곳이면 시드가 결과에 안 닿는다)");

            E2EHarness.Log($"  자리 {자리들.Length}곳 · 한 세계 안의 모양 {모양수}가지 · " +
                           $"씨앗을 바꾸니 {갈린수}곳이 갈렸다");
            적는다("씨앗이 다르면", $"자리 {자리들.Length}곳 중 {갈린수}곳이 갈렸다 (같은 씨앗은 0곳)");
        }

        // ── 3. 씨앗이 저장을 건넌다 ─────────────────────────────

        /// <summary>
        /// <b>시드는 세계 상태다.</b> 불러온 세계가 다른 것을 떨구면 그것은 같은
        /// 세계가 아니고, 저장 직전에 본 「저 덤불에는 무엇이 있다」가 불러오는
        /// 순간 거짓이 된다. 시각이 저장되는 것과 같은 이유이고, 실리는 자리도
        /// 같다 — 저장본의 「세계」 절이다.
        ///
        /// <b>굴림의 결과는 저장하지 않는다.</b> 저장하는 것은 씨앗 하나뿐이고,
        /// 굴림은 불러온 뒤 그때그때 일어난다. 그런데도 답이 같다 —
        /// 그것이 파생이 하는 일이다.
        /// </summary>
        static IEnumerator 씨앗이_저장을_건넌다()
        {
            E2EHarness.Log("— 씨앗이 저장을 건넌다 —");

            var node = 노드("LooseScrap");
            E2EHarness.Assert(node != null, "아직 안 캔 흩어진 잔해가 있다");
            if (node == null || node.Definition?.drops == null) yield break;

            var 자리 = node.transform.position;

            // 아직 캐지 않는다. 씨앗만 앉히고 「저 자리에서 무엇이 나올지」를 적어 둔다.
            WorldSeed.Restore(씨앗);
            string 예상 = 굴린다(node.Definition.drops, 자리);

            저장소.Save(슬롯);
            yield return null;

            // 다른 세계로 갈아탄다 — 새 판을 켠 것과 같다.
            WorldSeed.Restore(다른씨앗);
            E2EHarness.AssertEqual(WorldSeed.Value, 다른씨앗, "다른 세계로 갈아탔다");

            E2EHarness.Assert(저장소.Load(슬롯), "저장본이 열렸다");
            yield return null;

            E2EHarness.AssertEqual(WorldSeed.Value, 씨앗,
                "불러오니 저장해 둔 씨앗으로 돌아왔다 (시드가 「세계」 절에 실렸다)");

            // 그리고 진짜로 캐 본다. 저장한 것은 씨앗뿐인데 답이 같다.
            string 실제 = 캔다(node);
            yield return null;

            E2EHarness.AssertEqual(실제, 예상,
                "불러온 세계의 그 자리가 저장하기 전과 같은 것을 떨궜다");

            적는다("씨앗이 저장을 건넌다", $"씨앗 {씨앗} · 저장 → 다른 세계 → 불러오기 → [{실제}]");
        }

        // ── 잔 도구들 ───────────────────────────────────────────

        /// <summary>표를 굴려 결과를 한 줄로. 노드를 축내지 않는다.</summary>
        static string 굴린다(LootTableSO 표, Vector3 자리) =>
            한줄로(표.Roll(WorldSeed.Rng(WorldSeedBranch.HarvestLoot, 자리, 0)));

        static string 한줄로(IEnumerable<ItemStack> 굴림) =>
            string.Join(",", 굴림.Where(s => s != null && s.item != null)
                                 .Select(s => $"{s.item.id}x{s.count}")
                                 .OrderBy(s => s));

        /// <summary>노드를 캐고 <b>실제로 소지품에 들어온 것</b>을 한 줄로.</summary>
        static string 캔다(HarvestNode node)
        {
            var 전 = 소지품();
            node.Interact(E2EHarness.Player);
            var 후 = 소지품();

            var 델타 = new List<string>();
            foreach (var kv in 후)
            {
                전.TryGetValue(kv.Key, out int 있던것);
                if (kv.Value > 있던것) 델타.Add($"{kv.Key}x{kv.Value - 있던것}");
            }
            델타.Sort();
            return string.Join(",", 델타);
        }

        static Dictionary<string, int> 소지품()
        {
            var 표 = new Dictionary<string, int>();
            var inv = E2EHarness.Player?.Inventory?.Inventory;
            if (inv == null) return 표;

            foreach (var slot in inv.Slots)
            {
                if (slot == null || slot.IsEmpty) continue;
                표.TryGetValue(slot.item.id, out int n);
                표[slot.item.id] = n + slot.count;
            }
            return 표;
        }

        /// <summary>
        /// 물체를 죽이고 <b>같은 자리에</b> 같은 것을 새로 세운다. 씬을 다시 읽는
        /// 것과 같은 일이다 — 필드에 살던 상태(굴림 횟수 포함)가 그때 사라진다.
        /// 꺼진 부모 밑에 복제하는 이유는 <c>E2EWorldLedger</c>에 적어 두었다:
        /// 그냥 복제하면 원본과 사본이 같은 신원으로 동시에 등록된다.
        /// </summary>
        static HarvestNode 다시_세운다(HarvestNode 원본, Vector3 자리, Quaternion 자세)
        {
            if (원본 == null) return null;

            var 보관 = new GameObject("E2E_사본보관");
            보관.SetActive(false);

            var 사본 = Object.Instantiate(원본.gameObject, 보관.transform);
            Object.DestroyImmediate(원본.gameObject);

            사본.transform.SetParent(null, false);
            사본.transform.SetPositionAndRotation(자리, 자세);
            Object.Destroy(보관);

            사본.SetActive(true);
            E2EHarness.SyncPhysics();
            return 사본.GetComponent<HarvestNode>();
        }

        static IEnumerable<HarvestNode> 모든노드() =>
            Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include)
                  .Where(n => n != null);

        /// <summary>이 정의의, 아직 안 캔 노드 하나. 사람에게서 먼 것을 고른다.</summary>
        static HarvestNode 노드(string 정의이름)
        {
            var 눈 = E2EHarness.Eye != null ? E2EHarness.Eye.transform.position : Vector3.zero;
            return 모든노드()
                .Where(n => n.Definition != null && n.Definition.name == 정의이름 && !n.IsDepleted)
                .OrderByDescending(n => Vector3.Distance(눈, n.transform.position))
                .FirstOrDefault();
        }
    }
}
