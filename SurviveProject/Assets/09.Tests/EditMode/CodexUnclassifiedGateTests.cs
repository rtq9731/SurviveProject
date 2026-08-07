using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Creatures;
using Survive.Harvesting;
using Survive.Localization;
using Survive.Progression;
using Survive.UI;

/// <summary>
/// 도감이 계층을 알려 주지 않는다는 것을 못 박는 게이트 (기획서 §4.7 · 스펙 §10).
///
/// <b>무엇을 지키는가.</b> 분석 기록은 <b>전부 미분류로 시작한다.</b> 분해자·생산자·
/// 소비자 같은 영양 단계는 우리가 생물을 만들 때 쓰는 뼈대이지 플레이어가 보는
/// 것이 아니다. AI는 <b>관찰한 것만 기록하고 무엇인지는 판정하지 않는다.</b>
///
/// <b>왜 게이트가 필요한가.</b> 계층명은 지우기는 쉽고 되살리기는 더 쉽다 —
/// <c>Loc.T("Codex", "tier_…")</c> 한 줄이면 돌아온다. 그러면 화면에는 아무 오류도
/// 뜨지 않고 설계만 조용히 뒤집힌다. 잃는 것이 크다.
/// <list type="number">
/// <item>계층을 도감이 알려 주지 않아야 <b>"다리 개수 = 포식 차수"</b> 같은 규칙을
///       플레이어가 관찰로 발견한다. 생태계를 읽으면 이득을 본다는 차별화 축이
///       필드에서만이 아니라 열람 화면에서도 성립한다.</item>
/// <item>낫이 규칙 밖 존재라는 사실이 분류표의 예외가 아니라 <b>그냥 알 수 없는 것</b>이
///       된다. 낫에게 별도 분류명을 붙이면 "이건 특별하다"고 알려 주는 셈이다.</item>
/// <item>「모르는 것은 보여 주지 않는다」(§5.12)와 같은 문법이다.</item>
/// </list>
///
/// <b>데이터의 <see cref="CreatureDefinitionSO.tier"/>는 건드리지 않는다.</b>
/// 보여 주지 않는 것과 없는 것은 다르다 — 그 값은 여전히 포식 차수 규칙과
/// 연구·드롭이 읽는다. 여기서 보는 것은 <b>화면에 나가는 말</b>뿐이다.
///
/// <b>무엇을 훑는가.</b> 실제 에셋(<c>Resources</c>의 발견·연구·생물 목록)과 실제 표를
/// 그대로 써서 도감이 지을 수 있는 문자열을 전부 짓고, 그 안에 계층 낱말이 있는지 본다.
/// 가짜 에셋으로 검사하면 진짜 데이터가 계층을 흘려도 초록불이 켜진다.
/// </summary>
public class CodexUnclassifiedGateTests
{
    /// <summary>
    /// 계층 낱말. <b>조각으로 짓는다</b> — 이 파일 자신이 다른 훑개
    /// (<see cref="AssetTextScan"/>)에 걸리지 않게 하려는 것이 아니라,
    /// 여기 적은 말이 그대로 표에 복사되어 되살아나는 일을 막으려는 것이다.
    ///
    /// 영어 쪽은 <c>consum</c>이 아니라 <c>consumer</c>다 — <c>Consumable</c>(소모품)이
    /// 애먼 데서 걸린다. 훑개가 너무 넓으면 아무도 못 고치고 결국 예외 목록이 생긴다.
    /// </summary>
    static readonly string[] 계층어 =
    {
        "분해" + "자", "생산" + "자", "소비" + "자", "미분" + "류", "영양 " + "단계",
        "decompo" + "ser", "produ" + "cer", "consu" + "mer", "unclassi" + "fied", "troph" + "ic",
    };

    /// <summary>도감이 자기 목소리로 쓰는 말이 사는 구역.</summary>
    const string 도감구역 = "Codex";

    string _처음로케일;

    [SetUp]
    public void 로케일을_기억해_둔다() => _처음로케일 = Loc.CurrentLocale;

    [TearDown]
    public void 로케일을_되돌린다() => Loc.SetLocale(_처음로케일);

