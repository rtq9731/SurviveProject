using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Survive.Core;
using Survive.Harvesting;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>세계가 저장을 건넌다.</b>
    ///
    /// 지금까지 다 캔 자리·딴 갓·자란 단계는 전부 컴포넌트 필드에 살았고
    /// <b>씬 로드로 사라졌다</b>. 저장 포맷에 「세계」 절이 아예 없었으므로,
    /// 저장하고 불러오면 캐 놓은 세계가 통째로 처음 모습으로 되돌아갔다.
    ///
    /// 여기서 넷을 본다.
    /// <list type="number">
    /// <item><b>다 캔 자리가 저장을 건넌다</b> — 물체가 죽고 같은 자리에 새로
    ///   태어나도(= 씬을 다시 읽는 것과 같다) 불러온 세계에서 <b>다시 서 있지 않다</b>.</item>
    /// <item><b>재생이 저장을 건너서도 돈다</b> — 주기가 지나면 돌아온다.
    ///   다 캔 <b>시각</b>이 절대 세계 초로 실렸다는 뜻이다.</item>
    /// <item><b>돌아온 자리는 원장에서 빠진다</b> — 다시 저장하면 그 줄이 없다.
    ///   「다 캔 것」과 「이미 돌아온 것」이 갈리는 자리다.</item>
    /// <item><b>세계 절이 없는 옛 저장본이 그냥 열린다</b> — 그때 세계는
    ///   씬이 놓아둔 그대로다.</item>
    /// </list>
    ///
    /// <b>기본 슬롯을 밟지 않는다.</b> 세 클론과 사람의 에디터가 같은 저장 경로를
    /// 쓴다. 이 검사는 자기 슬롯만 쓰고 끝나면 지운다.
    /// </summary>
    public static class E2EWorldLedger
    {
        /// <summary>이 검사 전용 슬롯. 기본 슬롯(auto)은 사람과 다른 갈래의 것이다.</summary>
        const string 슬롯 = "e2e_world";

        /// <summary>「세계」 절을 도려낸 저장본을 넣어 둘 슬롯.</summary>
        const string 옛슬롯 = "e2e_world_legacy";

        /// <summary>검증용으로 줄인 재생 주기(초).</summary>
        const float 검증용재생초 = 4f;

        static readonly List<string> _표 = new List<string>();

        public static IEnumerator FullRun()
        {
            _표.Clear();

            yield return 준비();
            yield return 다_캔_자리가_저장을_건넌다();
            yield return 재생이_저장을_건너서도_돈다();
            yield return 돌아온_자리는_원장에서_빠진다();
            yield return 세계_절이_없는_저장본도_그냥_열린다();
            yield return 뒷정리();

            E2EHarness.Log("");
            E2EHarness.Log("═══ 실측표 ═══");
            foreach (var line in _표) E2EHarness.Log("  " + line);
            E2EHarness.Log("=== 세계 원장 완주 ===");
        }

        static void 적는다(string 항목, string 값) => _표.Add($"{항목} | {값}");

        // ── 준비 ────────────────────────────────────────────────

        static SaveCoordinator 저장소;

        static IEnumerator 준비()
        {
            int 잠든것 = E2EHarness.SleepWildCreatures();
            E2EHarness.Log($"  야생 생물 {잠든것}마리를 재웠다");

            저장소 = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Include);
            E2EHarness.Assert(저장소 != null, "SaveCoordinator가 씬에 있다");

            var 원장 = WorldLedgerService.Instance;
            E2EHarness.Assert(원장 != null, "세계 원장이 스스로 붙었다 (씬을 고치지 않는다)");

            // 실행 시점에 붙은 것들(원장·시계)이 저장 대상 목록에 들어와 있어야 한다.
            int 모은것 = 저장소.Collect();
            E2EHarness.Log($"  저장 대상 {모은것}개를 모았다");

            var 서비스 = 저장소.Service;
            E2EHarness.Assert(서비스 != null, "SaveService가 섰다");

            E2EHarness.Log($"  세계 시계 {WorldClock.Seconds:F1}초");
            yield return null;
        }

        static IEnumerator 뒷정리()
        {
            저장소?.Delete(슬롯);
            저장소?.Delete(옛슬롯);
            E2EHarness.Log("  검사용 슬롯을 지웠다 (기본 슬롯은 건드리지 않았다)");
            yield return null;
        }

        // ── 1. 다 캔 자리가 저장을 건넌다 ───────────────────────

        /// <summary>
        /// <b>세 번 연속으로 본다.</b> 한 번 통과한 것은 우연일 수 있고,
        /// 특히 이 갈래는 「저장할 때와 불러올 때의 열쇠가 같은가」에 걸려 있어
        /// 자리가 조금 흔들리는 것만으로도 산발적으로 깨진다.
        /// </summary>
        static IEnumerator 다_캔_자리가_저장을_건넌다()
        {
            E2EHarness.Log("— 다 캔 자리가 저장을 건넌다 (3회 연속) —");

            for (int 회 = 1; 회 <= 3; 회++)
            {
                var node = 노드("LooseScrap");
                E2EHarness.Assert(node != null, $"{회}회차: 아직 안 캔 흩어진 잔해가 있다");
                if (node == null) yield break;

                string 신원 = node.WorldId;
                var 자리 = node.transform.position;
                var 자세 = node.transform.rotation;

                node.Interact(E2EHarness.Player);
                yield return null;
                E2EHarness.Assert(node.IsDepleted, $"{회}회차: 잔해를 캤다");

                저장소.Save(슬롯);
                yield return null;

                // <b>물체를 죽이고 같은 자리에 새로 세운다.</b> 씬을 다시 읽는 것과
                // 같은 일이다 — 필드에 살던 상태는 그때 통째로 사라진다.
                //
                // 손에 든 원장도 함께 비운다. 안 그러면 새로 선 노드가 <b>태어나는
                // 순간</b> 아직 메모리에 남아 있던 줄을 받아 이미 캐여 있게 되고
                // (늦게 태어난 물건에게도 제 줄을 건네는 길이 있다 —
                // <c>WorldLedgerService.OnOwnerArrived</c>), 그러면 이 검사가
                // 파일을 거치지 않고도 통과한다. 진실이 <b>파일에만</b> 있어야
                // 불러오기를 재는 검사가 된다.
                WorldLedgerService.Instance?.Ledger.Clear();

                var 새노드 = 다시_세운다(node, 자리, 자세);
                yield return null;

                E2EHarness.Assert(새노드 != null, $"{회}회차: 같은 자리에 새 노드가 섰다");
                if (새노드 == null) yield break;
                E2EHarness.AssertEqual(새노드.WorldId, 신원, $"{회}회차: 신원이 같다");
                E2EHarness.Assert(!새노드.IsDepleted,
                    $"{회}회차: 새로 선 노드는 아무것도 모른 채 서 있다 (씬 로드가 하던 일)");

                bool 열렸나 = 저장소.Load(슬롯);
                yield return null;

                E2EHarness.Assert(열렸나, $"{회}회차: 저장본이 열렸다");
                E2EHarness.Assert(새노드.IsDepleted,
                    $"{회}회차: 불러온 세계에서 그 자리는 <b>다시 서 있지 않다</b>");

                var 겉 = 새노드.GetComponentsInChildren<Collider>(true);
                E2EHarness.Assert(겉.All(c => !c.enabled),
                    $"{회}회차: 다 캔 자리는 겨눠지지도 않는다 (상태만이 아니라 몸도 따라왔다)");
            }

            적는다("다 캔 자리", "저장 → 물체 죽고 새로 남 → 불러오기: 3회 연속 다시 서 있지 않았다");
        }

        // ── 2. 재생이 저장을 건너서도 돈다 ──────────────────────

        /// <summary>
        /// <b>담은 것이 「남은 시간」이 아니라 「다 캔 시각」이라는 것이 여기서 드러난다.</b>
        /// 남은 시간을 담았다면 불러온 순간부터 다시 세므로 주기가 늘 처음부터
        /// 시작하고, 저장을 자주 하는 사람에게는 세계가 영영 안 찬다.
        /// </summary>
        static IEnumerator 재생이_저장을_건너서도_돈다()
        {
            E2EHarness.Log("— 재생이 저장을 건너서도 돈다 —");

            var node = 노드("LooseScrap");
            E2EHarness.Assert(node != null, "아직 안 캔 흩어진 잔해가 있다");
            if (node == null) yield break;

            node.OverrideRespawnSeconds(검증용재생초);
            var 자리 = node.transform.position;
            var 자세 = node.transform.rotation;

            node.Interact(E2EHarness.Player);
            yield return null;
            E2EHarness.Assert(node.IsDepleted, "잔해를 캤다");

            float 캔시각 = WorldClock.Seconds;
            저장소.Save(슬롯);
            yield return null;

            // 진실이 파일에만 남게 손에 든 원장을 비운다 (위와 같은 이유)
            WorldLedgerService.Instance?.Ledger.Clear();

            var 새노드 = 다시_세운다(node, 자리, 자세);
            yield return null;
            if (새노드 == null) { E2EHarness.Assert(false, "새 노드를 세우지 못했다"); yield break; }
            E2EHarness.Assert(!새노드.IsDepleted, "새로 선 노드는 아무것도 모른 채 서 있다");

            E2EHarness.Assert(저장소.Load(슬롯), "저장본이 열렸다");
            yield return null;
            E2EHarness.Assert(새노드.IsDepleted, "불러온 자리는 비어 있다");

            // 주기를 줄이는 것은 불러온 <b>뒤에</b> 한다 — 저장본이 들고 있는 것은
            // 다 캔 시각뿐이고 주기는 에셋의 것이라, 이 순서가 곧 "시각만 실렸다"의 확인이다.
            새노드.OverrideRespawnSeconds(검증용재생초);

            // 눈앞에서는 안 돌아온다. 멀찍이 밀어 조건을 연다 — 신원은 깨어날 때
            // 잡혔으므로 지금 옮겨도 열쇠는 그대로다.
            var 눈 = E2EHarness.Eye.transform;
            새노드.transform.position = 눈.position + 눈.forward * (HarvestRespawnRule.UnseenDistance + 8f);
            E2EHarness.SyncPhysics();
            yield return null;

            yield return E2EHarness.WaitUntil(() => !새노드.IsDepleted,
                                              "저장을 건넌 자리에서도 주기가 돌아 다시 찼다",
                                              검증용재생초 + 10f);

            float 걸린시간 = WorldClock.Seconds - 캔시각;
            E2EHarness.Assert(!새노드.IsDepleted, "저장을 건넌 뒤에도 주기가 지나자 돌아왔다");
            E2EHarness.Log($"  다 캔 시각 {캔시각:F1}초 → 돌아온 시각 {WorldClock.Seconds:F1}초 " +
                           $"(주기 {검증용재생초:F0}초, 실제 {걸린시간:F1}초)");
            적는다("재생이 저장을 건넌다", $"주기 {검증용재생초:F0}초 · 실제 {걸린시간:F1}초 만에 돌아왔다");
        }

        // ── 3. 돌아온 자리는 원장에서 빠진다 ────────────────────

        /// <summary>
        /// <b>「다 캔 것」과 「이미 돌아온 것」을 가르는 자리.</b> 씬 로드로 사라지던
        /// 것이 이제 안 사라지므로, 이 구분이 없으면 한 번 캔 자리는 원장에
        /// <c>gone</c>으로 박혀 영영 비어 있게 된다.
        /// </summary>
        static IEnumerator 돌아온_자리는_원장에서_빠진다()
        {
            E2EHarness.Log("— 돌아온 자리는 원장에서 빠진다 —");

            var 원장 = WorldLedgerService.Instance;
            E2EHarness.Assert(원장 != null, "원장이 있다");
            if (원장 == null) yield break;

            var node = 노드("LooseScrap");
            E2EHarness.Assert(node != null, "아직 안 캔 흩어진 잔해가 있다");
            if (node == null) yield break;

            node.OverrideRespawnSeconds(검증용재생초);
            string 신원 = node.WorldId;

            node.Interact(E2EHarness.Player);
            yield return null;

            저장소.Save(슬롯);
            yield return null;
            E2EHarness.Assert(원장.Ledger.TryGet(신원, out _), "다 캔 자리가 원장에 적혔다");

            var 눈 = E2EHarness.Eye.transform;
            node.transform.position = 눈.position + 눈.forward * (HarvestRespawnRule.UnseenDistance + 8f);
            E2EHarness.SyncPhysics();

            yield return E2EHarness.WaitUntil(() => !node.IsDepleted, "자리가 다시 찼다",
                                              검증용재생초 + 10f);

            저장소.Save(슬롯);
            yield return null;
            E2EHarness.Assert(!원장.Ledger.TryGet(신원, out _),
                              "다시 찬 자리는 원장에서 빠졌다 — 그래서 다음에 불러오면 서 있다");

            E2EHarness.Assert(원장.Ledger.Conflicts.Count == 0,
                              $"세계 물건 중에 신원이 겹치는 것이 없다 " +
                              $"(격자 {Survive.World.WorldId.Grid}m)");

            적는다("원장 줄 수", $"{원장.Ledger.Count}줄 · 신원 충돌 {원장.Ledger.Conflicts.Count}건");
        }

        // ── 4. 옛 저장본 ────────────────────────────────────────

        /// <summary>
        /// <b>「세계」 절이 없던 저장본이 그냥 열린다.</b> 저장본은 열쇠-값 목록이고
        /// 불러올 때 모르는 열쇠는 건너뛰므로, 절을 하나 더한 것은 <b>덧붙임</b>이지
        /// 형식 변경이 아니다. 옛 파일이 열릴 때 세계는 <b>씬이 놓아둔 그대로</b>다.
        /// </summary>
        static IEnumerator 세계_절이_없는_저장본도_그냥_열린다()
        {
            E2EHarness.Log("— 세계 절이 없는 옛 저장본이 그냥 열린다 —");

            저장소.Save(옛슬롯);
            yield return null;

            string 경로 = E2EHarness.SlotPath(옛슬롯);
            E2EHarness.Assert(File.Exists(경로), "저장본 파일이 생겼다");

            E2EHarness.Assert(SaveSerializer.TryDeserialize(File.ReadAllText(경로),
                                                           out var 저장본, out var 사유),
                              $"저장본을 읽었다 ({사유})");
            if (저장본 == null) yield break;

            int 전 = 저장본.entries.Count;
            저장본.entries.RemoveAll(e => e.key == WorldLedgerService.Key);
            E2EHarness.Assert(저장본.entries.Count == 전 - 1,
                              $"「세계」 절을 도려냈다 ({전} → {저장본.entries.Count}칸)");

            File.WriteAllText(경로, SaveSerializer.Serialize(저장본));
            yield return null;

            var 원장 = WorldLedgerService.Instance;
            int 전에되돌린것 = 원장 != null ? 원장.LastRestored : 0;

            bool 열렸나 = 저장소.Load(옛슬롯);
            yield return null;

            E2EHarness.Assert(열렸나, "세계 절이 없는 저장본이 그냥 열렸다");
            E2EHarness.AssertEqual(원장 != null ? 원장.LastRestored : 0, 전에되돌린것,
                                   "그때 원장은 아무것도 되돌리지 않았다 (세계는 씬 그대로다)");

            적는다("옛 저장본", $"「세계」 절 없는 {저장본.entries.Count}칸짜리 파일이 그냥 열렸다");
        }

        // ── 잔 도구들 ───────────────────────────────────────────

        /// <summary>
        /// 물체를 죽이고 <b>같은 자리에</b> 같은 것을 새로 세운다. 씬을 다시 읽는
        /// 것과 같은 일이다.
        ///
        /// <b>꺼진 부모 밑에 복제한다.</b> 그냥 복제하면 그 자리에서 곧바로
        /// <c>Awake</c>가 돌아 <b>원본과 사본이 같은 신원으로 동시에 등록</b>되고,
        /// 그것은 원장이 충돌로 잡는 상태다. 부모가 꺼져 있으면 <c>Awake</c>가
        /// 미뤄지므로, 원본을 치우고 자리를 잡아 준 뒤에 켠다.
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
