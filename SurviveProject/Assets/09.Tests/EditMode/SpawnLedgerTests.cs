using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Survive.World;

/// <summary>
/// <b>생성 목록</b> — 세계 원장의 나머지 절반.
///
/// 원장(<see cref="WorldLedger"/>)은 <b>씬이 놓은 것의 변화</b>를 담는다.
/// 이 목록은 <b>씬에 없던 것의 존재</b>를 담는다. 규율이 정반대라 한 물건에
/// 넣을 수 없었고, 그래서 물건이 둘이 됐다. 여기서 못 박는 것이 넷이다.
/// <list type="number">
/// <item><b>신원이 없다.</b> 원장은 저장본의 줄을 씬에 서 있는 물건과 짝지어야
///   해서 열쇠가 필요했지만, 이쪽은 줄이 곧 몸을 만든다. 게다가 지을 수 있는
///   열쇠는 자리뿐인데 자리는 이쪽에서 신원이 되지 못한다 — 같은 자리에 다시
///   지을 수 있고, 낙하물은 굴러다닌다.</item>
/// <item><b>상한이 있고, 넘칠 때 무엇을 버리는지가 규칙으로 서 있다.</b>
///   무한히 쌓이면 저장본이 자라고, 버리는 쪽이 안 정해져 있으면 손실이
///   설명되지 않는 사고가 된다.</item>
/// <item><b>화톳불은 남은 초가 아니라 다 타는 시각을 적는다.</b>
///   원장이 이미 못 박은 규율(파생값 금지)이 이쪽에도 그대로 산다.</item>
/// <item><b>덧붙임이지 형식 변경이 아니다.</b> 이 목록이 없던 저장본이 그냥 열린다.</item>
/// </list>
/// </summary>
public class SpawnLedgerTests
{
    static SpawnRecord 건축물(string what, Vector3 at) => new SpawnRecord
    {
        kind = WorldLedgerScope.Structure,
        what = what,
        count = 1,
        position = at,
        rotation = Quaternion.identity,
    };

    static SpawnRecord 낙하물(string what, int count, Vector3 at) => new SpawnRecord
    {
        kind = WorldLedgerScope.Drop,
        what = what,
        count = count,
        position = at,
        rotation = Quaternion.identity,
    };

    // ══ ① 신원이 없다 ══════════════════════════════════════════

    /// <summary>
    /// <b>같은 자리에 다시 세울 수 있다.</b> 원장이었다면 두 번째 줄이 첫 번째와
    /// 열쇠가 같아 충돌로 잡혔을 것이고, 그것이 이 갈래를 원장에 못 넣는 이유였다.
    /// 이쪽은 차례가 신원이라 둘이 그냥 둘이다.
    /// </summary>
    [Test]
    public void 같은_자리에_둘이_서_있어도_서로를_덮지_않는다()
    {
        var 목록 = new SpawnLedger();
        목록.BeginSweep();
        Assert.IsTrue(목록.Put(건축물("piece_wall", new Vector3(3f, 0f, 3f))));
        Assert.IsTrue(목록.Put(건축물("piece_wall", new Vector3(3f, 0f, 3f))));
        목록.EndSweep();

        Assert.AreEqual(2, 목록.Count,
            "자리를 열쇠로 삼았다면 하나가 사라졌을 자리다");
    }

    /// <summary>
    /// 낙하물은 굴러다녀 자리가 신원이 되지 못한다. 열쇠가 없으므로 <b>같은
    /// 물건이 같은 자리에 여럿</b> 있어도 목록은 그대로 여럿으로 적는다.
    /// </summary>
    [Test]
    public void 굴러다니는_것도_차례로_적는다()
    {
        var 목록 = new SpawnLedger();
        목록.BeginSweep();
        목록.Put(낙하물("scrap", 3, Vector3.zero));
        목록.Put(낙하물("scrap", 1, Vector3.zero));
        목록.EndSweep();

        Assert.AreEqual(2, 목록.Count);
        Assert.AreEqual(3, 목록.Records[0].count, "먼저 떨어진 것이 앞에 남는다");
    }

