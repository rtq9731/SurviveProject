using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Interaction;
using Survive.Items;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 챕터 1 재건 스펙 §11 - 필수 장비는 소지품 칸을 두고 경쟁하지 않는다.
    ///
    /// EditMode(<c>EquipmentSlotsTests</c>)가 규칙을 지킨다. 규칙이 옳아도 실제
    /// 습득 경로가 그 규칙을 안 지나면 랜턴은 그대로 칸에 앉고, 그 구멍은
    /// 진짜로 주워 보기 전까지 아무 신호도 내지 않는다. 그래서 여기서는
    /// <b>바닥에 놓고 E로 줍는다.</b>
    ///
    /// 보는 것은 셋이다.
    /// 1. 주운 랜턴이 장비 자리로 가고 소지품 15칸은 그대로 15칸이다.
    /// 2. 15칸을 끝까지 채워도 랜턴이 밀려나지 않고, <b>켜져 있던 불이 그대로다.</b>
    /// 3. 저장 왕복(진짜 DTO를 JsonUtility로 굽고 되돌린다)을 해도 자리에 남는다.
    ///
    /// 디스크의 세이브 파일은 건드리지 않는다 - 지금 이 컴퓨터에서 여러 에디터가
    /// 같은 persistentDataPath를 쓴다. 저장 경로 검증은 <see cref="PlayerInventory"/>의
    /// CaptureState/RestoreState를 직접 굴려서 한다.
    /// </summary>
    public static class E2EEquipmentSlot
    {
        const string LanternId = "lantern";
        const string FillerId = "scrap";

        static PlayerInventory PI => E2EHarness.Player.Inventory;
        static Inventory Inv => PI.Inventory;
        static EquipmentSlots Equip => PI.Equipment;

        static LanternController _lantern;
        static GameObject _drop;

        public static IEnumerator FullRun()
        {
            yield return 판을_비운다();
            yield return 바닥에서_주운_랜턴이_장비_자리로_간다();
            yield return 불은_저절로_들어온다();
            yield return 소지품을_끝까지_채운다();
            yield return 저장_왕복();
            yield return 되돌린다();

            E2EHarness.Log("=== 장비 슬롯 완주 ===");
        }

        // ── 1. 판을 비운다 ───────────────────────────────────────

        static IEnumerator 판을_비운다()
        {
            E2EHarness.Assert(PI != null, "PlayerInventory가 있다");
            E2EHarness.Assert(PI.Database != null, "아이템 데이터베이스가 연결돼 있다");
            E2EHarness.AssertEqual(Inv.SlotCount, 15, "소지품 칸 수");

            _lantern = Object.FindAnyObjectByType<LanternController>(FindObjectsInactive.Include);
            E2EHarness.Assert(_lantern != null, "랜턴 장치가 씬에 있다");

            // 앞선 시나리오가 남긴 것이 있으면 "가득 찼다"를 셀 수 없다.
            for (int 회 = 0; 회 < 8; 회++)
            {
                var ids = Inv.Slots.Where(s => !s.IsEmpty).Select(s => s.item.id).Distinct().ToArray();
                if (ids.Length == 0) break;
                foreach (var id in ids) Inv.TryRemove(id, Inv.CountInSlots(id));
            }
            Equip.Clear();
            yield return null;

            // 랜턴을 가진 것이 없으니 불도 없다. 끄는 조작이 없는데도 꺼져 있는
            // 이유는 그것뿐이어야 한다(스펙 §12).
            E2EHarness.Assert(!_lantern.IsOn, "랜턴이 없으니 불도 없다");
            E2EHarness.AssertEqual(_lantern.Tier, 0, "랜턴 티어");

            E2EHarness.AssertEqual(Inv.Slots.Count(s => s.IsEmpty), 15, "출발선의 빈 칸 수");
            E2EHarness.Assert(Equip.IsEmpty(EquipmentSlotKind.Light), "장비 자리가 비어 있다");
        }

        // ── 2. 진짜로 줍는다 ─────────────────────────────────────

        static IEnumerator 바닥에서_주운_랜턴이_장비_자리로_간다()
        {
            var 랜턴정의 = PI.Database.GetById(LanternId);
            E2EHarness.Assert(랜턴정의 != null, "랜턴 아이템 정의를 찾았다");
            E2EHarness.AssertEqual(랜턴정의.equipSlot, EquipmentSlotKind.Light,
                                   "랜턴은 데이터에서 조명 장비로 지정돼 있다");

            var cam = E2EHarness.Eye;
            _drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _drop.name = "E2E_LanternDrop";
            _drop.transform.localScale = Vector3.one * 0.4f;
            _drop.transform.position = cam.transform.position + cam.transform.forward * 1.6f;
            _drop.AddComponent<ItemPickup>().Setup(랜턴정의, 1);
            E2EHarness.SyncPhysics();

            yield return null;
            E2EHarness.LookAt(_drop.transform.position);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current != null, "떨어진 랜턴이 탐지된다", 4f);
            yield return E2EHarness.TapKey(Key.E);
            yield return null;
            yield return E2EHarness.WaitUntil(() => Inv.CountOf(LanternId) > 0, "랜턴이 손에 들어왔다", 4f);

            E2EHarness.Assert(!Equip.IsEmpty(EquipmentSlotKind.Light),
                              "주운 랜턴이 장비 자리로 갔다 (칸이 아니라)");
            E2EHarness.AssertEqual(Inv.CountInSlots(LanternId), 0, "소지품 칸에 든 랜턴 수");
            E2EHarness.AssertEqual(Inv.Slots.Count(s => s.IsEmpty), 15,
                                   "랜턴을 걸어도 빈 칸은 15개다");
            E2EHarness.AssertEqual(Inv.SlotCount, 15, "칸 수는 줄지 않는다");
        }

        // ── 3. 불은 저절로 들어온다 ──────────────────────────────

        /// <summary>
        /// <b>아무것도 누르지 않는다.</b> 스펙 §12로 랜턴에는 스위치가 없어졌고,
        /// 자리에 걸린 것을 점등 판정이 못 보면 여기서 불이 안 들어온다.
        /// </summary>
        static IEnumerator 불은_저절로_들어온다()
        {
            _lantern.Recharge(LanternRule.MaxBattery);
            E2EHarness.Assert(_lantern.BatteryNormalized > 0.5f,
                              $"배터리가 차 있다 ({_lantern.BatteryNormalized:P0})");

            yield return E2EHarness.WaitUntil(() => _lantern.IsOn,
                                              "누른 것 없이 불이 들어왔다", 3f);
            E2EHarness.AssertEqual(_lantern.Tier, 1, "걸린 랜턴의 티어");
            E2EHarness.AssertEqual(_lantern.LitZoneRadius, LanternRule.RadiusForTier(1),
                                   "밝은 구역 반경(m)");
        }

        // ── 4. 소지품을 끝까지 채운다 ────────────────────────────

        static IEnumerator 소지품을_끝까지_채운다()
        {
            var 채울것 = PI.Database.GetById(FillerId);
            E2EHarness.Assert(채울것 != null, $"{FillerId} 아이템 정의를 찾았다");

            // 스택 상한 x 칸 수보다 넉넉히 넣는다. 남는 것은 들어갈 데가 없어야 한다.
            int 남은수 = Inv.TryAdd(채울것, 채울것.maxStack * Inv.SlotCount + 25);
            yield return null;

            E2EHarness.AssertEqual(Inv.Slots.Count(s => s.IsEmpty), 0, "빈 칸이 하나도 없다");
            E2EHarness.Assert(남은수 > 0, $"더 넣을 자리가 없어 {남은수}개가 남았다");

            E2EHarness.Assert(!Equip.IsEmpty(EquipmentSlotKind.Light),
                              "소지품이 가득 차도 랜턴은 자리에 남아 있다");
            E2EHarness.AssertEqual(Inv.CountOf(LanternId), 1, "랜턴 소지 수");
            E2EHarness.Assert(_lantern.IsOn, "불은 여전히 켜져 있다");

            // 다음 프레임에도 살아 있는지 본다. 한 프레임짜리 참은 참이 아니다.
            for (int i = 0; i < 30; i++) yield return null;
            E2EHarness.Assert(!Equip.IsEmpty(EquipmentSlotKind.Light), "30프레임 뒤에도 걸려 있다");
            E2EHarness.Assert(_lantern.IsOn, "30프레임 뒤에도 켜져 있다");
        }

        // ── 5. 저장 왕복 ─────────────────────────────────────────

        static IEnumerator 저장_왕복()
        {
            var 뜬것 = PI.CaptureState();
            string json = JsonUtility.ToJson(뜬것);
            E2EHarness.Assert(json.Contains(LanternId), "저장 덩어리에 랜턴이 실렸다");

            var 되돌린것 = JsonUtility.FromJson<PlayerInventory.SaveState>(json);
            E2EHarness.Assert(되돌린것.equippedIds != null && 되돌린것.equippedIds.Contains(LanternId),
                              "장비 칸이 저장 덩어리에 그대로 있다");

            PI.RestoreState(되돌린것);
            yield return null;

            E2EHarness.Assert(!Equip.IsEmpty(EquipmentSlotKind.Light), "불러와도 랜턴이 걸려 있다");
            E2EHarness.AssertEqual(Inv.CountInSlots(LanternId), 0, "불러온 뒤 칸에 든 랜턴 수");
            E2EHarness.AssertEqual(Inv.SlotCount, 15, "불러온 뒤 칸 수");
            E2EHarness.Assert(Inv.CountOf(FillerId) > 0, "채워 둔 것도 같이 돌아왔다");
        }

        // ── 6. 되돌린다 ──────────────────────────────────────────

        static IEnumerator 되돌린다()
        {
            for (int 회 = 0; 회 < 8; 회++)
            {
                var ids = Inv.Slots.Where(s => !s.IsEmpty).Select(s => s.item.id).Distinct().ToArray();
                if (ids.Length == 0) break;
                foreach (var id in ids) Inv.TryRemove(id, Inv.CountInSlots(id));
            }

            if (_drop != null) Object.Destroy(_drop);
            _drop = null;
            yield return null;
        }
    }
}
