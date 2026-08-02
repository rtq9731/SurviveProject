using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Harvesting;
using Survive.Interaction;
using Survive.Items;

namespace Survive.Testing
{
    /// <summary>
    /// E2E 시나리오 모음.
    ///
    /// 전부 <b>실제 조작</b>으로 검증한다. 플래그를 코드로 세우거나
    /// 인벤토리에 아이템을 직접 넣는 것은 통과로 치지 않는다 —
    /// 그러면 트리거·콜라이더·홀드 시간 같은 실제 조건을 건너뛴다.
    /// </summary>
    public static class E2EScenarios
    {
        /// <summary>
        /// A1 통과 기준. 하네스가 실제로 동작하는지 확인한다.
        /// 아이템을 바닥에 놓고, 걸어가서, 주워서, 인벤토리에 들어오는 것까지.
        /// </summary>
        public static IEnumerator 줍기()
        {
            var 곡괭이 = Resources.FindObjectsOfTypeAll<ToolItemSO>()
                                 .FirstOrDefault(t => t.id == "pickaxe");
            E2EHarness.단언(곡괭이 != null, "곡괭이 아이템 정의를 찾았다");

            var inv = E2EHarness.Player.Inventory;
            E2EHarness.단언(inv != null, "PlayerInventory가 있다");

            int 시작수량 = inv.Inventory.CountOf("pickaxe");

            // 카메라 정면 2.5m 앞에 줍기 대상을 놓는다
            var cam = E2EHarness.Eye;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "E2E_Pickup";
            go.transform.localScale = Vector3.one * 0.4f;
            go.transform.position = cam.transform.position + cam.transform.forward * 2.5f;

            var pickup = go.AddComponent<ItemPickup>();
            pickup.Setup(곡괭이, 1);
            E2EHarness.기록("  줍기 대상 배치: " + go.transform.position.ToString("F2"));

            yield return null;
            E2EHarness.바라보기(go.transform.position);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            E2EHarness.단언(it != null, "PlayerInteractor가 있다");

            yield return E2EHarness.기다리기(
                () => it.Current != null, "줍기 대상이 탐지된다", 3f);
            E2EHarness.기록("  프롬프트: " + it.Current.InteractionPrompt);

            // 줍기는 홀드가 아니라 탭이다
            yield return E2EHarness.키탭(Key.E);
            yield return null;

            yield return E2EHarness.기다리기(
                () => inv.Inventory.CountOf("pickaxe") > 시작수량,
                "곡괭이가 인벤토리에 들어왔다", 3f);

            E2EHarness.단언같음(inv.Inventory.CountOf("pickaxe"), 시작수량 + 1, "곡괭이 수량");
            E2EHarness.단언(go == null, "줍기 대상이 세계에서 사라졌다");
        }

        /// <summary>
        /// 홀드 채집. 프롬프트가 뜨는 것과 실제로 캐지는 것은 다른 문제다.
        /// 오늘 이 둘을 구별하지 못해 버그를 놓쳤다.
        /// </summary>
        public static IEnumerator 홀드채집()
        {
            // 맨손으로 캘 수 있는 노드를 고른다.
            // 아무거나 잡으면 "곡괭이 필요"에 걸려 홀드가 시작조차 되지 않는다.
            var node = Object.FindObjectsByType<HarvestNode>(FindObjectsSortMode.None)
                             .FirstOrDefault(h => h.Definition != null &&
                                                  h.Definition.requiredTool == ToolType.None);
            E2EHarness.단언(node != null, "맨손 채집 가능한 노드가 씬에 있다");

            var inv = E2EHarness.Player.Inventory;
            int 시작 = inv.ScrapCount;

            // 카메라 정면으로 옮긴다. 지형에 막히지 않게 하려는 것이지
            // 검증을 건너뛰려는 것이 아니다 — 홀드 시간은 그대로 채운다.
            var cam = E2EHarness.Eye;
            node.transform.position = cam.transform.position + cam.transform.forward * 2.2f;
            yield return null;

            E2EHarness.바라보기(node.transform.position);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.기다리기(
                () => it.Current != null, "채집 노드가 탐지된다", 3f);
            E2EHarness.기록("  프롬프트: " + it.Current.InteractionPrompt);

            float 필요시간 = node.HoldDuration;
            E2EHarness.기록($"  필요 홀드 시간: {필요시간:F2}초");

            // 진행 게이지가 실제로 차오르는지도 함께 본다
            float 최대진행 = 0f;
            void 진행관찰(float v) { if (v > 최대진행) 최대진행 = v; }
            it.HoldProgressChanged += 진행관찰;

            yield return E2EHarness.키홀드(Key.E, 필요시간 + 0.4f);
            yield return null;

            it.HoldProgressChanged -= 진행관찰;

            E2EHarness.단언(최대진행 > 0.5f, $"홀드 진행 게이지가 차올랐다 (최대 {최대진행:F2})");
            E2EHarness.단언(inv.ScrapCount > 시작,
                $"채집으로 자원을 얻었다 ({시작} -> {inv.ScrapCount})");
        }

        /// <summary>탭으로는 채집이 되지 않아야 한다. 홀드형이라는 계약의 확인.</summary>
        public static IEnumerator 탭으로는채집안됨()
        {
            var node = Object.FindObjectsByType<HarvestNode>(FindObjectsSortMode.None)
                             .FirstOrDefault();
            E2EHarness.단언(node != null, "채집 노드가 있다");

            var cam = E2EHarness.Eye;
            node.transform.position = cam.transform.position + cam.transform.forward * 2.2f;
            yield return null;
            E2EHarness.바라보기(node.transform.position);
            yield return null;
            yield return null;

            var inv = E2EHarness.Player.Inventory;
            int 시작 = inv.ScrapCount;

            yield return E2EHarness.키탭(Key.E);
            yield return null;
            yield return null;

            E2EHarness.단언같음(inv.ScrapCount, 시작, "탭만으로는 채집되지 않는다");
        }
    }
}
