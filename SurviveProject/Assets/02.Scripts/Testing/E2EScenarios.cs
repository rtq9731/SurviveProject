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
        public static IEnumerator Pickup()
        {
            var pickaxe = Resources.FindObjectsOfTypeAll<ToolItemSO>()
                                 .FirstOrDefault(t => t.id == "pickaxe");
            E2EHarness.Assert(pickaxe != null, "곡괭이 아이템 정의를 찾았다");

            var inv = E2EHarness.Player.Inventory;
            E2EHarness.Assert(inv != null, "PlayerInventory가 있다");

            int startCount = inv.Inventory.CountOf("pickaxe");

            // 카메라 정면 2.5m 앞에 줍기 대상을 놓는다
            var cam = E2EHarness.Eye;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "E2E_Pickup";
            go.transform.localScale = Vector3.one * 0.4f;
            go.transform.position = cam.transform.position + cam.transform.forward * 2.5f;

            var pickup = go.AddComponent<ItemPickup>();
            pickup.Setup(pickaxe, 1);
            E2EHarness.Log("  줍기 대상 배치: " + go.transform.position.ToString("F2"));

            yield return null;
            E2EHarness.LookAt(go.transform.position);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            E2EHarness.Assert(it != null, "PlayerInteractor가 있다");

            yield return E2EHarness.WaitUntil(
                () => it.Current != null, "줍기 대상이 탐지된다", 3f);
            E2EHarness.Log("  프롬프트: " + it.Current.InteractionPrompt);

            // 줍기는 홀드가 아니라 탭이다
            yield return E2EHarness.TapKey(Key.E);
            yield return null;

            yield return E2EHarness.WaitUntil(
                () => inv.Inventory.CountOf("pickaxe") > startCount,
                "곡괭이가 인벤토리에 들어왔다", 3f);

            E2EHarness.AssertEqual(inv.Inventory.CountOf("pickaxe"), startCount + 1, "곡괭이 수량");
            E2EHarness.Assert(go == null, "줍기 대상이 세계에서 사라졌다");
        }

        /// <summary>
        /// 홀드 채집. 프롬프트가 뜨는 것과 실제로 캐지는 것은 다른 문제다.
        /// 오늘 이 둘을 구별하지 못해 버그를 놓쳤다.
        /// </summary>
        public static IEnumerator HoldHarvest()
        {
            // 맨손으로 캘 수 있는 노드를 고른다.
            // 아무거나 잡으면 "곡괭이 필요"에 걸려 홀드가 시작조차 되지 않는다.
            var node = Object.FindObjectsByType<HarvestNode>(FindObjectsSortMode.None)
                             .FirstOrDefault(h => h.Definition != null &&
                                                  h.Definition.requiredTool == ToolType.None);
            E2EHarness.Assert(node != null, "맨손 채집 가능한 노드가 씬에 있다");

            var inv = E2EHarness.Player.Inventory;
            int start = inv.ScrapCount;

            // 카메라 정면으로 옮긴다. 지형에 막히지 않게 하려는 것이지
            // 검증을 건너뛰려는 것이 아니다 — 홀드 시간은 그대로 채운다.
            var cam = E2EHarness.Eye;
            node.transform.position = cam.transform.position + cam.transform.forward * 2.2f;
            yield return null;

            E2EHarness.LookAt(node.transform.position);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(
                () => it.Current != null, "채집 노드가 탐지된다", 3f);
            E2EHarness.Log("  프롬프트: " + it.Current.InteractionPrompt);

            float needSeconds = node.HoldDuration;
            E2EHarness.Log($"  필요 홀드 시간: {needSeconds:F2}초");

            // 진행 게이지가 실제로 차오르는지도 함께 본다
            float maxProgress = 0f;
            void OnProgress(float v) { if (v > maxProgress) maxProgress = v; }
            it.HoldProgressChanged += OnProgress;

            yield return E2EHarness.HoldKey(Key.E, needSeconds + 0.4f);
            yield return null;

            it.HoldProgressChanged -= OnProgress;

            E2EHarness.Assert(maxProgress > 0.5f, $"홀드 진행 게이지가 차올랐다 (최대 {maxProgress:F2})");
            E2EHarness.Assert(inv.ScrapCount > start,
                $"채집으로 자원을 얻었다 ({start} -> {inv.ScrapCount})");
        }

        /// <summary>
        /// 보행 진단. A2가 걷기에서 막혀서 원인을 좁히려고 만들었다.
        /// 목표 없이 정면으로 10초 걷고 실제 이동 거리를 잰다 —
        /// 입력이 안 들어가는 것인지, 들어가는데 못 가는 것인지 가른다.
        /// </summary>
        public static IEnumerator WalkDiagnostic()
        {
            var p = E2EHarness.Player.transform;
            Vector3 start = p.position;

            E2EHarness.Log($"  시작 위치 {start.ToString("F1")}");
            E2EHarness.Log($"  Keyboard.current = {Keyboard.current}");

            yield return E2EHarness.PressKey(Key.W);

            float t = 0f;
            Vector3 last = start;
            while (t < 10f)
            {
                E2EHarness.QueueKeys();
                float before = t;
                t += Time.deltaTime;

                if (Mathf.FloorToInt(t) > Mathf.FloorToInt(before))
                {
                    var now = p.position;
                    E2EHarness.Log($"    [{t:F0}s] 1초 이동 {Vector3.Distance(now, last):F2}m, " +
                                   $"위치 {now.ToString("F1")}, W눌림 {Keyboard.current.wKey.isPressed}");
                    last = now;
                }
                yield return null;
            }

            yield return E2EHarness.ReleaseKey(Key.W);

            float total = Vector3.Distance(p.position, start);
            E2EHarness.Log($"  10초 총 이동 {total:F1}m");
            E2EHarness.Assert(total > 20f, $"10초에 20m 이상 걸었다 (실제 {total:F1}m)");
        }

        /// <summary>목표 1의 트리거까지 실제로 걸어가는 것만 따로 본다.</summary>
        public static IEnumerator WalkToTrigger()
        {
            var trigger = GameObject.Find("Trigger_Surveyed");
            E2EHarness.Assert(trigger != null, "탐색 트리거가 있다");

            var from = E2EHarness.Player.transform.position;
            E2EHarness.Log($"  {from.ToString("F1")} -> {trigger.transform.position.ToString("F1")} " +
                           $"({Vector3.Distance(from, trigger.transform.position):F1}m)");

            yield return E2EHarness.WalkTo(trigger.transform.position, 3f, 30f);
        }

        /// <summary>탭으로는 채집이 되지 않아야 한다. 홀드형이라는 계약의 확인.</summary>
        public static IEnumerator TapDoesNotHarvest()
        {
            var node = Object.FindObjectsByType<HarvestNode>(FindObjectsSortMode.None)
                             .FirstOrDefault();
            E2EHarness.Assert(node != null, "채집 노드가 있다");

            var cam = E2EHarness.Eye;
            node.transform.position = cam.transform.position + cam.transform.forward * 2.2f;
            yield return null;
            E2EHarness.LookAt(node.transform.position);
            yield return null;
            yield return null;

            var inv = E2EHarness.Player.Inventory;
            int start = inv.ScrapCount;

            yield return E2EHarness.TapKey(Key.E);
            yield return null;
            yield return null;

            E2EHarness.AssertEqual(inv.ScrapCount, start, "탭만으로는 채집되지 않는다");
        }
    }
}