    /// <summary>
    /// <b>화톳불 연료는 제 줄이 없다.</b> 몸이 없는 줄이 생기면 불러온 세계에
    /// 그 연료를 돌려줄 불이 없다 — 이 갈래를 원장에서 뺐던 바로 그 이유다.
    /// 연료는 몸을 싣는 건축물 줄에 실린다.
    /// </summary>
    [Test]
    public void 화톳불_연료는_제_줄을_갖지_않는다()
    {
        Assert.IsTrue(WorldLedgerScope.Spawns(WorldLedgerScope.CampfireFuel),
            "생성 목록이 담기는 한다");
        Assert.IsFalse(SpawnLedgerRule.HasRow(WorldLedgerScope.CampfireFuel),
            "다만 제 줄로 담지는 않는다");

        var 목록 = new SpawnLedger();
        목록.BeginSweep();
        bool 적혔나 = 목록.Put(new SpawnRecord
        {
            kind = WorldLedgerScope.CampfireFuel,
            what = "campfire",
            count = 1,
        });
        목록.EndSweep();

        Assert.IsFalse(적혔나, "몸 없는 줄은 문 앞에서 막힌다");
        Assert.AreEqual(0, 목록.Count);
    }

    [Test]
    public void 원장이_담는_갈래는_목록에_안_적힌다()
    {
        var 목록 = new SpawnLedger();
        목록.BeginSweep();
        bool 적혔나 = 목록.Put(new SpawnRecord
        {
            kind = WorldLedgerScope.Harvest,
            what = "LooseScrap",
            count = 1,
        });
        목록.EndSweep();

        Assert.IsFalse(적혔나, "차이를 담는 것은 저쪽 물건의 일이다");
    }

    /// <summary>
    /// 무엇으로 다시 만드는지 모르는 줄은 몸을 만들 수 없다. 저장본에 남겨 두면
    /// 불러올 때마다 경고만 내는 유령 줄이 된다.
    /// </summary>
    [Test]
    public void 무엇으로_만드는지_모르는_줄은_안_적힌다()
    {
        var 목록 = new SpawnLedger();
        목록.BeginSweep();
        Assert.IsFalse(목록.Put(건축물(null, Vector3.zero)));
        Assert.IsFalse(목록.Put(건축물("", Vector3.zero)));
        Assert.IsFalse(목록.Put(낙하물("scrap", 0, Vector3.zero)), "0개는 없는 것이다");
        Assert.IsFalse(목록.Put(null));
        목록.EndSweep();

        Assert.AreEqual(0, 목록.Count);
    }

    // ══ ② 상한과 버리는 규칙 ═══════════════════════════════════

    /// <summary>
    /// <b>낙하물은 뒤를 남긴다.</b> 사람이 지금 주우러 걸어가는 것은 방금
    /// 떨어진 것이고, 앞엣것은 이미 지나쳐 잊은 것이다. 규칙 §5.7의
    /// 「대가는 시간뿐」이 지켜지는 쪽도 이쪽이다.
    /// </summary>
    [Test]
    public void 낙하물이_넘치면_오래된_것부터_놓아_준다()
    {
        var 목록 = new SpawnLedger();
        목록.BeginSweep();

        int 넘게 = SpawnLedgerRule.DropCap + 7;
        for (int i = 0; i < 넘게; i++)
            목록.Put(낙하물("scrap", i + 1, new Vector3(i, 0f, 0f)));

        목록.EndSweep();

        Assert.AreEqual(SpawnLedgerRule.DropCap, 목록.Count, "상한만큼만 남는다");
        Assert.AreEqual(7, 목록.Trimmed, "놓아 준 수를 셀 수 있다");
        Assert.AreEqual(8, 목록.Records[0].count,
            "여덟 번째로 떨어진 것이 이제 맨 앞이다 — 앞에서부터 놓아 줬다");
        Assert.AreEqual(넘게, 목록.Records[목록.Count - 1].count,
            "가장 나중에 떨어진 것은 반드시 남는다");
        Assert.IsEmpty(목록.Overflowed, "낙하물이 잘리는 것은 사고가 아니라 규칙이다");
    }

