using System.Collections;
using System.Linq;
using UnityEngine;
using Survive.Harvesting;
using Survive.Items;
using Survive.Localization;
using Survive.Player;

namespace Survive.Testing
{
    /// <summary>
    /// 지상에 서 있는 채집물이 <b>화면에서도</b> 제 이름으로 불리는가.
    ///
    /// <b>왜 이 시나리오가 필요한가.</b> 2026-08-07 검토 회신 ①이 지상에서 매크로늄을
    /// 없앴다. 지상은 스크랩과 기계 부품만 나오는 학습·준비 구간이고 새 물질은
    /// 지하부터다. 그런데 씬에는 매크로늄의 이름을 단 채집물이 열세 곳 서 있었다 —
    /// 실제로 떨구는 것은 매크로늄이 아니라 <b>이종 합금</b>인데도. 물건은 맞고
    /// 화면만 어긋난 상태였다.
    ///
    /// <b>EditMode로는 여기까지 못 본다.</b> 에셋의 <c>displayName</c>과 표의 칸이
    /// 맞는지는 EditMode가 보지만, 그 값이 <b>조준 프롬프트까지 실제로 흘러가는지</b>는
    /// 재생해 봐야 안다. 사이에 <c>DataText</c>·<c>Loc</c>·<c>Prompt</c> 서식이
    /// 끼어 있고, 그중 하나만 옛 키를 들고 있어도 화면에는 옛 이름이 남는다.
    ///
    /// <b>씬을 건드리지 않는다.</b> 노드를 옮기지 않고 플레이어를 그 앞에 세운다.
    /// 부수지도 않는다 — 뒤에 도는 시나리오가 밟을 세계를 줄이지 않기 위해서다.
    /// </summary>
    public static class E2EOreVeinName
    {
        /// <summary>씬에서 이 채집물을 고르는 열쇠. <b>표시 이름이 아니라 에셋 이름</b>이다 —
        /// 표시 이름으로 고르면 이름이 틀렸을 때 노드를 못 찾아 "없다"고만 말한다.</summary>
        const string 노드에셋이름 = "OreVein";

        /// <summary>이 채집물이 떨구는 것. 이름이 이것과 맞아야 한다는 것이 이 검사의 핵심이다.</summary>
        const string 떨구는것 = "alien_alloy";

        /// <summary>지상에서 사라진 말. 이 낱말이 프롬프트에 다시 나오면 실패다.</summary>
        const string 없앤말 = "매크로늄";

        /// <summary>
        /// 카메라에서 이만큼 안이면 조준이 걸린다. <c>PlayerInteractor.detectDistance</c>가
        /// 3m이므로 여유를 조금 남긴다 — 딱 맞추면 한 프레임의 흔들림에 검사가 갈린다.
        /// </summary>
        const float 닿는거리 = 2.4f;

        /// <summary>노드 한가운데에서 이만큼 물러나 선다. 눈높이를 더해도 <see cref="닿는거리"/> 안이다.</summary>
        const float 설거리 = 1.2f;

        public static IEnumerator FullRun()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            // 야생 생물이 앞을 지나가면 조준선이 그쪽을 먼저 잡는다.
            E2EHarness.SleepWildCreatures();
            yield return null;

            var node = 씬에서_찾는다();
            E2EHarness.Assert(node != null, $"씬에 {노드에셋이름} 채집물이 서 있다");
            if (node == null) yield break;

            var 정의 = node.Definition;
            string 이름 = DataText.Name(정의);
            E2EHarness.Log($"  표시 이름: \"{이름}\" (에셋 {정의.name})");

            // ── ① 이름이 나오는 물건과 맞는가 ──────────────────
            //
            // 이름을 고른 근거를 검사가 들고 있어야 한다. 떨구는 것이 바뀌면
            // 이름도 다시 봐야 한다는 것을, 그때 이 줄이 말해 준다.
            var 떨굼 = 정의.drops?.entries?
                .Where(e => e?.item != null)
                .Select(e => e.item.id)
                .ToList();
            E2EHarness.Assert(떨굼 != null && 떨굼.Contains(떨구는것),
                $"이 채집물은 {떨구는것}을 떨군다 (지금: " +
                $"{(떨굼 == null ? "없음" : string.Join(", ", 떨굼))})");

            E2EHarness.Assert(!이름.Contains(없앤말),
                $"표시 이름에 「{없앤말}」이 없다 — 지상에서 없앤 말이다");

