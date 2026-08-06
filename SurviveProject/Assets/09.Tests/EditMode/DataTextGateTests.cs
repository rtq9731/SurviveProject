using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Localization;

/// <summary>
/// 데이터 에셋 번역의 게이트. <b>이 파일이 이 라운드의 핵심 산출물이다.</b>
///
/// 직전 라운드가 남긴 사각지대를 메운다. 소스 스캐너(<c>LocSourceScanner</c>)는
/// <c>Loc.T("UI","craft_empty")</c>처럼 <b>리터럴</b>로 적힌 호출만 볼 수 있다.
/// 데이터 에셋 경로는 <c>Loc.T("Item", item.id + ".name")</c> 꼴이라 코드만 봐서는
/// 어떤 키가 필요한지 알 수 없다 — 그래서 여기서는 <b>에셋을 실제로 연다</b>.
///
/// 아이템을 하나 새로 만들고 번역 키를 안 만들면 다음 EditMode에서 즉시 빨개진다.
/// 그것이 이 게이트를 세운 유일한 이유다.
/// </summary>
public class DataTextGateTests
{
    static List<DataTextEntry> _entries;
    static StringCatalog _catalog;
    static string _csv;

    [OneTimeSetUp]
    public void 에셋과_표를_한_번씩_읽는다()
    {
        _entries = DataTextAssets.CollectAll();
        _csv = File.ReadAllText(LocalizationTestBootstrap.CsvPath);
        _catalog = StringCatalog.Parse(_csv);

        // 표를 실어 둔다. 아래 폴백 검사들이 진짜 조회 경로를 그대로 밟게.
        Loc.Load(_catalog);
        Loc.SetLocale(StringCatalog.DefaultLocale);
    }

    [OneTimeTearDown]
    public void 표를_원래대로_되돌린다()
    {
        Loc.Load(LocalizationTestBootstrap.LoadCatalogFromDisk());
        Loc.SetLocale(StringCatalog.DefaultLocale);
    }

    // ── ① 카탈로그 대조 ──────────────────────────────────────

    [Test]
    public void 에셋의_모든_글자에_대응하는_키가_표에_있다()
    {
        var missing = _entries
            .Where(e => !_catalog.Contains(e.AsKey()))
            .Select(e => $"{e}  (\"{e.Korean}\" — {AssetDatabase.GetAssetPath(e.Owner)})")
            .ToList();

        Assert.IsEmpty(missing,
            $"에셋에 적힌 글자 {missing.Count}칸이 번역 표에 없다. " +
            "메뉴 Tools/Survive/번역/데이터 에셋 초안 갱신 을 돌려라:\n  " +
            string.Join("\n  ", missing));
    }

    [Test]
    public void 긁힌_에셋이_실제로_있다()
    {
        // 긁는 쪽이 조용히 아무것도 못 찾게 되면 위 검사가 공회전한다.
        // 숫자는 "줄어들면 알아채라"는 뜻이지 정확한 개수의 계약이 아니다.
        Assert.Greater(DataTextAssets.FindAll().Count, 90, "데이터 에셋을 못 찾았다");
        Assert.Greater(_entries.Count, 120,
            $"번역 대상 칸을 {_entries.Count}개밖에 못 긁었다 — 게이트가 아무것도 막지 못한다");
    }

    [Test]
    public void 표에만_있고_에셋이_없어진_키는_경고로만_남긴다()
    {
        var fromAssets = new HashSet<LocKey>(_entries.Select(e => e.AsKey()));
        var 데이터카테고리 = new HashSet<string>(_entries.Select(e => e.Category));

        // UI 계열은 손으로 짓는 키라 여기서 판정할 수 없다. 데이터 카테고리만 본다.
        var orphans = _catalog.Keys
            .Where(k => 데이터카테고리.Contains(k.Category) && !fromAssets.Contains(k))
            .ToList();

        if (orphans.Count > 0)
            Debug.LogWarning($"[데이터 번역 게이트] 어느 에셋도 만들지 않는 키 {orphans.Count}개 " +
                             "(에셋을 지웠다면 표에서도 지워라):\n  " +
                             string.Join("\n  ", orphans.Select(k => k.ToString())));

        Assert.Pass($"에셋에서 {_entries.Count}칸, 표에 {_catalog.Keys.Count}줄, " +
                    $"주인 없는 키 {orphans.Count}개");
    }