    // ── ① 도감이 내놓는 말 ──────────────────────────────────────

    /// <summary><b>이 게이트가 이 파일의 알맹이다.</b></summary>
    [Test]
    public void 도감이_내놓는_어떤_문자열에도_계층명이_없다()
    {
        var 걸린것 = 계층명이_섞인_자리(도감이_내놓는_모든_문자열());

        Assert.IsEmpty(걸린것,
            $"도감이 계층을 알려 주고 있다 ({걸린것.Count}군데). " +
            "AI는 관찰한 것만 기록하고 무엇인지는 판정하지 않는다 (기획서 §4.7):\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary>
    /// <b>음성 확인.</b> 위 검사는 초록불일 때 아무 말도 하지 않는다 — 모으는 쪽이
    /// 조용히 망가져 늘 빈 목록을 내도 통과한다. 그래서 훑는 자리에 <b>진짜 도감의 말이
    /// 들어 있는지</b>를 따로 못 박는다.
    /// </summary>
    [Test]
    public void 훑는_자리에_진짜_도감의_말이_들어_있다()
    {
        var 글 = 도감이_내놓는_모든_문자열().Select(항목 => 항목.글).ToList();

        Assert.Greater(글.Count, 40, "모은 것이 너무 적다 — 실제 목록을 못 읽었을 수 있다");
        Assert.IsTrue(글.Any(s => s.Contains("낫")), "관측한 생물의 이름을 못 담았다");
        Assert.IsTrue(글.Any(s => s.Contains("지상")), "한국어 관측 보고를 못 담았다");
        Assert.IsTrue(글.Any(s => s.Contains("Ground")), "영어 관측 보고를 못 담았다");
        Assert.IsTrue(글.Any(s => s.Contains(CodexCatalog.UnknownTitle)), "잠긴 줄을 못 담았다");
    }

    /// <summary>
    /// <b>음성 확인 둘.</b> 계층명을 일부러 되살려 놓고 게이트가 무너지는지 본다.
    /// 되살리는 자리는 도감이 실제로 읽는 곳(생물 정의의 설명문)이라, 훑개만이 아니라
    /// <see cref="CodexCatalog.DescribeCreature"/>까지 함께 검사된다.
    /// </summary>
    [Test]
    public void 계층명을_되살리면_게이트가_무너진다()
    {
        foreach (var 말 in 계층어)
        {
            var 시험체 = ScriptableObject.CreateInstance<CreatureDefinitionSO>();
            시험체.id = "gate_probe";           // 표에 없는 id — 설명문이 그대로 폴백된다
            시험체.displayName = "시험체";
            시험체.codexDescription = $"관측 결과 {말} 계열로 보입니다.";

            var 보고 = CodexCatalog.DescribeCreature(시험체);
            var 걸린것 = 계층명이_섞인_자리(new[] { (자리: "시험체", 글: 보고) });

            Assert.IsNotEmpty(걸린것,
                $"\"{말}\"을 되살렸는데 게이트가 잡지 못한다 — 0건은 아무 뜻이 없다");
        }
    }

    // ── ② 표에 죽은 열쇠가 없다 ─────────────────────────────────

    /// <summary>
    /// 죽은 열쇠를 남기면 다음 사람이 되살린다. 아무도 안 부르는
    /// <c>Codex,tier_*</c>는 "예전엔 이렇게 했었지"라는 안내문이나 다름없다.
    /// </summary>
    [Test]
    public void 표의_도감_구역에_계층_열쇠도_계층_낱말도_없다()
    {
        var catalog = LocalizationTestBootstrap.LoadCatalogFromDisk();

        var 죽은열쇠 = catalog.Keys
            .Where(k => k.Category == 도감구역 && k.Key.StartsWith("tier"))
            .Select(k => k.ToString())
            .ToList();
        Assert.IsEmpty(죽은열쇠,
            "도감 구역에 계층 열쇠가 남아 있다. 아무도 안 쓰는 줄은 지운다:\n  " +
            string.Join("\n  ", 죽은열쇠));

        var 값들 = new List<(string 자리, string 글)>();
        foreach (var locale in catalog.Locales)
            foreach (var pair in catalog.TableFor(locale))
                if (pair.Key.Category == 도감구역)
                    값들.Add(($"{locale} {pair.Key}", pair.Value));

        var 걸린것 = 계층명이_섞인_자리(값들);
        Assert.IsEmpty(걸린것,
            "표의 도감 구역이 계층 낱말을 들고 있다:\n  " + string.Join("\n  ", 걸린것));
    }

    /// <summary>
    /// 관측 보고의 <b>자리 수</b>가 줄었는지 본다. 옛 틀(세 칸)을 그대로 두고 첫 칸에
    /// 빈 글자를 넘기는 길로 도망가면 화면에서는 사라진 것처럼 보이지만
    /// 계층 자리는 그대로 남는다 — 되돌리는 데 한 줄이면 된다.
    /// </summary>
    [Test]
    public void 관측_보고의_틀에서_계층_자리가_사라졌다()
    {
        var catalog = LocalizationTestBootstrap.LoadCatalogFromDisk();

        Assert.IsFalse(catalog.Contains(new LocKey(도감구역, "creature_" + "traits")),
            "세 칸짜리 옛 틀이 남아 있다 — 자리를 없애야 계층이 못 돌아온다");
        Assert.IsTrue(catalog.Contains(new LocKey(도감구역, "creature_observed")),
            "두 칸짜리 새 틀이 없다");

        foreach (var locale in catalog.Locales)
        {
            var 틀 = catalog.TableFor(locale)[new LocKey(도감구역, "creature_observed")];
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, LocFormat.Indices(틀),
                $"{locale}의 관측 보고 틀에 자리가 둘이 아니다: \"{틀}\"");
        }
    }

    // ── ③ 식물은 여전히 따로 묶여 있다 ──────────────────────────

    /// <summary>
    /// <b>다 지워 버리지 않았는지</b> 본다.
    ///
    /// 계층을 걷어내는 김에 <b>식물까지</b> 생물과 한 덩어리로 밀어 넣으면 그것은
    /// 정보 은폐가 아니라 불친절이다 — 식물은 움직이지 않고 위협이 아니므로 계열이
    /// 명백하고, 명백한 것을 감추는 데는 얻을 것이 없다 (기획서 §4.7).
    ///
    /// <b>식물은 라벨이 아니라 갈래로 분리되어 있다.</b> "이것은 식물이다"라고 적어 주는
    /// 대신, 식물은 아예 다른 종류의 정의(<see cref="PlantNodeSO"/>)이고 이름도 자기
    /// 구역(<c>Plant</c>)에서 나오며 개체 관측 목록에는 실리지 않는다. 계층명을 지우면서
    /// 이 구분까지 지우면 이 검사가 무너진다.
    ///
    /// <b>식물에게 도감 탭이 생겼다 (검토회신 ⑪).</b> ⑩ 라운드에서는 "탭 하나를 세우려면
    /// 식물 목록 에셋과 해금 경로를 새로 지어야 한다"는 이유로 미뤘는데, 회신이 그것을
    /// <b>지으라고</b> 정했다 — "전부 미분류, 단 식물은 따로 분리"가 곧 식물만 갈래를
    /// 갖는 구조라는 뜻이다. 그래서 <see cref="PlantBookSO"/>와 <c>plant_</c> 열쇠가 섰다.
    ///
    /// <b>그래도 이 검사는 그대로다.</b> 탭이 생긴 것과 라벨이 생긴 것은 다른 일이다.
    /// 식물은 여전히 다른 종류의 정의이고, 이름도 자기 구역에서 나오고, 개체 관측
    /// 목록에는 실리지 않는다. 탭이 그 구분을 대신하는 것이 아니라 <b>드러내는</b> 것이다.
    /// 그리고 식물 탭이 내놓는 모든 문자열은 아래 훑개가 계층어로 검사한다 —
    /// 탭이 계층명을 다시 들여오는 통로가 되면 첫 번째 검사가 잡는다.
    /// </summary>
    [Test]
    public void 식물은_여전히_생물과_따로_묶여_있다()
    {
        var 식물들 = AssetDatabase.FindAssets("t:" + nameof(PlantNodeSO))
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<PlantNodeSO>)
            .Where(p => p != null)
            .ToList();

        Assert.IsNotEmpty(식물들, "식물 정의를 하나도 못 찾았다 — 갈래가 통째로 사라졌다");

        foreach (var p in 식물들)
        {
            Assert.AreEqual(DataText.Category.Plant,
                            DataText.NameKey(p).Category,
                            $"{p.name}의 이름이 식물 구역에서 나오지 않는다");
            Assert.IsNotEmpty(DataText.Name(p),
                              $"{p.name}의 이름이 비었다");
        }

        // 개체 관측 목록에 식물이 섞이지 않았는가. 종류가 달라 컴파일러가 막아 주지만,
        // 이름이 겹치면 화면에서는 같은 것으로 보인다.
        var 식물이름 = new HashSet<string>(식물들.Select(p => DataText.Name(p)));
        var 생물목록 = Resources.Load<CreatureBookSO>("CreatureBook");
        Assert.IsNotNull(생물목록, "생물 목록을 못 읽었다");

        foreach (var c in 생물목록.creatures)
        {
            if (c == null) continue;
            Assert.IsFalse(식물이름.Contains(DataText.Name(c)),
                $"개체 관측에 식물과 같은 이름의 줄이 있다: {c.id}");
        }
    }