            // ── ② 맨손으로 조준 — 이름과 필요한 도구를 말한다 ──
            맨손으로_만든다();
            yield return null;
            yield return 앞에_선다(node);

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current != null, "채집물이 조준된다", 5f);

            node.CanInteract(E2EHarness.Player);   // 지금 든 것(맨손)을 물려 준다
            string 맨손문구 = node.InteractionPrompt;
            E2EHarness.Log("  맨손 프롬프트: " + 맨손문구);
            E2EHarness.Assert(맨손문구.Contains(이름), $"프롬프트가 「{이름}」이라고 말한다");
            E2EHarness.Assert(!맨손문구.Contains(없앤말), $"프롬프트에 「{없앤말}」이 없다");
            E2EHarness.Assert(맨손문구.Contains(ToolMatch.Name(ToolType.Pickaxe)),
                              "맨손에는 곡괭이가 필요하다고 말한다");

            // ── ③ 곡괭이를 들고 조준 — 문구가 바뀌어도 이름은 그대로 ──
            준다("pickaxe", 1);
            var user = E2EHarness.Player.GetComponent<PlayerToolUser>();
            E2EHarness.Assert(user != null && user.EquipFirst("pickaxe"), "곡괭이를 들었다");
            yield return null;
            yield return 앞에_선다(node);

            node.CanInteract(E2EHarness.Player);
            string 곡괭이문구 = node.InteractionPrompt;
            E2EHarness.Log("  곡괭이 프롬프트: " + 곡괭이문구);
            E2EHarness.Assert(곡괭이문구.Contains(이름), $"부술 때도 「{이름}」이라고 말한다");
            E2EHarness.Assert(!곡괭이문구.Contains(없앤말), $"부술 때도 「{없앤말}」이 없다");

            // ── ④ 조준선이 실제로 잡은 것 — 이것이 화면에 뜨는 글자다 ──
            //
            // ②③은 노드에서 직접 읽은 값이다. 화면의 프롬프트 띠는 그것이 아니라
            // <c>PlayerInteractor.Current</c>가 내는 값을 적는다 — 조준선이 남의 것
            // (지형·버섯 기둥)을 먼저 잡으면 화면에는 그쪽 글자가 뜬다. 사람이 볼
            // 것을 말하려면 여기까지 와야 한다. 스크린샷도 이 상태에서 찍는다.
            yield return E2EHarness.WaitUntil(
                () => it.Current != null && it.Current.InteractionPrompt.Contains(이름),
                $"조준선이 잡은 것의 프롬프트가 「{이름}」이라고 말한다", 5f);

            string 화면문구 = it.Current.InteractionPrompt;
            E2EHarness.Log("  조준된 것: " + 화면문구);
            E2EHarness.Assert(!화면문구.Contains(없앤말),
                $"화면에 뜨는 글자에 「{없앤말}」이 없다 — {화면문구}");

