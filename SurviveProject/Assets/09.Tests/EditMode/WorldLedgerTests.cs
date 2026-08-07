using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Survive.World;

/// <summary>
/// <b>세계의 원장</b> — 다섯 갈래 중 하나뿐 비어 있던 자리.
///
/// 여기서 못 박는 것이 셋이다.
/// <list type="number">
/// <item><b>무엇을 담고 무엇을 안 담는가</b>, 그리고 <b>안 담는 것마다 왜인지.</b>
///   이유 없이 빠진 것과 일부러 뺀 것을 글로 갈라 두지 않으면, 다음 사람은
///   빠진 것을 전부 「아직 안 한 일」로 읽고 되는대로 채워 넣는다.</item>
/// <item><b>여럿일 때 열쇠가 안 겹친다.</b> 세계 물건은 하나가 아니라 여럿이라,
///   <c>PlayerInventory</c>·<c>chapter_director</c>처럼 고정 문자열을 쓰면
///   나중 것이 앞의 것을 덮는다.</item>
/// <item><b>「다 캔 것」과 「돌아온 것」이 갈린다.</b> 씬 로드로 사라지던 것이
///   이제 안 사라지므로, 이 구분이 없으면 한 번 캔 자리가 영영 비어 있게 된다.</item>
/// </list>
/// </summary>
public class WorldLedgerTests
{
    // ══ ① 담는 것과 안 담는 것 ══════════════════════════════════

    [Test]
    public void 원장이_담는_것은_셋이다()
    {
        Assert.IsTrue(WorldLedgerScope.Carries(WorldLedgerScope.Harvest), "채집물");
        Assert.IsTrue(WorldLedgerScope.Carries(WorldLedgerScope.GlowCap), "군락의 갓");
        Assert.IsTrue(WorldLedgerScope.Carries(WorldLedgerScope.Plant), "식물");

        Assert.AreEqual(3, WorldLedgerScope.Carried.Count,
            "담는 갈래가 늘었으면 왜 늘었는지가 이 테스트에도 적혀야 한다");
    }

    /// <summary>
    /// <b>안 담기로 한 것마다 이유.</b> 이 표가 이 파일의 알맹이다 —
    /// 이유 없이 빠진 것과 일부러 뺀 것을 갈라 두지 않으면, 다음 사람은 빠진
    /// 것을 전부 「아직 안 한 일」로 읽고 되는대로 채워 넣는다. 그러면 원장이
    /// 씬의 사본이 되고, 씬을 고칠 때마다 저장본이 옛 세계를 되살린다.
    ///
    /// <b>왜 표가 코드가 아니라 테스트에 있는가.</b> 둘이다. 하나는
    /// <c>Domain/World/</c>가 「화면에 나가는 코드」로 분류되어 한글 문장을
    /// 상수로 들 수 없다는 것(<see cref="LocSentenceGateTests"/>)이고, 다른
    /// 하나가 더 중요하다 — <b>이유를 테스트가 요구해야</b> 갈래를 하나 더
    /// 뺄 때 이유 없이 뺄 수 없다.
    /// </summary>
    static readonly (string 갈래, string 왜_안_담는가)[] 안_담는_이유 =
    {
        (WorldLedgerScope.Structure,
            "실행 중에 태어난 것이라 「달라진 것」이 아니라 「없던 것」이다. " +
            "되살리려면 무엇으로 다시 세우는지(청사진 신원·자세·스냅 관계)가 필요하고, " +
            "그것은 원장이 아니라 생성 목록이라는 다른 물건이다. 자리만 열어 둔다."),

        (WorldLedgerScope.Drop,
            "같은 이유로 실행 중에 태어난 것이고, 게다가 낙하물은 물리로 굴러다녀 " +
            "자리가 신원이 되지 못한다. 자리가 신원이 아니면 열쇠를 지을 수 없다 — " +
            "이쪽은 「어디에 무엇이 몇 개」를 통째로 적는 생성 목록이 맞다."),

        (WorldLedgerScope.CampfireFuel,
            "화톳불은 사람이 세우는 물건이라 씬에 하나도 놓여 있지 않다. " +
            "몸이 원장 밖에 있는데 연료만 적으면 불러온 세계에는 그 연료를 " +
            "돌려줄 불이 없다. 건축물 갈래가 열리는 날 함께 열린다."),

        (WorldLedgerScope.Population,
            "「구역 도달 → 낫 2마리」의 방아쇠 구역이 아직 정해지지 않았다(사람의 몫). " +
            "개체수는 분명히 세계 상태이고 코드가 0줄이라 지금 세우는 값이 싸지만, " +
            "방아쇠를 모른 채 세우면 무엇을 세어야 하는지 모르는 칸이 된다. 자리만 열어 둔다."),
    };