    // ── 훑개 ────────────────────────────────────────────────────

    static List<string> 계층명이_섞인_자리(IEnumerable<(string 자리, string 글)> 글들)
    {
        var 걸린것 = new List<string>();
        foreach (var (자리, 글) in 글들)
        {
            if (string.IsNullOrEmpty(글)) continue;
            foreach (var 말 in 계층어)
                if (글.IndexOf(말, StringComparison.OrdinalIgnoreCase) >= 0)
                    걸린것.Add($"{자리}: \"{글.Replace("\n", " / ")}\" ← {말}");
        }
        return 걸린것;
    }

    /// <summary>
    /// 도감이 지을 수 있는 말 전부. 실제 목록 에셋을 쓰고, <b>잠근 채</b>와
    /// <b>다 아는 채</b> 둘 다, <b>두 로케일</b> 모두 짓는다 — 계층명이 한 갈래나
    /// 한 언어에만 남아 있는 것이 제일 흔한 빠뜨림이다.
    /// </summary>
    static List<(string 자리, string 글)> 도감이_내놓는_모든_문자열()
    {
        var discoveries = Resources.Load<DiscoveryBookSO>("DiscoveryBook");
        var research = Resources.Load<ResearchBookSO>("ResearchBook");
        var creatures = Resources.Load<CreatureBookSO>("CreatureBook");
        var plants = Resources.Load<PlantBookSO>(CodexUIPlantBookName);

        Assert.IsNotNull(discoveries, "발견 목록을 못 읽었다");
        Assert.IsNotNull(research, "연구 목록을 못 읽었다");
        Assert.IsNotNull(creatures, "생물 목록을 못 읽었다");
        Assert.IsNotNull(plants, "식물 목록을 못 읽었다 — 탭이 통째로 비어 검사가 헛돈다");

        var 모은것 = new List<(string, string)>();
        var 줄 = new List<CodexEntry>();
        var 처음로케일 = Loc.CurrentLocale;

        try
        {
            foreach (var locale in new[] { StringCatalog.DefaultLocale, "en" })
            {
                Loc.SetLocale(locale);

                foreach (CodexSection s in Enum.GetValues(typeof(CodexSection)))
                    모은것.Add(($"{locale} 갈래 이름", CodexCatalog.SectionTitle(s)));

                모은것.Add(($"{locale} 잠긴 물질", CodexCatalog.UnknownDiscoveryBody));
                모은것.Add(($"{locale} 잠긴 청사진", CodexCatalog.UnknownBlueprintBody));
                모은것.Add(($"{locale} 잠긴 연구", CodexCatalog.UnknownResearchBody));
                모은것.Add(($"{locale} 잠긴 개체", CodexCatalog.UnknownCreatureBody));
                모은것.Add(($"{locale} 잠긴 식물", CodexCatalog.UnknownPlantBody));
                모은것.Add(($"{locale} 빈 식물 탭", MenuListing.CodexEmptyBody(CodexSection.Plant)));

                foreach (var (원장이름, 원장) in new[]
                         {
                             ("잠근 채", new UnlockLedger()),
                             ("다 아는 채", 다_아는_원장(discoveries, research, creatures, plants)),
                         })
                {
                    CodexCatalog.BuildDiscoveries(discoveries, 원장, 줄);
                    담는다($"{locale} 물질 분석 · {원장이름}", 줄, 모은것);

                    CodexCatalog.BuildBlueprints(discoveries, research, 원장, 줄);
                    담는다($"{locale} 확보 제작법 · {원장이름}", 줄, 모은것);

                    CodexCatalog.BuildResearch(research, 원장, 줄);
                    담는다($"{locale} 정밀 분석 · {원장이름}", 줄, 모은것);

                    CodexCatalog.BuildCreatures(creatures, 원장, 줄);
                    담는다($"{locale} 개체 관측 · {원장이름}", 줄, 모은것);

                    CodexCatalog.BuildPlants(plants, 원장, 줄);
                    담는다($"{locale} 식물 관찰 · {원장이름}", 줄, 모은것);
                }

                // 관측 보고는 화면을 거치지 않고도 지을 수 있다. 목록을 거치는 길이
                // 언젠가 바뀌어도 보고 자체는 계속 검사되게 따로 한 번 더 짓는다.
                foreach (var c in creatures.creatures)
                    if (c != null)
                        모은것.Add(($"{locale} 관측 보고 {c.id}", CodexCatalog.DescribeCreature(c)));

                foreach (var p in plants.plants)
                    if (p != null)
                        모은것.Add(($"{locale} 관찰 기록 {p.name}", CodexCatalog.DescribePlant(p)));
            }
        }
        finally { Loc.SetLocale(처음로케일); }

        return 모은것.Select(t => (자리: t.Item1, 글: t.Item2)).ToList();
    }