    /// <summary>
    /// <b>건축물은 앞을 남긴다.</b> 사람이 가장 오래 산 거점이 앞에 있고,
    /// 그것을 잃는 것이 방금 놓은 벽 하나를 잃는 것보다 훨씬 나쁘다.
    /// 그리고 여기 닿는 것 자체가 사고이므로 <b>조용히</b> 버리지 않는다.
    /// </summary>
    [Test]
    public void 건축물이_넘치면_먼저_세운_것을_남기고_사고로_적는다()
    {
        var 목록 = new SpawnLedger();
        목록.BeginSweep();

        for (int i = 0; i < SpawnLedgerRule.StructureCap; i++)
            목록.Put(건축물("piece_wall", new Vector3(i, 0f, 0f)));

        목록.Put(건축물("나중에_세운_것", new Vector3(999f, 0f, 0f)));
        목록.EndSweep();

        Assert.AreEqual(SpawnLedgerRule.StructureCap, 목록.Count);
        Assert.AreEqual(1, 목록.Overflowed.Count,
            "건축물이 상한에 닿는 것은 사고다. 소리 없이 사라지면 안 된다");
        Assert.AreEqual("나중에_세운_것", 목록.Overflowed[0]);
        Assert.AreEqual(0f, 목록.Records[0].position.x, 0.001f, "처음 세운 것이 남는다");
    }

    /// <summary>
    /// 상한은 갈래마다 따로 센다. 낙하물이 가득 찼다고 건축물이 잘리면
    /// 「무엇을 버리는가」가 다른 갈래의 사정에 달리게 된다.
    /// </summary>
    [Test]
    public void 상한은_갈래마다_따로_센다()
    {
        var 목록 = new SpawnLedger();
        목록.BeginSweep();

        for (int i = 0; i < SpawnLedgerRule.DropCap + 10; i++)
            목록.Put(낙하물("scrap", 1, Vector3.zero));
        목록.Put(건축물("campfire", Vector3.zero));

        목록.EndSweep();

        Assert.AreEqual(SpawnLedgerRule.DropCap + 1, 목록.Count);
        Assert.AreEqual(1, 목록.Records.Count(r => r.kind == WorldLedgerScope.Structure),
            "화톳불은 낙하물이 넘쳤다고 사라지지 않는다");
    }

    /// <summary>저장본은 사람이 고칠 수 있는 입력이다. 들어올 때도 상한을 먹인다.</summary>
    [Test]
    public void 상한을_넘긴_저장본은_들어올_때도_잘린다()
    {
        var state = new SpawnLedgerState();
        for (int i = 0; i < SpawnLedgerRule.DropCap + 20; i++)
            state.records.Add(낙하물("scrap", 1, Vector3.zero));

        var 목록 = new SpawnLedger();
        목록.Restore(state);

        Assert.AreEqual(SpawnLedgerRule.DropCap, 목록.Count);
    }

    // ══ ③ 화톳불은 절대 시각을 적는다 ══════════════════════════