    [Test]
    public void 안_담기로_한_것에는_전부_이유가_적혀_있다()
    {
        var 이유없는것 = WorldLedgerScope.Excluded
            .Where(k => !안_담는_이유.Any(r => r.갈래 == k && r.왜_안_담는가.Length >= 40))
            .ToList();

        Assert.IsEmpty(이유없는것,
            "안 담기로 한 갈래에 이유가 없다. 빠뜨린 것과 일부러 뺀 것은 " +
            "글로 갈라 두지 않으면 구별되지 않는다:\n  " + string.Join("\n  ", 이유없는것));

        var 없는갈래를_설명한것 = 안_담는_이유
            .Where(r => !WorldLedgerScope.Excluded.Contains(r.갈래))
            .Select(r => r.갈래)
            .ToList();

        Assert.IsEmpty(없는갈래를_설명한것,
            "이제 담기 시작한 갈래의 변명이 남아 있다. 표는 줄기만 해야 한다:\n  " +
            string.Join("\n  ", 없는갈래를_설명한것));
    }

    [Test]
    public void 실행_중에_태어난_것은_일부러_뺐다()
    {
        foreach (var kind in new[] { WorldLedgerScope.Structure, WorldLedgerScope.Drop,
                                     WorldLedgerScope.CampfireFuel })
        {
            Assert.IsFalse(WorldLedgerScope.Carries(kind), $"{kind}는 아직 담지 않는다");
            Assert.IsTrue(WorldLedgerScope.ExcludedOnPurpose(kind),
                $"{kind}는 일부러 뺀 것이어야 한다");
        }
    }

    /// <summary>
    /// 개체수는 <b>분명히</b> 세계 상태다. 그런데도 지금 안 세우는 이유는
    /// 「구역 도달 → 낫 2마리」의 방아쇠 구역이 아직 정해지지 않아서다.
    /// 자리만 열어 두었다는 것을 여기서 못 박는다 — 자리조차 없으면
    /// 그날 다시 이 라운드를 한다.
    /// </summary>
    [Test]
    public void 개체수는_자리만_열려_있다()
    {
        Assert.IsFalse(WorldLedgerScope.Carries(WorldLedgerScope.Population));
        Assert.IsTrue(WorldLedgerScope.ExcludedOnPurpose(WorldLedgerScope.Population));

        var 이유 = 안_담는_이유.First(r => r.갈래 == WorldLedgerScope.Population).왜_안_담는가;
        StringAssert.Contains("방아쇠", 이유,
            "개체수를 미루는 이유는 「방아쇠 구역이 미정」이지 「어렵다」가 아니다");
    }

    /// <summary>
    /// 모르는 갈래를 「일부러 뺀 것」이라고 말하면 실수가 설계로 둔갑한다
    /// (<c>HarvestRespawnRule.NeverOnPurpose</c>와 같은 결).
    /// </summary>
    [Test]
    public void 모르는_갈래는_담지도_않고_일부러_뺀_것도_아니다()
    {
        Assert.IsFalse(WorldLedgerScope.Carries("없는갈래"));
        Assert.IsFalse(WorldLedgerScope.ExcludedOnPurpose("없는갈래"));

        Assert.IsFalse(WorldLedgerScope.Carries(null));
        Assert.IsFalse(WorldLedgerScope.ExcludedOnPurpose(null));
    }

    [Test]
    public void 담지_않는_갈래는_적으려_해도_안_적힌다()
    {
        var ledger = new WorldLedger();
        bool 적혔나 = ledger.Put(new WorldRecord
        {
            id = "structure@1_0_1",
            kind = WorldLedgerScope.Structure,
            gone = false,
        });

        Assert.IsFalse(적혔나, "안 담기로 한 갈래는 문 앞에서 막힌다");
        Assert.AreEqual(0, ledger.Count);
    }

    // ══ ② 열쇠가 안 겹친다 ═════════════════════════════════════

    [Test]
    public void 같은_종류가_둘이면_자리가_열쇠를_가른다()
    {
        var a = WorldId.At(WorldLedgerScope.Harvest, new Vector3(10f, 0f, 4f));
        var b = WorldId.At(WorldLedgerScope.Harvest, new Vector3(14f, 0f, 4f));

        Assert.AreNotEqual(a, b,
            "잔해는 스물몇 개다. 고정 문자열을 열쇠로 쓰면 나중 것이 앞의 것을 덮는다");
    }

