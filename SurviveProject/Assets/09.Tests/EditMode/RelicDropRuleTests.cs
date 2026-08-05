using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.Items;
using Survive.Progression;

/// <summary>
/// 낫이 무엇을 흘릴지 정하는 규칙 (백로그 39, 스펙 §2 "드롭 안전장치").
///
/// 여기서 지키는 것 셋.
/// <list type="number">
/// <item><b>미보유가 먼저다.</b> 운이 나빠 같은 것만 다섯 번 나오는 일이 없어야
///       진행이 확률에 막히지 않는다 — 그것이 스펙이 pity를 안전장치라 부른 이유다</item>
/// <item><b>"보유"는 손에 든 것만이 아니다.</b> 이미 다 밝혀낸 유물은 쓸 데가 없으므로
///       가진 것과 똑같이 친다 (인벤토리 + 원장)</item>
/// <item><b>전부 가졌으면 멈춘다.</b> 다 밝혀낸 뒤로도 계속 떨구면 바닥에 쓰레기만 쌓인다</item>
/// </list>
/// </summary>
public class RelicDropRuleTests
{
    const string 막 = "relic_membrane";
    const string 핵 = "relic_core";
    const string 보행설계 = "bp_surface_walker";
    const string 잠항설계 = "bp_submersible";

    List<RelicOption> _후보;
    Inventory _소지품;
    UnlockLedger _원장;

