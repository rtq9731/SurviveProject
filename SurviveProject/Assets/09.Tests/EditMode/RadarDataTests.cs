using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Crafting;
using Survive.Instruments;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;

/// <summary>
/// 챕터 1 재건 스펙 §8-2 — 레이더의 <b>데이터와 배선</b>.
///
/// 규칙이 맞아도 에셋이 틀리면 게임 안에서는 아무 일도 일어나지 않는다. 이 라운드가
/// 만든 것은 사슬 하나다: <b>자리에 닿음 → 발견 → 청사진 → 제작법 → 장비</b>.
/// 한 마디라도 끊기면 강 건너 작은 섬에 가도 아무 일이 없거나, AI가 말은 하는데
/// 만들 수 있는 것이 늘지 않는다.
///
/// <b>배치는 사람의 몫이다</b>(스펙 §16·§17). A섬 작은 섬이 아직 없으므로 트리거를
/// 씬에 심는 것은 여기서 하지 않는다. 여기서 하는 것은 심는 순간 조용히 아무 일도
/// 안 일어나는 상태가 없게 못 박는 것까지다.
/// </summary>
public class RadarDataTests
{
    const string DbPath = "Assets/08.Data/Items/ItemDatabase.asset";
    const string RecipeBookPath = "Assets/08.Data/Recipes/RecipeBook.asset";
    const string DiscoveryBookPath = "Assets/08.Data/Progression/Resources/DiscoveryBook.asset";

    const string 레이더 = "radar";
    const string 청사진id = "bp_radar";
    const string 발견id = "disc_radar_islet";

    /// <summary>
    /// A섬 강 건너 작은 섬. <b>이 문자열이 계약이다</b> — 사람이 씬에
    /// <c>LocationDiscoveryTrigger</c>를 심을 때 여기에 이 값을 적어야 한다.
    /// </summary>
    const string 장소id = "a_islet";

