using UnityEngine;
using Survive.Building;
using Survive.Core;
using Survive.Crafting;
using Survive.Interaction;
using Survive.Items;

namespace Survive.World
{
    /// <summary>
    /// <b>생성 목록과 세계 사이의 손.</b> 목록(<see cref="SpawnLedger"/>)은 Unity를
    /// 모르는 순수 물건이라, 살아 있는 것을 훑어 적고 적힌 것을 다시 세우는 일은
    /// 여기가 맡는다. <c>WorldLedgerService</c>가 저장·불러오기의 두 자리에서
    /// 이 문을 지난다.
    ///
    /// <b>다시 세울 때는 먼저 거둔다.</b> 남겨 두면 불러온 판에 같은 것이 두 벌
    /// 서고, 그것은 사람이 자기 거점을 두 채 보게 되는 것이다.
    /// <c>DeathDropService</c>가 이미 같은 판단을 해 두었다 —
    /// 목록의 주인이 하나면 거두는 쪽도 그 하나여야 한다.
    ///
    /// <b><c>BuildPlacer.TryBuild</c>를 다시 부르지 않는다.</b> 저쪽은 자원을 받고,
    /// 청사진 해금을 묻고, 겹침을 판정한다 — 되살리기에서 그 셋은 전부 틀린 일이다.
    /// 자원은 이미 냈고, 해금은 그때 있었고, 겹침 판정은 <b>먼저 되살아난 것</b>을
    /// 장애물로 보아 뒤엣것을 통째로 떨어뜨린다. 그래서 <c>Instantiate</c> 뒤에
    /// 저쪽이 하던 뒷정리만 그대로 따라 한다.
    /// </summary>
    public static class SpawnLedgerStage
    {
        // ── 적는다 ───────────────────────────────────────────────

        /// <summary>
        /// 지금 세계에 있는 <b>태어난 것</b>들을 목록에 옮겨 적는다.
        ///
        /// 훑기가 시작할 때 목록을 비우므로(<see cref="SpawnLedger.BeginSweep"/>),
        /// 철거된 건축물과 주워진 물건은 <b>아무 일도 하지 않아도</b> 빠진다.
        /// 원장 쪽의 「이번 훑기에 아무도 안 적은 줄은 버린다」와 겉모습은 같지만
        /// 뜻이 다르다 — 저쪽은 「돌아왔다」, 이쪽은 「없어졌다」다.
        /// </summary>
        public static void Sweep(SpawnLedger ledger)
        {
            if (ledger == null) return;

            ledger.BeginSweep();

            float 지금 = WorldClock.Seconds;

            var 건축물들 = BuiltStructure.Active;
            for (int i = 0; i < 건축물들.Count; i++)
            {
                var s = 건축물들[i];
                if (s == null || !s.Spawned || s.Definition == null) continue;

                var 줄 = new SpawnRecord
                {
                    kind = WorldLedgerScope.Structure,
                    what = s.Definition.id,
                    count = 1,
                    position = s.transform.position,
                    rotation = s.transform.rotation,
                };

                // 화톳불의 연료는 제 줄이 없다. 몸을 싣는 이 줄에 함께 실린다 —
                // 그것이 「몸이 원장 밖에 있으면 연료를 돌려줄 불이 없다」의 답이다.
                var 불 = s.GetComponent<Campfire>();
                if (불 != null)
                {
                    // 남은 초가 아니라 <b>다 타는 시각</b>을 적는다.
                    줄.until = SpawnLedgerRule.BurnsOutAt(지금, 불.FuelSeconds);
                    줄.since = 불.KindledAt;
                }

                딸림을_적는다(s, 줄);

                ledger.Put(줄);
            }

            var 낙하물들 = ItemPickup.Active;
            for (int i = 0; i < 낙하물들.Count; i++)
            {
                var d = 낙하물들[i];
                if (d == null || !d.Spawned || d.Item == null || d.Count <= 0) continue;

                ledger.Put(new SpawnRecord
                {
                    kind = WorldLedgerScope.Drop,
                    what = d.Item.id,
                    count = d.Count,
                    position = d.RestAt,
                    rotation = Quaternion.identity,
                });
            }

            ledger.EndSweep();
        }

