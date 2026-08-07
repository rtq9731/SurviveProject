using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Creatures;
using Survive.Harvesting;
using Survive.Localization;

/// <summary>
/// <b>「식물」의 경계는 형(type)이다</b> — 이름도, "움직이지 않는다"도 아니다
/// (2026-08-07 판단).
///
/// <b>물음.</b> 도감 식물 탭에 서는 것이 둘뿐이다(재양치·불씨버섯). 무광버섯과
/// 발광 버섯은 <see cref="HarvestNodeSO"/>라 그 탭에 없다. 옮겨야 하는가.
///
/// <b>설계의 잣대를 문자 그대로 재 보면 답이 안 나온다.</b> 기획서는 <i>"식물은
/// 움직이지 않고 위협이 아니므로 계열이 명백하다"</i>고 적었는데, 그 잣대로 재면
/// <b>흩어진 잔해와 합금 더미까지 식물</b>이다 — 그것들도 움직이지 않고 위협이
/// 아니다. 그 문장은 <b>소속 기준</b>이 아니라 <b>탭을 세워도 되는 이유</b>다.
/// 명백한 것을 감추는 데는 얻을 것이 없다는 말이지, 무엇이 그 탭에 서는지를
/// 정하는 말이 아니다. 실제로 그 잣대가 가르는 것은 <b>생물과 그 밖</b>이고,
/// 아래 <see cref="움직임과_위협은_생물과_그_밖을_가른다"/>가 그것을 확인한다.
///
/// <b>그러면 식물과 채집물을 가르는 성질은 무엇인가 — 자라는가다.</b> 그리고 그
/// 성질은 <b>이미 형이 들고 있다.</b> <see cref="PlantNodeSO"/>에는 성장 단계·자라는
/// 시간·시드는 시간·먹였을 때의 영양이 있고, <see cref="HarvestNodeSO"/>에는 그
/// 넷이 하나도 없다(대신 내구도와 통째 재생 시간이 있다). 형이 곧 성질이므로
/// <b>형으로 가르는 것과 성질로 가르는 것이 같은 답을 낸다.</b>
///
/// <b>그래서 옮기지 않았다.</b> 근거 넷.
/// <list type="number">
/// <item><b>옮길 데이터가 없다.</b> 무광버섯을 식물로 만들려면 성장 단계·자라는
///       시간·시드는 시간·영양을 <b>지어내야</b> 한다. 없는 값을 지어내는 것은
///       분류가 아니라 설계이고, 그 설계는 사람의 몫이다.</item>
/// <item><b>채집 방식이 통째로 바뀐다.</b> 무광버섯은 <c>requiredTool: None</c>이라
///       도구 요건은 그대로지만, 150초 뒤 <b>통째로 되살아나는 것</b>과 단계로
///       <b>자라는 것</b>은 다른 규칙이다. 부품도 <c>HarvestNode</c>에서
///       <c>PlantNode</c>로 갈아야 하고, 그것은 프리팹 수정이다.</item>
/// <item><b>화면이 달라지지 않는다.</b> 2026-08-07 실측 — 씬의 채집물 40개는
///       흩어진 잔해 18 · 기계 잔해 9 · 합금 더미 13이 전부다. 무광버섯과
///       매크로늄 석영은 프리팹만 있고 <b>인스턴스가 0개</b>, 발광 버섯은
///       프리팹조차 없다. 도감 탭을 고쳐도 세계에 캘 것이 없다.</item>
/// <item><b>이름표를 뚫지 않는다.</b> ⑩ 라운드가 계층명을 통째로 걷어냈고, 식물 탭
///       라운드는 <c>ItemDataSO</c>에 식물 칸을 일부러 안 뚫었다 — 그 칸이 곧
///       이름표가 되어 ⑩을 되돌리는 통로가 되기 때문이다. 형으로 가르면 그 통로가
///       열리지 않는다. 어떤 에셋에도 "나는 식물이다"라고 적힌 칸이 없다.</item>
/// </list>
///
/// <b>그러면 언제 옮기는가.</b> <see cref="채집물에는_성장_칸이_하나도_없다"/>가
/// 그 날을 잡는다. 누군가 <see cref="HarvestNodeSO"/>에 성장 칸을 더하는 순간
/// 이 검사가 빨개지고, 그것이 곧 "이 정의는 이제 식물이다"라는 신호다.
/// <b>특례 목록이 아니라 한 규칙이다</b> — 어떤 에셋의 이름도 여기 적혀 있지 않다.
/// </summary>
public class PlantBoundaryTests
{
    /// <summary>
    /// 자라는 것만 갖는 칸. <b>이 넷이 「식물인가」를 답하는 유일한 자</b>다.
    /// 값이 아니라 <b>칸이 있는가</b>를 본다 — 값으로 재면 <c>growSeconds = 0</c>인
    /// 식물 하나가 갈래를 잃는다.
    /// </summary>
    static readonly string[] 성장의_칸 =
    {
        nameof(PlantNodeSO.maxStage),
        nameof(PlantNodeSO.growSeconds),
        nameof(PlantNodeSO.witherSeconds),
        nameof(PlantNodeSO.nutritionPerStage),
    };

