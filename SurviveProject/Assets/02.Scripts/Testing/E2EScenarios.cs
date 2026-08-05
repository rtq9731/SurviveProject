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

        /// <summary>
        /// 홀드 게이지가 <b>화면에</b> 나타나는지 본다.
        /// 이벤트가 오는 것과 눈에 보이는 것은 다른 문제다 —
        /// 이벤트 검증만 통과시켜 놓고 정작 안 보이는 상태로 두 번 넘어갔다.
        /// </summary>
        public static IEnumerator HoldGauge()
        {
            var node = Object.FindObjectsByType<HarvestNode>(FindObjectsSortMode.None)
                             .FirstOrDefault(h => !h.IsDepleted && h.Definition != null &&
                                                  h.Definition.requiredTool == ToolType.None);
            E2EHarness.Assert(node != null, "맨손 채집 노드가 있다");

            var view = Object.FindFirstObjectByType<Survive.UI.HoldProgressView>(FindObjectsInactive.Include);
            E2EHarness.Assert(view != null, "HoldProgressView가 씬에 있다");

            var group = view.GetComponent<CanvasGroup>();
            var fill = view.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                           .FirstOrDefault(i => i.type == UnityEngine.UI.Image.Type.Filled);
            E2EHarness.Assert(group != null, "CanvasGroup이 있다");
            E2EHarness.Assert(fill != null, "Filled 이미지가 있다");

            var cam = E2EHarness.Eye;
            node.transform.position = cam.transform.position + cam.transform.forward * 2.2f;
            yield return null;
            E2EHarness.LookAt(node.transform.position);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current != null, "노드가 탐지된다", 3f);

            float maxAlpha = 0f, maxFill = 0f;
            bool holding = true;

            E2EHarness.Log($"  홀드 전: alpha={group.alpha:F2} fill={fill.fillAmount:F2}");

            // 홀드하면서 매 프레임 화면 값을 본다
            E2EHarness.Player.StartCoroutine(Sample());
            IEnumerator Sample()
            {
                while (holding)
                {
                    if (group.alpha > maxAlpha) maxAlpha = group.alpha;
                    if (fill.fillAmount > maxFill) maxFill = fill.fillAmount;
                    yield return null;
                }
            }

            yield return E2EHarness.HoldKey(Key.E, node.HoldDuration * 0.8f);
            holding = false;
            yield return null;

            E2EHarness.Log($"  홀드 중 최대: alpha={maxAlpha:F2} fill={maxFill:F2}");
            E2EHarness.Assert(maxAlpha > 0.5f, $"게이지가 화면에 나타난다 (최대 alpha {maxAlpha:F2})");
            E2EHarness.Assert(maxFill > 0.3f, $"게이지가 차오른다 (최대 fill {maxFill:F2})");

            yield return E2EHarness.ReleaseAllKeys();
        }

        /// <summary>
        /// 홀드 중간에 시간을 멈춘다. 밖에서 화면을 찍어 눈으로 확인하기 위한 것이다.
        /// 값이 맞는 것과 보기 좋은 것은 다르고, 후자는 사람이 봐야 안다.
        /// </summary>
        public static IEnumerator HoldGaugeFreeze()
        {
            var node = Object.FindObjectsByType<HarvestNode>(FindObjectsSortMode.None)
                             .FirstOrDefault(h => !h.IsDepleted && h.Definition != null &&
                                                  h.Definition.requiredTool == ToolType.None);
            E2EHarness.Assert(node != null, "맨손 채집 노드가 있다");

            var cam = E2EHarness.Eye;
            node.transform.position = cam.transform.position + cam.transform.forward * 2.2f;
            yield return null;
            E2EHarness.LookAt(node.transform.position);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current != null, "노드가 탐지된다", 3f);

            yield return E2EHarness.PressKey(Key.E);

            float t = 0f;
            float half = node.HoldDuration * 0.5f;
            while (t < half)
            {
                E2EHarness.QueueKeys();
                t += Time.deltaTime;
                yield return null;
            }

            Time.timeScale = 0f;
            E2EHarness.Log("  진행 약 50%에서 정지. 확인 후 E2EScenarios.Resume()을 부른다");

            // 여기서 끝내면 안 된다. 시나리오가 끝나는 순간 러너가 가상 키보드를
            // 치우고, 그러면 E가 떨어져 홀드가 취소되면서 게이지가 사라진다.
            // 사람이 화면을 볼 때까지 키를 누른 채로 기다린다.
            _frozen = true;
            while (_frozen)
            {
                E2EHarness.QueueKeys();
                yield return null;
            }

            yield return E2EHarness.ReleaseAllKeys();
        }

        static bool _frozen;

        /// <summary>HoldGaugeFreeze가 멈춘 시간을 되돌린다.</summary>
        public static void Resume()
        {
            _frozen = false;
            Time.timeScale = 1f;
        }

        /// <summary>
        /// 곡괭이로 부수고, 떨어진 것이 바닥에 남은 상태에서 시간을 멈춘다.
        /// 밖에서 화면을 찍어 "부서지고 떨어졌다"가 실제로 보이는지 확인한다.
        /// </summary>
        public static IEnumerator BreakFreeze()
        {
            var db = E2EHarness.Player.Inventory.Database;
            var pickaxe = db != null ? db.GetById("pickaxe") : null;
            E2EHarness.Assert(pickaxe != null, "곡괭이 정의를 찾았다");

            E2EHarness.Player.Inventory.Inventory.TryAdd(pickaxe, 1);
            var user = E2EHarness.Player.GetComponent<Survive.Player.PlayerToolUser>();
            E2EHarness.Assert(user != null && user.EquipFirst("pickaxe"), "곡괭이를 장착했다");
            yield return null;

            var node = Object.FindObjectsByType<HarvestNode>(FindObjectsSortMode.None)
                             .FirstOrDefault(h => !h.IsDepleted && h.IsBreakable);
            E2EHarness.Assert(node != null, "부술 수 있는 노드가 있다");

            // 원점이 아니라 콜라이더 한가운데를 눈앞에 둔다. 부술 수 있는 것이
            // 광맥뿐이던 시절은 끝났고(백로그 35의 거대 버섯), 원점이 발밑에 있는
            // 것은 원점을 겨누면 몸통이 전방 원뿔 위로 벗어난다.
            var cam = E2EHarness.Eye;
            var body = node.GetComponentInChildren<Collider>(true);
            Vector3 aim = cam.transform.position + cam.transform.forward * 2.2f;
            if (body != null) node.transform.position += aim - body.bounds.center;
            else node.transform.position = aim;
            yield return null;
            E2EHarness.LookAt(body != null ? body.bounds.center : node.transform.position);
            yield return null;
            yield return null;

            for (int swing = 0; swing < 25 && !node.IsDepleted; swing++)
            {
                yield return E2EHarness.ClickAttack();
                float t = 0f;
                while (t < 0.35f && !node.IsDepleted) { t += Time.deltaTime; yield return null; }
            }
            E2EHarness.Assert(node.IsDepleted, "노드가 부서졌다");

            // 파편이 아직 날고 있을 때 멈춘다. 파티클 수명이 1초 남짓이라
            // 착지까지 기다리면 정작 확인하려던 연출이 사라진 뒤다.
            float w = 0f;
            while (w < 0.3f) { w += Time.deltaTime; yield return null; }

            int drops = Object.FindObjectsByType<Survive.Interaction.ItemPickup>(FindObjectsSortMode.None)
                              .Count(p => p.name.StartsWith("Drop_"));
            E2EHarness.Assert(drops > 0, $"바닥에 떨어진 것이 있다 ({drops}개)");

            // 떨어진 것 하나를 바라본 채로 멈춘다
            var one = Object.FindObjectsByType<Survive.Interaction.ItemPickup>(FindObjectsSortMode.None)
                            .FirstOrDefault(p => p.name.StartsWith("Drop_"));
            if (one != null) E2EHarness.LookAt(one.transform.position);
            yield return null;
            yield return null;

            Time.timeScale = 0f;
            _frozen = true;
            while (_frozen) yield return null;
        }

        /// <summary>목표 1의 트리거 안까지 실제로 걸어 들어가는 것만 따로 본다.</summary>
        public static IEnumerator WalkToTrigger()
        {
            var trigger = GameObject.Find("Trigger_Surveyed");
            E2EHarness.Assert(trigger != null, "탐색 트리거가 있다");

            var from = E2EHarness.Player.transform.position;
            E2EHarness.Log($"  {from.ToString("F1")} -> {trigger.transform.position.ToString("F1")} " +
                           $"({Vector3.Distance(from, trigger.transform.position):F1}m)");

            // 판의 중심은 포탈 장치 너머 바위 위다. 목표가 요구하는 것은 판을 밟는 것뿐이고,
            // 중심을 목표로 잡으면 게임이 아니라 검사가 실패한다.
            yield return E2EHarness.WalkInto(trigger, 60f);
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