    static ItemDataSO 아이템(string id)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = 10;
        return it;
    }

    [SetUp]
    public void SetUp()
    {
        _후보 = new List<RelicOption>
        {
            new RelicOption(막, 보행설계),
            new RelicOption(핵, 잠항설계),
        };
        _소지품 = new Inventory(12);
        _원장 = new UnlockLedger();
    }

    int 뽑는다(float roll) => RelicDropRule.Pick(_후보, _소지품, _원장, roll);

    string 뽑은것(float roll)
    {
        int i = 뽑는다(roll);
        return i < 0 ? null : _후보[i].ItemId;
    }

    // ── 아무것도 없을 때 ────────────────────────────────────

    [Test]
    public void 아무것도_없으면_난수가_고른다()
    {
        Assert.AreEqual(막, 뽑은것(0f));
        Assert.AreEqual(막, 뽑은것(0.49f));
        Assert.AreEqual(핵, 뽑은것(0.5f));
        Assert.AreEqual(핵, 뽑은것(0.99f));
    }

    [Test]
    public void 난수가_경계를_벗어나도_후보_밖으로_나가지_않는다()
    {
        // 부르는 쪽이 Random.value를 그대로 넘긴다. 1.0이 섞여 들어와도 터지면 안 된다.
        Assert.AreEqual(핵, 뽑은것(1f));
        Assert.AreEqual(막, 뽑은것(-3f));
        Assert.AreEqual(핵, 뽑은것(42f));
    }

    // ── pity: 미보유 우선 ───────────────────────────────────

    [Test]
    public void 하나를_이미_가졌으면_난수와_무관하게_없는_것이_나온다()
    {
        _소지품.TryAdd(아이템(막), 1);

        for (float roll = 0f; roll < 1f; roll += 0.1f)
            Assert.AreEqual(핵, 뽑은것(roll), $"roll={roll}에서 이미 가진 것이 또 나왔다");
    }

    [Test]
    public void 반대쪽을_가졌으면_반대로_나온다()
    {
        _소지품.TryAdd(아이템(핵), 1);

        for (float roll = 0f; roll < 1f; roll += 0.1f)
            Assert.AreEqual(막, 뽑은것(roll), $"roll={roll}에서 이미 가진 것이 또 나왔다");
    }

    [Test]
    public void 여러_개_가지고_있어도_보유는_보유다()
    {
        _소지품.TryAdd(아이템(막), 5);
        Assert.AreEqual(핵, 뽑은것(0f));
    }

    // ── "보유"에는 밝혀낸 것도 든다 ─────────────────────────

    [Test]
    public void 이미_밝혀낸_유물은_손에_없어도_가진_것으로_친다()
    {
        // 연구를 마치면 유물은 사라지고 원장에 한 줄이 남는다. 그때 인벤토리만 보면
        // "없으니 또 주자"가 되어 영원히 같은 것만 떨어진다.
        _원장.Unlock(보행설계);

        Assert.AreEqual(0, _소지품.CountOf(막), "손에는 없다");
        for (float roll = 0f; roll < 1f; roll += 0.1f)
            Assert.AreEqual(핵, 뽑은것(roll), $"roll={roll}에서 이미 밝혀낸 것이 또 나왔다");
    }

    [Test]
    public void 손에_있는_것과_밝혀낸_것을_섞어도_남은_하나를_찾는다()
    {
        _원장.Unlock(잠항설계);
        Assert.AreEqual(막, 뽑은것(0.7f));
    }

    // ── 전부 가졌으면 멈춘다 ────────────────────────────────

    [Test]
    public void 둘_다_손에_있으면_아무것도_떨구지_않는다()
    {
        _소지품.TryAdd(아이템(막), 1);
        _소지품.TryAdd(아이템(핵), 1);

        Assert.AreEqual(RelicDropRule.Nothing, 뽑는다(0.3f));
        Assert.IsFalse(RelicDropRule.AnythingLeft(_후보, _소지품, _원장));
    }

    [Test]
    public void 둘_다_밝혀냈으면_아무것도_떨구지_않는다()
    {
        _원장.Unlock(보행설계);
        _원장.Unlock(잠항설계);

        Assert.AreEqual(RelicDropRule.Nothing, 뽑는다(0.3f),
            "다 밝혀낸 뒤로도 계속 떨구면 바닥에 쓰레기만 쌓인다");
        Assert.IsFalse(RelicDropRule.AnythingLeft(_후보, _소지품, _원장));
    }

    [Test]
    public void 하나는_손에_하나는_원장에_있어도_멈춘다()
    {
        _소지품.TryAdd(아이템(막), 1);
        _원장.Unlock(잠항설계);

        Assert.AreEqual(RelicDropRule.Nothing, 뽑는다(0.9f));
    }

    [Test]
    public void 아직_남았으면_남았다고_말한다()
    {
        Assert.IsTrue(RelicDropRule.AnythingLeft(_후보, _소지품, _원장));

        _소지품.TryAdd(아이템(막), 1);
        Assert.IsTrue(RelicDropRule.AnythingLeft(_후보, _소지품, _원장), "핵이 남았다");
    }

    // ── 진행이 막히지 않는다 ────────────────────────────────

    [Test]
    public void 연구를_끝낼_때마다_다음_유물로_넘어간다()
    {
        // 실제 진행 순서를 그대로 돌려 본다. 주웠다가 연구하고, 다시 주웠다가 연구하면
        // 두 설계가 다 열리고 그 뒤로는 아무것도 나오지 않아야 한다.
        var 첫번째 = 뽑은것(0f);
        Assert.AreEqual(막, 첫번째);

        _소지품.TryAdd(아이템(막), 1);
        Assert.AreEqual(핵, 뽑은것(0f), "손에 쥐자 다음 것으로 넘어간다");

        _소지품.TryRemove(막, 1);           // 연구대에 넣었다
        _원장.Unlock(보행설계);              // 다 봤다
        Assert.AreEqual(핵, 뽑은것(0f), "연구가 끝나도 되돌아가지 않는다");

        _소지품.TryAdd(아이템(핵), 1);
        _소지품.TryRemove(핵, 1);
        _원장.Unlock(잠항설계);

        Assert.AreEqual(RelicDropRule.Nothing, 뽑는다(0f), "둘 다 밝혀냈으니 그만이다");
    }

    // ── 경계 ────────────────────────────────────────────────

    [Test]
    public void 후보가_없으면_떨굴_것도_없다()
    {
        Assert.AreEqual(RelicDropRule.Nothing,
            RelicDropRule.Pick(new List<RelicOption>(), _소지품, _원장, 0.5f));
        Assert.AreEqual(RelicDropRule.Nothing, RelicDropRule.Pick(null, _소지품, _원장, 0.5f));
    }

    [Test]
    public void 아이템_id가_빈_후보는_없는_것으로_친다()
    {
        var 망가진것 = new List<RelicOption> { new RelicOption("", 보행설계), new RelicOption(핵, 잠항설계) };
        Assert.AreEqual(1, RelicDropRule.Pick(망가진것, _소지품, _원장, 0f),
            "id가 없으면 떨굴 수가 없다 — 건너뛴다");
    }

    [Test]
    public void 원장이_없어도_인벤토리만으로_판단한다()
    {
        // 순수 테스트 문맥이나 원장이 아직 서지 않은 순간이 있다. 그때 멈추면
        // 유물이 영영 안 나오고, 다 열린 것으로 치면 스팸이 된다 — 손에 든 것만 본다.
        Assert.AreEqual(막, _후보[RelicDropRule.Pick(_후보, _소지품, null, 0f)].ItemId);

        _소지품.TryAdd(아이템(막), 1);
        Assert.AreEqual(핵, _후보[RelicDropRule.Pick(_후보, _소지품, null, 0f)].ItemId);
    }

    [Test]
    public void 소지품이_없으면_원장만으로_판단한다()
    {
        _원장.Unlock(보행설계);
        Assert.AreEqual(핵, _후보[RelicDropRule.Pick(_후보, null, _원장, 0f)].ItemId);
    }
}