    const string 식물목록에셋 = "Assets/08.Data/Plants/Resources/PlantBook.asset";

    string _처음로케일;

    [SetUp]
    public void 로케일을_기억해_둔다() => _처음로케일 = Loc.CurrentLocale;

    [TearDown]
    public void 로케일을_되돌린다() => Loc.SetLocale(_처음로케일 ?? StringCatalog.DefaultLocale);

    // ── ① 경계는 한 규칙이다 ────────────────────────────────────

    [Test]
    public void 식물_정의만_성장의_칸을_갖는다()
    {
        var 있는것 = 칸이름(typeof(PlantNodeSO));

        var 빠진것 = 성장의_칸.Where(f => !있는것.Contains(f)).ToList();
        Assert.IsEmpty(빠진것,
            "식물 정의에서 성장의 칸이 사라졌다. 「식물인가」를 잴 자가 없어진다:\n  " +
            string.Join("\n  ", 빠진것));
    }

    /// <summary>
    /// <b>이 검사가 이 파일의 알맹이다.</b> 채집물에 성장 칸이 생기는 날이 곧
    /// 옮길 날이다 — 그때 이 검사가 빨개져 그 사실을 알린다.
    /// </summary>
    [Test]
    public void 채집물에는_성장_칸이_하나도_없다()
    {
        var 있는것 = 칸이름(typeof(HarvestNodeSO));
        var 섞인것 = 성장의_칸.Where(있는것.Contains).ToList();

        Assert.IsEmpty(섞인것,
            $"채집물 정의가 성장의 칸을 갖게 되었다 ({string.Join(", ", 섞인것)}). " +
            "자라는 것은 식물이다 — 그 정의는 PlantNodeSO로 옮기고 " +
            "PlantBook에 실어라. 지금은 도감 식물 탭이 그 정의를 못 본다.");
    }

    /// <summary>
    /// <b>설계의 잣대가 실제로 가르는 것.</b> "움직이지 않고 위협이 아니다"를 데이터로
    /// 재면 채집물과 식물은 <b>같은 쪽</b>에 선다 — 둘 다 이동·공격 칸이 없다.
    /// 그 잣대로는 무광버섯을 식물 탭으로 옮길 근거가 서지 않는다는 뜻이다.
    /// </summary>
    [Test]
    public void 움직임과_위협은_생물과_그_밖을_가른다()
    {
        var 움직임과_위협 = new[]
        {
            nameof(CreatureDefinitionSO.moveSpeed),
            nameof(CreatureDefinitionSO.attackDamage),
        };

        var 생물칸 = 칸이름(typeof(CreatureDefinitionSO));
        foreach (var f in 움직임과_위협)
            Assert.IsTrue(생물칸.Contains(f), $"생물 정의에 {f}가 없다 — 잣대가 헛돈다");

        foreach (var 형 in new[] { typeof(PlantNodeSO), typeof(HarvestNodeSO) })
        {
            var 칸 = 칸이름(형);
            foreach (var f in 움직임과_위협)
                Assert.IsFalse(칸.Contains(f),
                    $"{형.Name}에 {f}가 있다. 「움직이지 않는다」가 채집물과 식물을 " +
                    "가르는 잣대가 아니라는 이 검사의 전제가 깨졌다");
        }
    }