    [Test]
    public void 같은_자리라도_갈래가_다르면_열쇠가_다르다()
    {
        var p = new Vector3(3f, 1f, -2f);
        Assert.AreNotEqual(WorldId.At(WorldLedgerScope.Harvest, p),
                           WorldId.At(WorldLedgerScope.Plant, p),
                           "식물과 채집물이 겹쳐 서 있으면 자리만으로는 둘이 같은 줄을 놓고 다툰다");
    }

    /// <summary>
    /// <b>물리가 조금 밀어 놓아도 열쇠는 그대로여야 한다.</b> 부동소수 좌표를
    /// 글자로 그대로 만들면 저장할 때와 불러올 때의 열쇠가 달라져,
    /// 저장본이 아무 자리도 못 찾은 채 조용히 무시된다.
    /// </summary>
    [Test]
    public void 밀리미터쯤_밀려도_열쇠는_같다()
    {
        var a = WorldId.At(WorldLedgerScope.Harvest, new Vector3(10f, 0f, 4f));
        var b = WorldId.At(WorldLedgerScope.Harvest, new Vector3(10.004f, 0.003f, 3.998f));
        Assert.AreEqual(a, b);
    }

    /// <summary>
    /// 격자보다 가깝게 겹쳐 세우면 열쇠가 같아진다. <b>그때 조용히 덮어쓰지 않는다</b> —
    /// 열쇠 충돌은 저장 대상 쪽의 결함이고, 여기서 지워 버리면 그 결함이
    /// 저장본에서 사라져 추적할 수 없게 된다(<c>SaveSnapshot.Add</c>와 같은 판단).
    /// </summary>
    [Test]
    public void 열쇠가_겹치면_뒤엣것을_버리고_충돌로_적는다()
    {
        var ledger = new WorldLedger();
        ledger.BeginSweep();

        var 앞 = new WorldRecord { id = "harvest@1_0_1", kind = WorldLedgerScope.Harvest, gone = true, at = 100f };
        var 뒤 = new WorldRecord { id = "harvest@1_0_1", kind = WorldLedgerScope.Harvest, gone = true, at = 999f };

        Assert.IsTrue(ledger.Put(앞));
        Assert.IsFalse(ledger.Put(뒤), "같은 훑기에서 같은 신원이 두 번 오면 뒤엣것은 버린다");
        ledger.EndSweep();

        Assert.AreEqual(1, ledger.Conflicts.Count, "충돌이 저장본이 아니라 로그로 드러나야 한다");
        Assert.IsTrue(ledger.TryGet("harvest@1_0_1", out var 남은것));
        Assert.AreEqual(100f, 남은것.at, 0.001f, "먼저 적은 것이 남는다");
    }

    [Test]
    public void 자리가_다르면_충돌하지_않는다()
    {
        var ledger = new WorldLedger();
        ledger.BeginSweep();
        ledger.Put(new WorldRecord { id = WorldId.At(WorldLedgerScope.Harvest, new Vector3(0, 0, 0)), kind = WorldLedgerScope.Harvest, gone = true });
        ledger.Put(new WorldRecord { id = WorldId.At(WorldLedgerScope.Harvest, new Vector3(9, 0, 0)), kind = WorldLedgerScope.Harvest, gone = true });
        ledger.EndSweep();

        Assert.IsEmpty(ledger.Conflicts);
        Assert.AreEqual(2, ledger.Count);
    }

    // ══ ③ 씬이 주인이고 원장은 차이만 담는다 ═══════════════════

    /// <summary>
    /// <b>이것이 「다시 부활하는 채집물과 이미 다 캔 채집물」을 가르는 방식이다.</b>
    /// 돌아온 자리는 훑기에서 아무것도 적지 않고, 그러면 그 줄이 원장에서 빠진다.
    /// 빠진 자리는 불러올 때 「씬이 놓아둔 그대로」 — 곧 서 있다.
    /// 돌아오지 않는 것은 <c>gone</c>인 채로 남아 영영 비어 있다.
    /// </summary>
    [Test]
    public void 돌아온_자리는_원장에서_빠진다()
    {
        var ledger = new WorldLedger();
        const string id = "harvest@2_0_2";

        ledger.BeginSweep();
        ledger.Put(new WorldRecord { id = id, kind = WorldLedgerScope.Harvest, gone = true, at = 10f });
        ledger.EndSweep();
        Assert.AreEqual(1, ledger.Count, "다 캔 자리는 적힌다");

        // 다음 훑기에서는 다시 차 있으므로 아무것도 적지 않는다
        ledger.BeginSweep();
        ledger.PutUnchanged(id);
        ledger.EndSweep();

        Assert.AreEqual(0, ledger.Count, "돌아온 자리는 원장에서 빠진다");
        Assert.IsFalse(ledger.TryGet(id, out _));
    }

