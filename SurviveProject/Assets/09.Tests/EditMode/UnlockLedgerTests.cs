using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.Progression;

/// <summary>
/// 해금 원장 — 무엇을 알고 있는가의 단일 상태.
///
/// 여기서 지키는 것: 중복 해금이 두 번 세지 않는 것, 모르는 열쇠는 잠겨 있는 것,
/// 요구가 비면 언제나 열려 있는 것(기존 데이터 하위호환의 근거), 그리고
/// 저장을 왕복해도 알던 것이 그대로 남는 것.
/// </summary>
public class UnlockLedgerTests
{
    UnlockLedger _ledger;

    [SetUp]
    public void SetUp() => _ledger = new UnlockLedger();

    // ── 해금 ────────────────────────────────────────────────

    [Test]
    public void 열지_않은_열쇠는_잠겨_있다()
    {
        Assert.IsFalse(_ledger.IsUnlocked("bp_alloy_shaping"));
    }

    [Test]
    public void 열면_열린다()
    {
        Assert.IsTrue(_ledger.Unlock("bp_alloy_shaping"));
        Assert.IsTrue(_ledger.IsUnlocked("bp_alloy_shaping"));
        Assert.AreEqual(1, _ledger.Count);
    }

    [Test]
    public void 같은_열쇠를_두_번_열어도_한_번만_센다()
    {
        Assert.IsTrue(_ledger.Unlock("bp_scrapworks"));
        Assert.IsFalse(_ledger.Unlock("bp_scrapworks"), "두 번째는 새로 열린 것이 아니다");

        Assert.AreEqual(1, _ledger.Count);
        Assert.IsTrue(_ledger.IsUnlocked("bp_scrapworks"));
    }

    [Test]
    public void 새로_열린_것만_알린다()
    {
        var heard = new List<string>();
        _ledger.KeyUnlocked += heard.Add;

        _ledger.Unlock("a");
        _ledger.Unlock("a");
        _ledger.Unlock("b");

        CollectionAssert.AreEqual(new[] { "a", "b" }, heard);
    }

    [Test]
    public void 빈_열쇠는_요구_없음이라_항상_열려_있다()
    {
        Assert.IsTrue(_ledger.IsUnlocked(null));
        Assert.IsTrue(_ledger.IsUnlocked(""));
        Assert.IsTrue(_ledger.IsUnlocked("   "));
    }

    [Test]
    public void 빈_열쇠는_원장에_들어가지_않는다()
    {
        Assert.IsFalse(_ledger.Unlock(null));
        Assert.IsFalse(_ledger.Unlock(""));
        Assert.IsFalse(_ledger.Unlock("  "));
        Assert.AreEqual(0, _ledger.Count);
    }

    [Test]
    public void 모르는_열쇠를_물어도_터지지_않는다()
    {
        _ledger.Unlock("bp_scrapworks");
        Assert.IsFalse(_ledger.IsUnlocked("bp_없는것"));
    }

    [Test]
    public void 비우면_전부_잠긴다()
    {
        _ledger.Unlock("a");
        _ledger.Unlock("b");
        _ledger.Clear();

        Assert.AreEqual(0, _ledger.Count);
        Assert.IsFalse(_ledger.IsUnlocked("a"));
    }

    // ── 저장 왕복 ────────────────────────────────────────────

    [Test]
    public void 왕복하면_알던_것이_그대로다()
    {
        _ledger.Unlock("bp_scrapworks");
        _ledger.Unlock("bp_salvaged_mechanism");
        _ledger.Unlock("disc_scrap");

        var json = JsonUtility.ToJson(_ledger.Capture());

        var 다른판 = new UnlockLedger();
        다른판.Restore(JsonUtility.FromJson<UnlockLedgerState>(json));

        Assert.AreEqual(3, 다른판.Count);
        Assert.IsTrue(다른판.IsUnlocked("bp_scrapworks"));
        Assert.IsTrue(다른판.IsUnlocked("bp_salvaged_mechanism"));
        Assert.IsTrue(다른판.IsUnlocked("disc_scrap"));
        Assert.IsFalse(다른판.IsUnlocked("bp_alloy_shaping"));
    }