    static void 담는다(string 자리, List<CodexEntry> 줄, List<(string, string)> 모은것)
    {
        foreach (var r in 줄)
        {
            if (r == null) continue;
            모은것.Add(($"{자리} [{r.Key}] 제목", r.Title));
            모은것.Add(($"{자리} [{r.Key}] 본문", r.Body));
        }
    }

    /// <summary>
    /// 세상의 모든 열쇠를 연 원장. 잠긴 줄만 보면 계층명이 숨을 자리가 남는다 —
    /// 계층은 <b>열린 줄</b>에만 적히던 것이다.
    /// </summary>
    /// <summary>도감이 식물 목록을 찾는 이름. 화면 쪽 상수와 같아야 한다.</summary>
    const string CodexUIPlantBookName = "PlantBook";

    static UnlockLedger 다_아는_원장(DiscoveryBookSO discoveries, ResearchBookSO research,
                                    CreatureBookSO creatures, PlantBookSO plants)
    {
        var ledger = new UnlockLedger();

        foreach (var d in discoveries.discoveries ?? new DiscoverySO[0])
        {
            if (d == null) continue;
            var key = FieldDiscovery.KeyOf(d);
            if (key != null) ledger.Unlock(key);
            foreach (var bp in d.unlocks ?? new BlueprintSO[0])
                if (bp != null) ledger.Unlock(bp.id);
        }

        foreach (var e in research.entries ?? new ResearchEntrySO[0])
        {
            if (e == null) continue;
            foreach (var bp in e.unlocks ?? new BlueprintSO[0])
                if (bp != null) ledger.Unlock(bp.id);
        }

        foreach (var c in creatures.creatures ?? new CreatureDefinitionSO[0])
        {
            var key = CodexCatalog.CreatureKey(c);
            if (key != null) ledger.Unlock(key);
        }

        foreach (var p in plants.plants ?? new PlantNodeSO[0])
        {
            var key = CodexCatalog.PlantKey(p);
            if (key != null) ledger.Unlock(key);
        }

        return ledger;
    }
}