    /// <summary>
    /// <b>이름은 소속을 정하지 않는다.</b> 표의 식물 이름과 채집물 이름에 같은 낱말이
    /// 걸쳐 있다(버섯). 이름으로 가르는 규칙을 누군가 세우려 하면 여기서 막힌다 —
    /// 특정 에셋을 적지 않고 <b>표를 세어서</b> 판정한다.
    /// </summary>
    [Test]
    public void 이름에_같은_낱말이_걸쳐_있어_이름으로는_못_가른다()
    {
        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk()
                                          .TableFor(StringCatalog.DefaultLocale);

        var 식물이름 = 표.Where(p => p.Key.Category == DataText.Category.Plant)
                        .Select(p => p.Value).ToList();
        var 채집이름 = 표.Where(p => p.Key.Category == DataText.Category.Harvest)
                        .Select(p => p.Value).ToList();

        Assert.IsNotEmpty(식물이름, "표에서 식물 이름을 못 찾았다 — 검사가 헛돈다");
        Assert.IsNotEmpty(채집이름, "표에서 채집물 이름을 못 찾았다 — 검사가 헛돈다");

        string 겹치는말 = "버" + "섯";
        Assert.IsTrue(식물이름.Any(n => n.Contains(겹치는말)) &&
                      채집이름.Any(n => n.Contains(겹치는말)),
            $"「{겹치는말}」이 한쪽에만 남았다. 이름이 갈래를 정하지 않는다는 근거가 " +
            "약해졌으니, 경계를 무엇으로 재는지 이 파일의 문서 주석을 다시 읽어라");
    }

    // ── ② 갈래가 소속으로 빠짐없이 이어진다 ─────────────────────

    /// <summary>
    /// <b>형이 곧 소속이다.</b> <c>08.Data</c>의 모든 식물 정의가 식물 목록에 실려
    /// 있어야 한다. 실리지 않으면 도감 탭이 그 식물을 못 보고, 그것은 화면에
    /// 아무 신호도 내지 않는다 — 없는 줄과 안 실은 줄은 똑같이 안 보인다.
    /// </summary>
    [Test]
    public void 모든_식물_정의가_식물_목록에_실려_있다()
    {
        var 디스크의식물 = 모든_식물_정의();
        Assert.IsNotEmpty(디스크의식물, "식물 정의를 하나도 못 찾았다 — 검사가 헛돈다");

        var 책 = AssetDatabase.LoadAssetAtPath<PlantBookSO>(식물목록에셋);
        Assert.IsNotNull(책, $"{식물목록에셋}를 못 읽었다");
        Assert.IsNotNull(책.plants, "식물 목록이 null이다");

        var 실린것 = new HashSet<PlantNodeSO>(책.plants.Where(p => p != null));

        var 빠진것 = 디스크의식물.Where(p => !실린것.Contains(p))
                                .Select(AssetDatabase.GetAssetPath)
                                .ToList();

        Assert.IsEmpty(빠진것,
            $"식물 정의 {빠진것.Count}개가 식물 목록에 없다. 형이 곧 소속이다 — " +
            $"{식물목록에셋}에 실어라:\n  " + string.Join("\n  ", 빠진것));
    }

    [Test]
    public void 식물_목록에_빈_칸이나_겹치는_줄이_없다()
    {
        var 책 = AssetDatabase.LoadAssetAtPath<PlantBookSO>(식물목록에셋);
        Assert.IsNotNull(책, $"{식물목록에셋}를 못 읽었다");

        Assert.IsFalse(책.plants.Any(p => p == null),
            "식물 목록에 빈 칸이 있다 — 도감에서 조용히 사라지는 줄이 된다");

        var 겹친것 = 책.plants.GroupBy(p => p).Where(g => g.Count() > 1)
                             .Select(g => AssetDatabase.GetAssetPath(g.Key)).ToList();
        Assert.IsEmpty(겹친것, "같은 식물이 두 번 실려 있다:\n  " + string.Join("\n  ", 겹친것));
    }

    // ── ③ 이 판단이 이름표를 되들이지 않았다 ────────────────────