        /// <summary>
        /// 몸에 붙어 있는 상태를 그 몸의 줄에 함께 적는다.
        ///
        /// <b>갈래를 세지 않는다.</b> 보관함이면 보관 격자, 스테이션이면 회수함과
        /// 대기열 — 물어보는 것은 「이 몸이 무엇을 들고 있나」 하나이고, 새로운
        /// 그릇이 붙는 날 여기 한 줄만 는다. 세 갈래를 목록의 세 갈래로 두지 않은
        /// 이유는 <see cref="SpawnRecord"/>에 적어 두었다.
        /// </summary>
        static void 딸림을_적는다(BuiltStructure s, SpawnRecord 줄)
        {
            var 보관함 = s.GetComponent<StorageContainer>();
            if (보관함 != null) 줄.holds = StationSaveRule.Capture(보관함.Contents);

            var 스테이션 = s.GetComponent<ICraftStation>();
            if (스테이션?.Work != null)
            {
                줄.output = StationSaveRule.Capture(스테이션.Work.Output);
                줄.queued = StationSaveRule.Capture(스테이션.Work.Queue);
            }
        }

        // ── 다시 세운다 ──────────────────────────────────────────

        /// <summary>
        /// 목록대로 세계를 다시 세운다.
        /// </summary>
        /// <returns>다시 세운 줄 수.</returns>
        public static int Rebuild(SpawnLedger ledger)
        {
            if (ledger == null) return 0;

            거둔다();

            var 목록 = 청사진목록();
            var 표 = 아이템표();
            float 지금 = WorldClock.Seconds;

            int 세운것 = 0;
            var 줄들 = ledger.Records;

            for (int i = 0; i < 줄들.Count; i++)
            {
                var r = 줄들[i];
                if (r == null) continue;

                if (r.kind == WorldLedgerScope.Structure)
                {
                    if (건축물을_세운다(r, 목록, 지금)) 세운것++;
                }
                else if (r.kind == WorldLedgerScope.Drop)
                {
                    if (낙하물을_놓는다(r, 표)) 세운것++;
                }
            }

            return 세운것;
        }

        /// <summary>
        /// 지금 세계에 있는 <b>태어난 것</b>을 전부 거둔다.
        ///
        /// <b><c>Destroy</c> 앞에서 한 번 끈다.</b> <c>Destroy</c>는 프레임 끝까지
        /// 미뤄지므로 <c>OnDisable</c>도 그때까지 안 돈다. 그러면 거둔 저장고가
        /// <b>아직 저장 대상 목록에 등록된 채</b> 남고, 바로 뒤에 되살아난 같은
        /// 자리의 저장고와 열쇠가 겹쳐 <c>SaveService</c>가 먼저 찾은 쪽 —
        /// 곧 곧 사라질 쪽 — 에 내용물을 되돌린다. 꺼 두면 그 자리에서 등록이 풀린다.
        /// </summary>
        static void 거둔다()
        {
            var 건축물들 = BuiltStructure.Active;
            var 거둘것 = new System.Collections.Generic.List<GameObject>();

            for (int i = 0; i < 건축물들.Count; i++)
            {
                var s = 건축물들[i];
                if (s != null && s.Spawned) 거둘것.Add(s.gameObject);
            }

            var 낙하물들 = ItemPickup.Active;
            for (int i = 0; i < 낙하물들.Count; i++)
            {
                var d = 낙하물들[i];
                if (d != null && d.Spawned) 거둘것.Add(d.gameObject);
            }

            for (int i = 0; i < 거둘것.Count; i++)
            {
                if (거둘것[i] == null) continue;
                거둘것[i].SetActive(false);
                Object.Destroy(거둘것[i]);
            }
        }