    static ItemDatabaseSO DB => AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DbPath);

    static RecipeBookSO 제작법목록()
    {
        var b = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(RecipeBookPath);
        Assert.IsNotNull(b, RecipeBookPath + "를 못 읽었다");
        return b;
    }

    static DiscoveryBookSO 발견목록()
    {
        var b = AssetDatabase.LoadAssetAtPath<DiscoveryBookSO>(DiscoveryBookPath);
        Assert.IsNotNull(b, DiscoveryBookPath + "를 못 읽었다");
        return b;
    }

    static RadarItemSO 레이더에셋()
    {
        var db = DB;
        Assert.IsNotNull(db, DbPath + "를 못 읽었다");

        var item = db.GetById(레이더);
        Assert.IsNotNull(item, $"ItemDatabase에 {레이더}가 없다 — 세이브를 다시 불러오면 조용히 사라진다");

        var radar = item as RadarItemSO;
        Assert.IsNotNull(radar,
            "레이더가 RadarItemSO가 아니다 — 대역이 없으면 무엇이 잡히는지 정할 값이 없다");
        return radar;
    }

    static RecipeSO 제작법()
    {
        var r = 제작법목록().recipes.FirstOrDefault(x => x != null && x.id == 레이더);
        Assert.IsNotNull(r, $"제작법 목록에 {레이더}가 없다 — 제작 화면에 안 뜬다");
        return r;
    }

    static DiscoverySO 발견()
    {
        var d = 발견목록().discoveries.FirstOrDefault(x => x != null && x.id == 발견id);
        Assert.IsNotNull(d, $"발견 목록에 {발견id}가 없다 — 작은 섬에 가도 아무 일이 없다");
        return d;
    }

    // ── 아이템 ───────────────────────────────────────────────

    [Test]
    public void 레이더가_아이템_DB에_등록돼_있다()
    {
        var radar = 레이더에셋();

        Assert.IsNotEmpty(radar.displayName, "이름이 없다");
        Assert.IsNotEmpty(radar.description, "설명이 없다");
        Assert.IsNotNull(radar.icon, "아이콘이 없으면 바닥에서 알 수 없는 덩어리로 떨어진다");
        Assert.AreEqual(ItemCategory.Tool, radar.category, "쓰는 물건이지 재료가 아니다");
        Assert.AreEqual(1, radar.maxStack, "레이더를 여러 대 지고 다닐 이유가 없다");
    }

    [Test]
    public void 레이더_이름과_설명이_번역_표에_있다()
    {
        var radar = 레이더에셋();

        Assert.IsTrue(Loc.TryT(DataText.NameKey(radar), out _), $"Item/{레이더}.name이 표에 없다");
        Assert.IsTrue(Loc.TryT(DataText.DescKey(radar), out _), $"Item/{레이더}.desc가 표에 없다");
    }

    [Test]
    public void 레이더_화면이_쓰는_글자가_전부_표에_있다()
    {
        // 화면에 나가는 말은 코드에 적을 수 없다. 하나라도 빠지면 그 자리에
        // 열쇠 이름이 그대로 찍힌다.
        var 열쇠들 = new[]
        {
            "scanning", "progress", "result_title", "result_empty", "reading",
            "cancel_moved", "cancel_power", "cancel_aborted", "spent",
            "kind_island", "kind_cavity", "kind_deep_layer", "kind_structure",
            "kind_creature", "kind_fissure", "kind_unknown",
        };

        foreach (var key in 열쇠들)
            Assert.IsTrue(Loc.TryT(new LocKey(RadarText.Category, key), out _),
                $"{RadarText.Category}/{key}가 표에 없다");
    }

    [Test]
    public void 종류마다_화면에_적을_이름이_있다()
    {
        // 종류를 하나 늘려 놓고 표를 잊으면 결과 화면에 열쇠 이름이 찍힌다.
        foreach (RadarContactKind kind in System.Enum.GetValues(typeof(RadarContactKind)))
            Assert.IsNotEmpty(RadarText.KindName(kind), $"{kind}에 이름이 없다");
    }

    // ── 대역: 에셋의 값이 스펙의 제약을 실제로 만든다 ────────

    [Test]
    public void 에셋의_대역이_섬과_공동은_잡고_낫과_균열은_놓친다()
    {
        // 규칙이 맞아도 파장을 잘못 적으면 게임 안에서는 낫이 잡힌다.
        // 규칙 테스트와 따로 두는 이유가 그것이다.
        var band = 레이더에셋().band;

        Assert.Less(band.ResolutionMeters, 90f,
            "해상도가 공동보다 거칠면 바다 아래를 못 본다");
        Assert.Greater(band.ResolutionMeters, 3f,
            "해상도가 낫보다 고우면 어둠 속에서 낫이 화면에 뜬다");
        Assert.Less(band.MaxTrackableSpeedMps, 3f,
            "추적 한계가 낫의 이동 속도를 넘으면 크기와 무관하게 따라간다");
        Assert.Greater(band.PenetrationDepthMeters, 200f,
            "투과 깊이가 얕으면 바다 아래 깊은 층이 안 잡힌다");
        Assert.Greater(band.rangeMeters, 2000f,
            "닿는 거리가 짧으면 다른 섬이 안 잡혀 §14 4단계가 성립하지 않는다");
    }

    [Test]
    public void 결맞음_반경이_걸음_한_번보다_좁다()
    {
        // "움직이면 끊긴다"가 성립하려면 반경이 사람의 한 걸음보다 좁아야 한다.
        // 이것이 넓어지면 걸어 다니면서 관측할 수 있게 되어 정지 시간이라는 값이 사라진다.
        Assert.Less(레이더에셋().band.CoherenceRadiusMeters, 1.5f);
    }

    [Test]
    public void 한_번_관측이_배터리의_한_귀퉁이를_먹는다()
    {
        // 공짜면 정보를 사는 값이 없고, 한 번에 다 먹으면 쓸 수가 없다.
        var radar = 레이더에셋();

        Assert.Greater(radar.ScanCost, 0f, "공짜 관측은 값이 아니다");
        Assert.Less(radar.ScanCost, radar.chargePerCell,
            "한 번에 셀 하나를 통째로 먹으면 셀을 쪼갠 뜻이 없다");
        Assert.Greater(radar.scanSeconds, 2f, "서 있는 시간이 짧으면 무방비라는 값이 없다");
        Assert.GreaterOrEqual(radar.maxCharge, radar.chargePerCell,
            "통이 셀 하나보다 작으면 끼울 때마다 남는 것이 버려진다");
    }

    [Test]
    public void 레이더가_먹는_것이_랜턴이_먹는_것과_같은_물건이다()
    {
        // 다툼이 여기서 일어난다 — 관측 한 번은 랜턴을 그만큼 못 켠다는 뜻이다.
        // 셀이 다른 물건이 되면 그 다툼이 사라진다.
        var db = DB;
        Assert.IsNotNull(db.GetById("battery_cell"),
            "배터리 셀이 ItemDatabase에 없다 — 레이더에 넣을 것이 없어진다");
    }

    [Test]
    public void 셀_하나가_채우는_양이_랜턴과_같다()
    {
        // 랜턴의 batteryPerCell(100)과 어긋나면 같은 셀이 어느 장치에 넣느냐에 따라
        // 다른 양이 되어, 셀 하나의 값어치를 사람이 셀 수 없게 된다.
        // 랜턴 쪽은 Assembly-CSharp이라 이 어셈블리에서 참조할 수 없어 값을 여기 적는다.
        const float 랜턴의_셀당_충전량 = 100f;
        Assert.AreEqual(랜턴의_셀당_충전량, 레이더에셋().chargePerCell, 0.01f);
    }

    // ── 사슬: 자리 → 발견 → 청사진 → 제작법 → 장비 ──────────

    [Test]
    public void 발견의_계기가_아이템이_아니라_자리다()
    {
        var d = 발견();

        Assert.IsNull(d.item, "레이더는 주워서 얻는 것이 아니다 — 거기 갔다는 사실이 여는 것이다");
        Assert.AreEqual(장소id, d.locationId, "장소 id가 계약과 다르다");
    }

    [Test]
    public void 자리에_처음_닿으면_청사진이_열린다()
    {
        var book = 발견목록();
        var ledger = new UnlockLedger();

        Assert.IsTrue(LocationDiscovery.TryDiscover(book, ledger, 장소id, out var d));
        Assert.AreEqual(발견id, d.id);
        Assert.IsTrue(ledger.IsUnlocked(청사진id));
    }

    [Test]
    public void 두_번째부터는_조용하다()
    {
        var book = 발견목록();
        var ledger = new UnlockLedger();

        LocationDiscovery.TryDiscover(book, ledger, 장소id, out _);

        Assert.IsFalse(LocationDiscovery.TryDiscover(book, ledger, 장소id, out _),
            "같은 자리에 다시 들어갔다고 AI가 또 말하면 안 된다");
    }

    [Test]
    public void 자리_발견도_원장에_남아_저장을_왕복한다()
    {
        var book = 발견목록();
        var ledger = new UnlockLedger();
        LocationDiscovery.TryDiscover(book, ledger, 장소id, out _);

        var 이어받은판 = new UnlockLedger();
        이어받은판.Restore(ledger.Capture());

        Assert.IsFalse(LocationDiscovery.TryDiscover(book, 이어받은판, 장소id, out _),
            "불러온 뒤에 다시 밟으면 첫 도달이 되풀이된다");
    }

    [Test]
    public void 자리_발견은_아이템_계기로는_열리지_않는다()
    {
        // 계기가 섞이면 어느 쪽으로 열렸는지 알 수 없다.
        var book = 발견목록();
        Assert.IsNull(book.Find(장소id), "장소 id가 아이템 id처럼 찾아진다");
        Assert.IsNull(book.FindByLocation(레이더), "아이템 id가 장소 id처럼 찾아진다");
    }

    [Test]
    public void 청사진이_제작법에_실제로_물려_있다()
    {
        var r = 제작법();

        Assert.IsNotNull(r.requiredBlueprint, "요구 청사진이 없으면 처음부터 만들 수 있다");
        Assert.AreEqual(청사진id, r.requiredBlueprint.id);
        Assert.AreEqual(레이더, r.result?.item?.id, "제작법이 레이더를 내놓지 않는다");
    }

    [Test]
    public void 자리에_닿기_전에는_레이더를_만들_수_없다()
    {
        var r = 제작법();
        Assert.IsFalse(BlueprintGate.IsUnlocked(r.requiredBlueprint, new UnlockLedger()));
    }

    [Test]
    public void 자리에_닿으면_바로_만들_수_있게_된다()
    {
        var ledger = new UnlockLedger();
        LocationDiscovery.TryDiscover(발견목록(), ledger, 장소id, out _);

        Assert.IsTrue(BlueprintGate.IsUnlocked(제작법().requiredBlueprint, ledger),
            "발견을 겪었는데도 잠겨 있다면 사슬이 끊긴 것이다");
    }

    [Test]
    public void 레이더_재료가_A섬에서_구할_수_있는_것들이다()
    {
        // §14 3단계에서 얻는 장비다. B섬 재료를 요구하면 순서가 뒤집혀
        // 레이더 없이 B섬에 먼저 닿아야 한다.
        var A섬재료 = new[] { "scrap", "machine_part", "fern_fiber", "mushroom_wood", "battery_cell" };

        var r = 제작법();
        Assert.IsNotEmpty(r.ingredients, "재료가 없으면 제작이 아니라 버튼이다");

        foreach (var i in r.ingredients)
        {
            Assert.IsNotNull(i?.item, "재료 칸이 비어 있다");
            Assert.Greater(i.count, 0, $"{i.item.id}의 수량이 0이다");
            Assert.Contains(i.item.id, A섬재료,
                $"{i.item.id}는 A섬에서 구할 수 없다 — 레이더보다 먼저 얻어야 하는 것이 생긴다");
        }
    }

    [Test]
    public void 레이더는_손으로_만든다()
    {
        // 작은 섬에서 청사진을 얻고 제작대까지 돌아가야 한다면, 배가 없는 챕터
        // 초반에 그 왕복이 진행을 막는다.
        Assert.AreEqual(StationType.None, 제작법().requiredStation);
    }

    // ── AI 대사 ──────────────────────────────────────────────

    /// <summary>
    /// <b>분석 보고에서 제안으로 넘어가는 첫 대사다.</b>
    ///
    /// 재료 발견의 정형구("해당 물질을 사용한 제작법이 있습니다")는 <b>알려 주는</b>
    /// 말이다. 여기서 처음으로 AI가 <b>권한다</b>. 그 전환이 이 대사의 요점이므로
    /// 권한다는 것을 못 박되, 말투 규격(관찰투·세 문장·감탄 금지)은 그대로 지킨다.
    ///
    /// 닫는 말은 여전히 제작법이다 — 이 저장소에는 "AI가 말했는데 목록이 안 자라는
    /// 발견"을 막는 게이트가 있고, 문장 자체도 그 약속을 지켜야 읽는 사람이 속지 않는다.
    /// </summary>
    [Test]
    public void AI가_관찰에서_제안으로_넘어간다()
    {
        var line = 발견().line;

        Assert.IsNotNull(line, "대사가 null이다");
        Assert.AreEqual("우주복 AI", line.speaker, "화자가 다르다");
        Assert.IsNotEmpty(line.text, "아무 말도 하지 않는다");

        Assert.IsTrue(line.text.Contains("분석..."),
            $"첫 문장이 '분석...'으로 닫히지 않는다 — \"{line.text}\"");
        Assert.IsTrue(line.text.Contains("것으로 보입니다") || line.text.Contains("판단됩니다"),
            $"판정이 단정이다(관찰투가 아니다) — \"{line.text}\"");
        Assert.IsTrue(line.text.Contains("권장"),
            $"권하지 않는다 — 이 대사의 요점은 제안이다 — \"{line.text}\"");
        Assert.IsTrue(line.text.EndsWith("제작법이 있습니다."),
            $"해금 안내로 닫지 않는다 — \"{line.text}\"");

        int 문장수 = line.text.Count(c => c == '.') - 2;   // "분석..."의 말줄임표 셋 중 둘은 문장이 아니다
        Assert.LessOrEqual(문장수, 3, $"세 문장을 넘는다({문장수}) — \"{line.text}\"");

        foreach (var 금지 in new[] { "!", "?", "겠어", "네요", "흥미" })
            Assert.IsFalse(line.text.Contains(금지),
                $"기계가 하지 않는 말이 섞였다({금지}) — \"{line.text}\"");
    }

    [Test]
    public void 대사가_번역_표에_있다()
    {
        var d = 발견();
        Assert.IsTrue(Loc.TryT(DataText.LineKey(d), out _), $"Discovery/{발견id}.line.text가 표에 없다");
    }

    // ── 다른 발견을 망가뜨리지 않았다 ────────────────────────

    [Test]
    public void 계기가_둘_다_비어_있는_발견이_없다()
    {
        // 계기가 없으면 영원히 일어나지 않는 발견이고, 그것은 목록에 있으면서
        // 아무 신호도 내지 않는다.
        foreach (var d in 발견목록().discoveries)
        {
            Assert.IsNotNull(d, "발견 목록에 빈 칸이 있다");
            Assert.IsTrue(d.item != null || !string.IsNullOrWhiteSpace(d.locationId),
                $"{d.id}에 계기가 없다 — 영원히 일어나지 않는다");
        }
    }

    [Test]
    public void 계기를_둘_다_가진_발견이_없다()
    {
        foreach (var d in 발견목록().discoveries)
            Assert.IsFalse(d.item != null && !string.IsNullOrWhiteSpace(d.locationId),
                $"{d.id}가 계기를 둘 다 갖고 있다 — 어느 쪽으로 열렸는지 알 수 없다");
    }

    [Test]
    public void 장소_id가_겹치지_않는다()
    {
        var 본것 = new System.Collections.Generic.HashSet<string>();
        foreach (var d in 발견목록().discoveries)
        {
            if (d == null || string.IsNullOrWhiteSpace(d.locationId)) continue;
            Assert.IsTrue(본것.Add(d.locationId), $"장소 id가 겹친다: {d.locationId}");
        }
    }

    [Test]
    public void 아이템_계기는_예전_그대로_돈다()
    {
        // 몸통을 DiscoveryChannel로 옮겼다. 옮기면서 채널 1이 조용해지면
        // 스크랩을 처음 주워도 아무 일도 안 일어난다.
        var ledger = new UnlockLedger();

        Assert.IsTrue(FieldDiscovery.TryDiscover(발견목록(), ledger, "scrap", out var d));
        Assert.AreEqual("disc_scrap", d.id);
        Assert.IsFalse(FieldDiscovery.TryDiscover(발견목록(), ledger, "scrap", out _));
    }

    [Test]
    public void 두_계기가_같은_열쇠를_쓰지_않는다()
    {
        // 아이템 id와 장소 id가 우연히 같아도 서로를 덮으면 안 된다.
        var 아이템쪽 = ScriptableObject.CreateInstance<DiscoverySO>();
        아이템쪽.item = ScriptableObject.CreateInstance<ItemDataSO>();
        아이템쪽.item.id = "같은이름";

        var 장소쪽 = ScriptableObject.CreateInstance<DiscoverySO>();
        장소쪽.locationId = "같은이름";

        Assert.AreNotEqual(DiscoveryChannel.KeyOf(아이템쪽), DiscoveryChannel.KeyOf(장소쪽));
    }
}