    /// <summary>
    /// ⑩이 지운 계층명이 <b>채집물·식물 이름 쪽에서</b> 되살아나지 않았는지 본다.
    /// <c>CodexUnclassifiedGateTests</c>는 도감이 <b>짓는 말</b>을 훑는데, 그쪽은
    /// 식물 탭만 지나간다 — 채집물 이름은 도감을 안 거치므로 거기 안 걸린다.
    /// </summary>
    [Test]
    public void 채집물과_식물_이름에_계층명이_없다()
    {
        var 계층어 = new[]
        {
            "분해" + "자", "생산" + "자", "소비" + "자", "미분" + "류", "영양 " + "단계",
            "decompo" + "ser", "produ" + "cer", "consu" + "mer", "unclassi" + "fied", "troph" + "ic",
        };

        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk();
        var 걸린것 = new List<string>();

        foreach (var locale in 표.Locales)
            foreach (var pair in 표.TableFor(locale))
            {
                if (pair.Key.Category != DataText.Category.Plant &&
                    pair.Key.Category != DataText.Category.Harvest) continue;

                foreach (var 말 in 계층어)
                    if (pair.Value.IndexOf(말, StringComparison.OrdinalIgnoreCase) >= 0)
                        걸린것.Add($"{locale} {pair.Key}: \"{pair.Value}\" <- {말}");
            }

        Assert.IsEmpty(걸린것,
            $"채집물·식물 이름에 계층명이 있다 ({걸린것.Count}군데). " +
            "AI는 관찰한 것만 기록하고 무엇인지는 판정하지 않는다 (기획서 §4.7):\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary>
    /// <b>어떤 에셋에도 「나는 식물이다」라고 적힌 칸이 없다.</b> 형으로 가르기로
    /// 한 결정의 알맹이가 이것이다 — 그런 칸이 생기는 순간 그 칸이 이름표가 되고,
    /// 이름표는 화면으로 새어 나간다. 식물 탭 라운드가 <c>ItemDataSO</c>에 식물 칸을
    /// 일부러 안 뚫은 이유가 그것이다.
    ///
    /// <b>기존 <c>ItemDataSO.category</c>는 여기 걸리지 않는다.</b> 그것은 소지품을
    /// 정렬하는 칸(자원·도구·소모품·임무)이고 생태 갈래가 아니다. 문제가 되는 것은
    /// <b>식물이라고 적는 칸</b>이므로, 그 뜻이 담긴 이름만 본다.
    /// </summary>
    [Test]
    public void 식물이라고_적어_두는_칸이_어디에도_없다()
    {
        var 걸린것 = new List<string>();

        foreach (var 형 in new[]
                 {
                     typeof(PlantNodeSO), typeof(HarvestNodeSO), typeof(Survive.Items.ItemDataSO),
                 })
            foreach (var f in 형.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                // 목록을 담는 칸(PlantBookSO.plants)이 아니라 <b>한 에셋이 자기가
                // 식물임을 적는 칸</b>이 문제다. 그래서 목록형은 빼고 본다.
                if (f.FieldType.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(f.FieldType))
                    continue;
                if (f.Name.IndexOf("plant", StringComparison.OrdinalIgnoreCase) >= 0)
                    걸린것.Add($"{형.Name}.{f.Name}");
            }

        // 정렬용 갈래에 식물이 끼어드는 것도 같은 통로다.
        foreach (var name in Enum.GetNames(typeof(Survive.Items.ItemCategory)))
            if (name.IndexOf("plant", StringComparison.OrdinalIgnoreCase) >= 0)
                걸린것.Add($"ItemCategory.{name}");

        Assert.IsEmpty(걸린것,
            "에셋이 자기가 식물임을 적는 칸이 생겼다. 그 칸은 곧 이름표가 되고 " +
            "⑩이 걷어낸 계층명을 되돌리는 통로가 된다. 갈래는 형이 정한다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    // ── 훑개 ────────────────────────────────────────────────────

    static HashSet<string> 칸이름(Type 형) =>
        new HashSet<string>(형.GetFields(BindingFlags.Public | BindingFlags.Instance)
                             .Select(f => f.Name));

    static List<PlantNodeSO> 모든_식물_정의() =>
        AssetDatabase.FindAssets("t:" + nameof(PlantNodeSO))
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<PlantNodeSO>)
            .Where(p => p != null)
            .ToList();
}