        static bool 건축물을_세운다(SpawnRecord r, BuildCatalogSO 목록, float 지금)
        {
            if (목록 == null) return false;

            var 정의 = 목록.GetById(r.what);
            if (정의 == null || 정의.prefab == null)
            {
                Debug.LogWarning($"[SpawnLedger] 저장본이 가리키는 청사진을 목록에서 " +
                                 $"찾지 못했다: {r.what}");
                return false;
            }

            var go = Object.Instantiate(정의.prefab, r.position, r.rotation);
            go.name = 정의.id;

            var 표시 = go.GetComponent<BuiltStructure>();
            if (표시 == null) 표시 = go.AddComponent<BuiltStructure>();
            표시.Setup(정의);

            if (go.GetComponent<StructureDemolisher>() == null)
                go.AddComponent<StructureDemolisher>();

            if (정의.IsModular)
            {
                var 조각 = go.GetComponent<ModularPiece>();
                if (조각 == null) 조각 = go.AddComponent<ModularPiece>();
                조각.Setup(정의.pieceKind);
            }

            var 불 = go.GetComponent<Campfire>();
            // 다 타는 시각에서 남은 초를 되짚는다. 저장해 둔 사이에 세계 시간이
            // 흘렀으면 그만큼 줄어 있고, 다 지났으면 꺼진 채로 선다.
            if (불 != null) 불.Adopt(SpawnLedgerRule.FuelLeft(r.until, 지금), r.since);

            딸림을_되돌린다(go, r);

            return true;
        }

        /// <summary>
        /// 줄이 실어 온 딸림을 방금 세운 몸에 앉힌다.
        ///
        /// <b>몸을 세우는 같은 걸음 안에서 앉힌다.</b> 이것이 딸림을 저장본의
        /// 다른 절에 두지 않은 값이다 — 절이 갈리면 「몸이 아직 없는데 내용물이
        /// 먼저 돌아오는」 순서가 생기고, 그것을 막으려면 어느 절이 먼저인지를
        /// 저장본이 약속해야 한다. 여기서는 순서가 있을 수 없다.
        /// </summary>
        static void 딸림을_되돌린다(GameObject go, SpawnRecord r)
        {
            var 보관함 = go.GetComponent<StorageContainer>();
            if (보관함 != null)
                StationSaveRule.AdoptInto(보관함.Contents, r.holds, 아이템찾기);

            var 스테이션 = go.GetComponent<ICraftStation>();
            if (스테이션?.Work != null)
            {
                StationSaveRule.AdoptInto(스테이션.Work.Output, r.output, 아이템찾기);
                StationSaveRule.AdoptInto(스테이션.Work.Queue, r.queued, RecipeIndex.Find);
            }
        }

        static ItemDataSO 아이템찾기(string id)
        {
            var 표 = 아이템표();
            return 표 != null ? 표.GetById(id) : null;
        }

        static bool 낙하물을_놓는다(SpawnRecord r, ItemDatabaseSO 표)
        {
            if (표 == null) return false;

            var 물건 = 표.GetById(r.what);
            if (물건 == null)
            {
                Debug.LogWarning($"[SpawnLedger] 저장본이 가리키는 아이템을 표에서 " +
                                 $"찾지 못했다: {r.what}");
                return false;
            }

            return ItemDropper.Place(물건, r.count, r.position) != null;
        }

        // ── 표를 찾는 손 ─────────────────────────────────────────

        static BuildCatalogSO 청사진목록() =>
            GameServices.TryGet<BuildPlacer>(out var placer) && placer != null
                ? placer.Catalog
                : null;

        static ItemDatabaseSO 아이템표() =>
            GameServices.TryGet<PlayerInventory>(out var inv) && inv != null
                ? inv.Database
                : null;
    }
}