    [Test]
    public void ko_칸이_빈_줄이_없다()
    {
        // StringCatalog가 빈 기본 로케일 칸을 Problems에 적는다. 데이터 구역만 골라 낸다.
        var empty = _catalog.Problems
            .Where(p => p.Contains($"기본 로케일({StringCatalog.DefaultLocale}) 칸이 비었다"))
            .ToList();

        Assert.IsEmpty(empty, "ko 칸이 비면 화면에 키가 그대로 뜬다:\n  " + string.Join("\n  ", empty));
    }

    [Test]
    public void 같은_키를_두_에셋이_만들지_않는다()
    {
        // id가 겹치면 한쪽의 번역이 다른 쪽에 뜬다. Recipe와 Item은 id가 겹쳐도
        // 카테고리가 달라 괜찮지만, 같은 카테고리 안에서는 사고다.
        var dup = _entries
            .GroupBy(e => e.AsKey())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key + " <- " +
                         string.Join(", ", g.Select(e => AssetDatabase.GetAssetPath(e.Owner))))
            .ToList();

        Assert.IsEmpty(dup, "두 에셋이 같은 키를 만든다:\n  " + string.Join("\n  ", dup));
    }

    [Test]
    public void 자동_생성_키도_이름표_규약을_지킨다()
    {
        var category = new Regex(@"^[A-Z][A-Za-z0-9]*$");
        var key = new Regex(@"^[a-z0-9]+([._][a-z0-9]+)*$");

        foreach (var e in _entries)
        {
            Assert.IsTrue(category.IsMatch(e.Category),
                $"Category가 규약을 벗어났다: {e} ({AssetDatabase.GetAssetPath(e.Owner)})");
            Assert.IsTrue(key.IsMatch(e.Key),
                $"Key가 규약을 벗어났다: {e} — 에셋의 id가 소문자 스네이크가 아니다 " +
                $"({AssetDatabase.GetAssetPath(e.Owner)})");
        }
    }

    // ── ② 조회 경로: 표가 이기고, 없으면 원문 ────────────────

    [Test]
    public void 표에_있으면_표가_이긴다()
    {
        int 확인 = 0;
        foreach (var e in _entries)
        {
            if (!_catalog.TryGet(StringCatalog.DefaultLocale, e.AsKey(), out var tableValue)) continue;
            Assert.AreEqual(tableValue, e.Resolve(),
                $"{e}의 화면 값이 표가 아니라 다른 데서 나왔다 " +
                $"({AssetDatabase.GetAssetPath(e.Owner)})");
            확인++;
        }

        Assert.Greater(확인, 120, "표와 대조한 칸이 너무 적다 — 검사가 공회전한다");
    }

    [Test]
    public void 다른_로케일_칸이_차_있으면_그것이_이긴다()
    {
        var 영어가_있는_키 = _entries
            .Where(e => _catalog.TryGet("en", e.AsKey(), out _))
            .ToList();

        Assert.IsNotEmpty(영어가_있는_키,
            "en 칸이 하나도 안 차 있으면 로케일 전환을 검사할 수 없다 — 표에 두어 개는 넣어 둬라");

        Loc.SetLocale("en");
        try
        {
            foreach (var e in 영어가_있는_키)
            {
                _catalog.TryGet("en", e.AsKey(), out var english);
                Assert.AreEqual(english, e.Resolve(), $"{e}가 en 칸을 따라오지 않는다");
                Assert.AreNotEqual(e.Korean, e.Resolve(), $"{e}가 로케일을 바꿔도 한국어 그대로다");
            }
        }
        finally { Loc.SetLocale(StringCatalog.DefaultLocale); }
    }

    [Test]
    public void 표에_없으면_에셋의_원문이_나온다()
    {
        // 표를 통째로 비우고 조회한다. 배포본에서 CSV가 빠졌을 때의 모습이다 —
        // 그때도 게임은 한국어로 돌아가야 한다.
        Loc.Load(StringCatalog.Empty);
        try
        {
            foreach (var e in _entries)
                Assert.AreEqual(e.AssetKorean, e.Resolve(),
                    $"{e}: 표가 없을 때 에셋 원문이 나오지 않는다 " +
                    $"({AssetDatabase.GetAssetPath(e.Owner)})");
        }
        finally
        {
            Loc.Load(_catalog);
            Loc.SetLocale(StringCatalog.DefaultLocale);
        }
    }

    [Test]
    public void 표가_없어도_빈_글자는_내지_않는다()
    {
        Loc.Load(StringCatalog.Empty);
        try
        {
            foreach (var e in _entries)
                Assert.IsNotEmpty(e.Resolve(), $"{e}가 빈 글자를 냈다 — 화면이 빈다");
        }
        finally
        {
            Loc.Load(_catalog);
            Loc.SetLocale(StringCatalog.DefaultLocale);
        }
    }

    [Test]
    public void 없는_에셋을_물어도_예외가_나지_않는다()
    {
        Assert.AreEqual("", DataText.Name((Survive.Items.ItemDataSO)null));
        Assert.AreEqual("", DataText.Desc((Survive.Items.ItemDataSO)null));
        Assert.AreEqual("", DataText.Hint((Survive.Progression.BlueprintSO)null));
        Assert.AreEqual("", DataText.Line((Survive.Progression.DiscoverySO)null));
        Assert.AreEqual("", DataText.Text((Survive.Progression.ObjectiveSO)null));
        Assert.AreEqual("", DataText.Line((Survive.Narrative.SequenceSO)null, 0));
    }

    [Test]
    public void id가_비면_표를_뒤지지_않고_원문을_낸다()
    {
        var item = ScriptableObject.CreateInstance<Survive.Items.ItemDataSO>();
        item.id = "";
        item.displayName = "이름 없는 것";

        // 키를 만들 수 없으면 표를 뒤질 방법도 없다. 그래도 화면은 살아 있어야 한다.
        Assert.IsTrue(DataText.NameKey(item).IsEmpty);
        Assert.AreEqual("이름 없는 것", DataText.Name(item));

        Object.DestroyImmediate(item);
    }

    // ── ③ 우회하는 자리가 없다 (정적 검사) ────────────────────

    /// <summary>
    /// 접근자를 거치지 않고 SO의 글자 필드를 직접 읽는 자리. <b>여기 없는 것이 나오면 실패다.</b>
    ///
    /// 이 네 파일은 지금 다른 작업이 진행 중이라 이번 라운드에서 손대지 않았다.
    /// 목록이 비는 날 이 검사는 "우회가 하나도 없다"가 된다.
    /// </summary>
    static readonly string[] 아직_옮기지_못한_파일 =
    {
        "Assets/02.Scripts/Domain/UI/MenuListing.cs",
        "Assets/02.Scripts/Domain/UI/ItemTooltipContent.cs",
        "Assets/02.Scripts/UI/CraftQueueView.cs",
        "Assets/02.Scripts/UI/ObjectiveListView.cs",
    };

    /// <summary>글자 필드를 직접 읽는 꼴. 대입(<c>= 값</c>)은 읽기가 아니라 세우기다.</summary>
    static readonly Regex 직접읽기 = new Regex(
        @"\.(displayName|description|hint|codexDescription|displayText)\b(?!\s*=[^=])");

    [Test]
    public void 접근자를_우회해_SO를_직접_읽는_곳이_없다()
    {
        var root = Path.Combine(Application.dataPath, "02.Scripts");
        var 위반 = new List<string>();
        var 허용했는데_없는_파일 = new List<string>(아직_옮기지_못한_파일);
        int 검사한파일 = 0;

        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Relative(path);

            // 필드를 선언하고 초기화하는 SO 자신과, 이번 라운드의 접근자는 당연히 읽는다.
            if (relative.StartsWith("Assets/02.Scripts/Domain/Localization/")) continue;
            if (relative.EndsWith("SO.cs")) continue;
            if (relative.EndsWith("Objective.cs")) continue;

            // 하네스는 "화면에 뜬 글자가 에셋의 값과 같은가"를 물어야 하므로 원문을 읽는다.
            if (relative.StartsWith("Assets/02.Scripts/Testing/")) continue;

            검사한파일++;

            bool 허용 = 허용했는데_없는_파일.Remove(relative);

            var text = LocSourceScanner.StripComments(File.ReadAllText(path));
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (!직접읽기.IsMatch(lines[i])) continue;
                if (허용) continue;
                위반.Add($"{relative}:{i + 1}  {lines[i].Trim()}");
            }
        }

        Assert.Greater(검사한파일, 100, "코드를 못 찾았다 — 검사가 공회전한다");

        Assert.IsEmpty(위반,
            "데이터 에셋의 글자를 DataText를 거치지 않고 직접 읽는 자리가 있다. " +
            "그 화면만 번역이 안 되고, 그 화면을 그 로케일로 열어 보기 전까지 아무 신호도 없다:\n  " +
            string.Join("\n  ", 위반));

        Assert.IsEmpty(허용했는데_없는_파일,
            "예외 목록에 이제 없는 파일이 남아 있다. 목록을 줄여라:\n  " +
            string.Join("\n  ", 허용했는데_없는_파일));
    }

    static string Relative(string full) =>
        "Assets" + full.Substring(Application.dataPath.Length).Replace('\\', '/');
}