    [Test]
    public void 이번_훑기에_아무도_안_적은_줄은_버려진다()
    {
        var ledger = new WorldLedger();

        ledger.BeginSweep();
        ledger.Put(new WorldRecord { id = "harvest@1_0_1", kind = WorldLedgerScope.Harvest, gone = true });
        ledger.Put(new WorldRecord { id = "harvest@2_0_2", kind = WorldLedgerScope.Harvest, gone = true });
        ledger.EndSweep();
        Assert.AreEqual(2, ledger.Count);

        // 둘째 물건이 세계에서 사라졌다(철거·씬 전환). 아무도 적지 않는다.
        ledger.BeginSweep();
        ledger.Put(new WorldRecord { id = "harvest@1_0_1", kind = WorldLedgerScope.Harvest, gone = true });
        ledger.EndSweep();

        Assert.AreEqual(1, ledger.Count);
        Assert.IsFalse(ledger.TryGet("harvest@2_0_2", out _));
    }

    // ══ ④ 옛 저장본 ════════════════════════════════════════════

    /// <summary>
    /// <b>「세계」 절이 없던 저장본은 그냥 열린다.</b> 그때 올바른 세계는
    /// 씬이 놓아둔 그대로이고, 그것이 곧 「원장이 비어 있다」다.
    /// </summary>
    [Test]
    public void 세계_절이_없는_저장본은_그냥_열린다()
    {
        var ledger = new WorldLedger();
        ledger.BeginSweep();
        ledger.Put(new WorldRecord { id = "harvest@1_0_1", kind = WorldLedgerScope.Harvest, gone = true });
        ledger.EndSweep();

        Assert.DoesNotThrow(() => ledger.Restore(null));
        Assert.AreEqual(0, ledger.Count, "절이 없으면 씬이 놓아둔 그대로다");
    }

    [Test]
    public void 저장본을_한_바퀴_돌아도_같은_줄이_남는다()
    {
        var 원본 = new WorldLedger();
        원본.BeginSweep();
        원본.Put(new WorldRecord { id = "harvest@1_0_1", kind = WorldLedgerScope.Harvest, gone = true, at = 42.5f });
        원본.Put(new WorldRecord { id = "plant@0_0_3", kind = WorldLedgerScope.Plant, gone = false, amount = 2f });
        원본.EndSweep();

        var 문자열 = JsonUtility.ToJson(원본.Capture(1234.5f));
        var 되읽은것 = JsonUtility.FromJson<WorldLedgerState>(문자열);

        var 사본 = new WorldLedger();
        사본.Restore(되읽은것);

        Assert.AreEqual(2, 사본.Count);
        Assert.IsTrue(사본.TryGet("harvest@1_0_1", out var h));
        Assert.AreEqual(42.5f, h.at, 0.001f, "다 캔 시각이 글자를 건너서도 남는다");
        Assert.IsTrue(사본.TryGet("plant@0_0_3", out var p));
        Assert.AreEqual(2f, p.amount, 0.001f);
        Assert.AreEqual(1234.5f, 되읽은것.clockSeconds, 0.001f,
            "원장의 시각들이 어느 시계로 적힌 것인지가 함께 실린다");
    }

    /// <summary>
    /// 저장본에 담기지 않기로 한 갈래가 <b>손으로 고친 파일</b>로 들어와도
    /// 원장이 받아 주지 않는다. 저장본은 사람이 고칠 수 있는 입력이다.
    /// </summary>
    [Test]
    public void 담지_않는_갈래는_저장본에서_들어와도_막힌다()
    {
        var state = new WorldLedgerState();
        state.records.Add(new WorldRecord { id = "drop@1_0_1", kind = WorldLedgerScope.Drop });
        state.records.Add(new WorldRecord { id = "harvest@1_0_1", kind = WorldLedgerScope.Harvest, gone = true });

        var ledger = new WorldLedger();
        ledger.Restore(state);

        Assert.AreEqual(1, ledger.Count);
        Assert.IsFalse(ledger.TryGet("drop@1_0_1", out _));
    }
}