            E2EHarness.Assert(!node.IsDepleted, "채집물을 부수지 않고 두었다");
            E2EHarness.Log("=== 채집물 이름 확인 완주 ===");
        }

        // ── 도구 ────────────────────────────────────────────────

        /// <summary>플레이어에게 제일 가까운 그 채집물. 멀리 있는 것을 고르면 텔레포트가 지형을 탄다.</summary>
        static HarvestNode 씬에서_찾는다()
        {
            Vector3 나 = E2EHarness.Player.transform.position;
            return Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Exclude)
                .Where(h => !h.IsDepleted && h.Definition != null &&
                            h.Definition.name == 노드에셋이름)
                .OrderBy(h => (h.transform.position - 나).sqrMagnitude)
                .FirstOrDefault();
        }

        /// <summary>
        /// 노드를 옮기지 않는다. 씬에 놓인 그대로 두고 사람이 그 앞에 선다.
        ///
        /// <b>왜 <see cref="E2EHarness.StandInFrontOf"/>를 안 쓰는가.</b> 두 가지가 걸렸다.
        /// <list type="number">
        /// <item><b>수평 거리로는 모자란다.</b> 이 채집물은 납작해서(높이 0.3m 남짓)
        ///   지면에 거의 붙어 있다. 눈높이가 1.5m라 수평 2.2m에 서면 카메라에서
        ///   노드까지가 2.6m를 넘고, 지면 기복이 조금만 도우면 <c>PlayerInteractor</c>의
        ///   탐지 거리 3m를 벗어난다. 조준 각도는 0.0도로 완벽한데 아무것도 안 잡힌다.</item>
        /// <item><b>제자리에서 다시 부르면 사람이 하늘로 올라간다.</b> 발밑 높이를
        ///   위에서 쏜 광선으로 재는데, 이미 그 자리에 서 있으면 그 광선이
        ///   <b>자기 몸</b>을 먼저 맞는다. 그러면 "머리 위 1m"에 다시 서고, 부를수록
        ///   높아진다. 실측으로 두 번째 호출에서 5.2m까지 멀어졌다.</item>
        /// </list>
        /// 그래서 거리는 <b>카메라에서</b> 재고, 지면은 <b>자기 몸과 노드를 건너뛰고</b> 잰다.
        /// </summary>
        static IEnumerator 앞에_선다(HarvestNode node)
        {
            var 몸 = node.GetComponentInChildren<Collider>(true);
            E2EHarness.SyncPhysics();
            Vector3 겨냥 = 몸 != null ? 몸.bounds.center : node.transform.position;

            // 노드에서 사람 쪽으로 물러난 자리. 이미 그 앞에 서 있어도 방향이 흔들리지 않는다.
            Vector3 뒤로 = E2EHarness.Player.transform.position - 겨냥;
            뒤로.y = 0f;
            if (뒤로.sqrMagnitude < 0.01f) 뒤로 = Vector3.back;

            Vector3 설자리 = 겨냥 + 뒤로.normalized * 설거리;
            설자리.y = 발밑높이(설자리, node) + 1.0f;

            E2EHarness.Teleport(설자리);
            yield return null;                 // 카메라가 따라올 프레임

            // 발이 땅에 닿을 때까지 기다린다. 순간이동은 지면보다 1m 위에 세우는데,
            // 그 1m가 그대로 눈높이에 더해져 카메라-노드 거리를 탐지 거리 밖으로
            // 밀어낸다. 세 프레임 뒤에 재면 아직 공중이라 늘 "멀다"가 나온다.
            var cc = E2EHarness.Player.GetComponent<CharacterController>();
            for (int f = 0; f < 120 && cc != null && !cc.isGrounded; f++) yield return null;

            // 반쯤 파묻힌 노드는 원점이 흙 속이라 원점을 겨누면 지면을 본다.
            // 콜라이더 한가운데를 겨눈다 — 사람이 눈으로 겨누는 자리가 그쪽이다.
            E2EHarness.LookAt(겨냥);
            yield return null;
            yield return null;                 // Cinemachine이 실제로 반영되는 데 한 프레임 더

            var cam = E2EHarness.Eye;
            float 거리 = Vector3.Distance(cam.transform.position, 겨냥);
            E2EHarness.Log($"    [조준] 어긋난 각 " +
                $"{Vector3.Angle(cam.transform.forward, 겨냥 - cam.transform.position):F1}도, " +
                $"거리 {거리:F2}m");
            E2EHarness.Assert(거리 <= 닿는거리,
                $"조준이 걸리는 거리 안에 섰다 ({거리:F2}m <= {닿는거리}m)");
        }

        /// <summary>
        /// 그 자리의 지면 높이. <b>자기 몸과 노드는 건너뛴다</b> —
        /// 둘 중 하나를 밟고 서면 부를 때마다 한 층씩 올라간다.
        /// </summary>
        static float 발밑높이(Vector3 자리, HarvestNode node)
        {
            var 나 = E2EHarness.Player.transform;
            float 최고 = 자리.y;
            bool 찾음 = false;

            foreach (var hit in Physics.RaycastAll(자리 + Vector3.up * 30f, Vector3.down, 200f,
                                                   ~0, QueryTriggerInteraction.Ignore))
            {
                var t = hit.collider.transform;
                if (t.IsChildOf(나) || t.IsChildOf(node.transform)) continue;
                if (!찾음 || hit.point.y > 최고) { 최고 = hit.point.y; 찾음 = true; }
            }

            return 찾음 ? 최고 : 자리.y;
        }

        static void 맨손으로_만든다()
        {
            var holder = E2EHarness.Player.ToolHolder;
            if (holder != null) holder.Unequip();
        }

        static void 준다(string id, int count)
        {
            var inv = E2EHarness.Player.Inventory.Inventory;
            var db = E2EHarness.Player.Inventory.Database;
            var item = db != null ? db.GetById(id) : null;
            if (item != null && inv.CountOf(id) < count) inv.TryAdd(item, count - inv.CountOf(id));
        }
    }
}