    /// <summary>
    /// <b>남은 초를 적었다면 저장·불러오기가 불을 공짜로 지켜 준다.</b>
    /// 며칠을 건너뛰고 돌아와도 불이 그대로 타고 있으면, 「불을 지키는 일이 곧
    /// 밖으로 나가는 이유」라는 규칙이 통째로 사라진다.
    /// </summary>
    [Test]
    public void 화톳불은_남은_초가_아니라_다_타는_시각을_적는다()
    {
        float 적을때 = 1000f;
        float 남은연료 = 120f;

        float 적힌값 = SpawnLedgerRule.BurnsOutAt(적을때, 남은연료);
        Assert.AreEqual(1120f, 적힌값, 0.001f);

        // 저장해 둔 사이에 세계가 30초 흘렀다
        Assert.AreEqual(90f, SpawnLedgerRule.FuelLeft(적힌값, 적을때 + 30f), 0.001f,
            "흐른 만큼 줄어 있어야 한다");

        // 다 타고도 남을 만큼 흘렀다
        Assert.AreEqual(0f, SpawnLedgerRule.FuelLeft(적힌값, 적을때 + 9999f), 0.001f,
            "다 지났으면 꺼진 채로 선다 — 음수가 되어 되살아나면 안 된다");
    }

    // ══ ④ 저장본을 건넌다 ═════════════════════════════════════

    /// <summary>
    /// <b>자리와 자세가 글자를 건너 그대로 남는다.</b> 이 목록이 담는 것은
    /// 「달라진 것」이 아니라 「무엇으로 어디에 다시 만드는가」라, 자리가
    /// 흔들리면 불러온 세계가 저장하던 세계와 다른 모양이 된다.
    /// </summary>
    [Test]
    public void 저장본을_한_바퀴_돌아도_자리와_자세가_남는다()
    {
        var 원본 = new SpawnLedger();
        원본.BeginSweep();

        var 자리 = new Vector3(12.375f, 3.5f, -8.125f);
        var 자세 = Quaternion.Euler(0f, 37.5f, 0f);
        원본.Put(new SpawnRecord
        {
            kind = WorldLedgerScope.Structure,
            what = "campfire",
            count = 1,
            position = 자리,
            rotation = 자세,
            until = 1450.25f,
            since = 1000.5f,
        });
        원본.Put(낙하물("scrap", 4, new Vector3(1.25f, 0.18f, 2.5f)));
        원본.EndSweep();

        var 문자열 = JsonUtility.ToJson(원본.Capture());
        var 되읽은것 = JsonUtility.FromJson<SpawnLedgerState>(문자열);

        var 사본 = new SpawnLedger();
        사본.Restore(되읽은것);

        Assert.AreEqual(2, 사본.Count);

        var 불 = 사본.Records[0];
        Assert.AreEqual("campfire", 불.what);
        Assert.AreEqual(자리.x, 불.position.x, 0.001f);
        Assert.AreEqual(자리.y, 불.position.y, 0.001f);
        Assert.AreEqual(자리.z, 불.position.z, 0.001f);
        Assert.AreEqual(자세.y, 불.rotation.y, 0.001f, "자세도 건넌다");
        Assert.AreEqual(1450.25f, 불.until, 0.001f, "다 타는 시각이 남는다");
        Assert.AreEqual(1000.5f, 불.since, 0.001f, "마지막으로 지핀 시각이 남는다");

        var 물건 = 사본.Records[1];
        Assert.AreEqual(4, 물건.count);
        Assert.AreEqual(0.18f, 물건.position.y, 0.001f,
            "내려앉은 높이가 그대로다 — 되풀이해도 물건이 떠오르지 않는다");
    }