    [Test]
    public void 복원은_알리지_않는다()
    {
        _ledger.Unlock("bp_scrapworks");
        var state = _ledger.Capture();

        var 다른판 = new UnlockLedger();
        int 울린횟수 = 0;
        다른판.KeyUnlocked += _ => 울린횟수++;
        다른판.Restore(state);

        Assert.AreEqual(0, 울린횟수, "불러오기는 발견이 아니다 — AI가 다시 떠들면 안 된다");
        Assert.IsTrue(다른판.IsUnlocked("bp_scrapworks"));
    }

    [Test]
    public void 복원은_먼저_있던_것을_밀어낸다()
    {
        _ledger.Unlock("옛것");
        _ledger.Restore(new UnlockLedgerState { keys = new List<string> { "새것" } });

        Assert.IsFalse(_ledger.IsUnlocked("옛것"));
        Assert.IsTrue(_ledger.IsUnlocked("새것"));
        Assert.AreEqual(1, _ledger.Count);
    }

    [Test]
    public void 망가진_저장본은_건질_수_있는_것만_건진다()
    {
        _ledger.Restore(new UnlockLedgerState
        {
            keys = new List<string> { "a", null, "", "a", "b" }
        });

        Assert.AreEqual(2, _ledger.Count);
        Assert.IsTrue(_ledger.IsUnlocked("a"));
        Assert.IsTrue(_ledger.IsUnlocked("b"));
    }

    [Test]
    public void 빈_상태를_복원해도_터지지_않는다()
    {
        _ledger.Unlock("a");
        _ledger.Restore(null);
        Assert.AreEqual(0, _ledger.Count);
    }

    // ── 청사진 판정 ──────────────────────────────────────────

    [Test]
    public void 요구_청사진이_비면_언제나_만들_수_있다()
    {
        Assert.IsTrue(BlueprintGate.IsUnlocked(null, _ledger),
            "청사진을 얹기 전에 만든 데이터가 그대로 동작해야 한다");
    }

    [Test]
    public void id가_빈_청사진도_요구가_없는_것으로_본다()
    {
        var bp = ScriptableObject.CreateInstance<BlueprintSO>();
        Assert.IsTrue(BlueprintGate.IsUnlocked(bp, _ledger));
    }

    [Test]
    public void 원장이_없으면_막지_않는다()
    {
        var bp = 청사진("bp_scrapworks");
        Assert.IsTrue(BlueprintGate.IsUnlocked(bp, null),
            "원장이 서기 전에 잠그면 판이 통째로 얼어붙는다 — 실패는 개방 쪽으로");
    }

    [Test]
    public void 요구_청사진이_있으면_열려야_만들_수_있다()
    {
        var bp = 청사진("bp_scrapworks");

        Assert.IsFalse(BlueprintGate.IsUnlocked(bp, _ledger));
        _ledger.Unlock("bp_scrapworks");
        Assert.IsTrue(BlueprintGate.IsUnlocked(bp, _ledger));
    }

    [Test]
    public void 잠금_문구를_만드는_함수는_더_이상_없다()
    {
        // LockText는 청사진 이름과 hint를 화면으로 옮기는 유일한 통로였다.
        // 모르는 것은 목록에 실리지 않기로 했으므로 그 통로를 막았다.
        // 함수가 살아 있으면 언젠가 누군가 다시 부른다.
        Assert.IsNull(typeof(BlueprintGate).GetMethod("LockText"),
            "BlueprintGate.LockText가 되살아났다 — 잠긴 항목의 힌트가 화면으로 새는 길이다");
    }

    static BlueprintSO 청사진(string id, string name = null, string hint = null)
    {
        var bp = ScriptableObject.CreateInstance<BlueprintSO>();
        bp.id = id;
        bp.displayName = name;
        bp.hint = hint;
        return bp;
    }
}