    /// <summary>
    /// <b>생성 목록이 없던 저장본이 그냥 열린다.</b> 이 칸은 「세계」 절에
    /// <b>덧붙인</b> 것이라 형식 변경이 아니다. 그때 올바른 세계는 사람이 세운
    /// 것이 하나도 없는 세계이고, 실제로 그 저장본을 쓰던 세계에는 정말로
    /// 하나도 남아 있지 않았다(씬 로드로 사라졌다).
    /// </summary>
    [Test]
    public void 생성_목록이_없는_저장본은_그냥_열린다()
    {
        // 그 시절의 저장본 그대로 — spawned 칸이 아예 없다
        const string 옛것 =
            "{\"clockSeconds\":1234.5,\"seed\":42,\"records\":[" +
            "{\"id\":\"harvest@1_0_1\",\"kind\":\"harvest\",\"gone\":true,\"at\":42.5,\"amount\":0}]}";

        var 되읽은것 = JsonUtility.FromJson<WorldLedgerState>(옛것);

        Assert.IsNotNull(되읽은것.spawned, "없는 칸은 빈 목록으로 선다");
        Assert.AreEqual(0, 되읽은것.spawned.records.Count);

        var 원장 = new WorldLedger();
        원장.Restore(되읽은것);
        Assert.AreEqual(1, 원장.Count, "원장 쪽은 아무 영향도 안 받는다");
        Assert.AreEqual(1234.5f, 되읽은것.clockSeconds, 0.001f);
        Assert.AreEqual(42, 되읽은것.seed);

        var 목록 = new SpawnLedger();
        Assert.DoesNotThrow(() => 목록.Restore(되읽은것.spawned));
        Assert.AreEqual(0, 목록.Count, "사람이 세운 것이 하나도 없는 세계다");

        Assert.DoesNotThrow(() => 목록.Restore(null), "칸이 null이어도 열린다");
        Assert.AreEqual(0, 목록.Count);
    }

    /// <summary>
    /// <b>두 물건이 한 절에 실린다.</b> 절을 가르면 두 절의 복원 순서를 저장본이
    /// 정하게 되는데, 생성 목록은 시계와 시드가 앉은 뒤라야 옳게 되살아난다.
    /// </summary>
    [Test]
    public void 두_원장이_같은_절에_실린다()
    {
        var 원장 = new WorldLedger();
        원장.BeginSweep();
        원장.Put(new WorldRecord
        {
            id = "harvest@1_0_1", kind = WorldLedgerScope.Harvest, gone = true, at = 42.5f,
        });
        원장.EndSweep();

        var 목록 = new SpawnLedger();
        목록.BeginSweep();
        목록.Put(건축물("campfire", new Vector3(5f, 0f, 5f)));
        목록.EndSweep();

        var 절 = 원장.Capture(1234.5f, 7);
        절.spawned = 목록.Capture();

        var 되읽은것 = JsonUtility.FromJson<WorldLedgerState>(JsonUtility.ToJson(절));

        Assert.AreEqual(1, 되읽은것.records.Count, "달라진 것");
        Assert.AreEqual(1, 되읽은것.spawned.records.Count, "없던 것");
        Assert.AreEqual(7, 되읽은것.seed,
            "시드도 같은 절이다 — 그래서 목록이 앉을 때 이미 세계 시드가 서 있다");
    }

    // ══ ⑤ 훑기 ═════════════════════════════════════════════════

    /// <summary>
    /// <b>훑기가 시작할 때 목록을 비운다.</b> 철거된 건축물과 주워진 물건은
    /// 아무 일도 하지 않아도 빠진다. 원장 쪽의 「이번 훑기에 아무도 안 적은 줄은
    /// 버린다」와 겉모습은 같지만 뜻이 다르다 — 저쪽은 「돌아왔다」이고
    /// 이쪽은 「없어졌다」다.
    /// </summary>
    [Test]
    public void 철거된_것은_다음_훑기에서_저절로_빠진다()
    {
        var 목록 = new SpawnLedger();

        목록.BeginSweep();
        목록.Put(건축물("campfire", Vector3.zero));
        목록.Put(건축물("bench", Vector3.one));
        목록.EndSweep();
        Assert.AreEqual(2, 목록.Count);

        // 벤치를 철거했다. 다음 훑기에서는 그것을 적는 사람이 없다.
        목록.BeginSweep();
        목록.Put(건축물("campfire", Vector3.zero));
        목록.EndSweep();

        Assert.AreEqual(1, 목록.Count);
        Assert.AreEqual("campfire", 목록.Records[0].what);
    }
}
