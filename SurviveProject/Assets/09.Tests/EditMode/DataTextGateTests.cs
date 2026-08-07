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
    /// <b>비었다.</b> 네 화면 파일(MenuListing·ItemTooltipContent·CraftQueueView·
    /// ObjectiveListView)이 전부 <see cref="DataText"/>를 거치게 됐다.
    /// 이제 이 검사는 그냥 "우회가 하나도 없다"이다 — 새 우회가 생기면 즉시 빨개진다.
    /// </summary>
    static readonly string[] 아직_옮기지_못한_파일 = { };

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

    // ── ④ 대사 한 줄도 우회하지 않는다 ────────────────────────
    //
    // 위 ③은 이름·설명 칸(displayName·description…)만 봤다. 그런데 화면에 나가는
    // 글자가 하나 더 있다 — <c>SequenceSO.Line</c>의 <c>.text</c>와 <c>.speaker</c>다.
    // 연출 자막과 AI 대사가 그것이고, <c>DataText.Line/Speaker</c>가 이미 있는데도
    // <b>프로덕션 호출자가 하나도 없었다</b>(2026-08-07 발견).
    //
    // 자료형으로 가릴 수 없어 <b>꼴</b>로 잡는다. <c>.text</c>만으로는 못 잡는다 —
    // <c>TMP_Text.text</c>가 화면 코드 전체에 널려 있고, 그것까지 물면 게이트가
    // 오탐 더미가 되어 결국 꺼진다. 그래서 대사 줄이 실제로 나타나는 세 꼴만 본다.

    /// <summary>
    /// 대사 한 줄을 <see cref="DataText"/>를 거치지 않고 읽는 파일. <b>여기 없는 것이
    /// 나오면 실패다.</b>
    ///
    /// <b>적는 순간 그것은 빚이다.</b> 지금 한 줄뿐이고, 그 한 줄은 이 라운드의
    /// 파일 범위 밖이라 손대지 않았다.
    /// </summary>
    static readonly string[] 대사를_아직_안_거친_파일 =
    {
        // UnlockService.Announce(SequenceSO.Line)이 대사 줄을 <b>날것으로</b> 받아
        // 줄을 세운다. 그래서 현장 발견·연구 대사가 표를 거치지 않고 화면에 나간다 —
        // 프롤로그 자막과 같은 구멍이고, 지금은 표와 에셋의 값이 같아 티가 안 난다.
        //
        // 고치려면 큐가 줄이 아니라 <b>줄의 주인</b>(DiscoverySO·ResearchEntrySO)을
        // 들어야 하고, 그러면 Progression/의 호출자 셋(UnlockService.OnItemAdded ·
        // LocationDiscoveryTrigger · ResearchStation)이 함께 움직인다.
        "Assets/02.Scripts/Progression/UnlockService.cs",
    };

    /// <summary>
    /// 대사 줄을 직접 읽는 꼴. 왼쪽부터 <c>x.line.text</c> · <c>x.lines[i].speaker</c> ·
    /// <c>line.text</c>(지역 변수). 대입(<c>= 값</c>)은 읽기가 아니라 세우기다.
    /// </summary>
    static readonly Regex 대사직접읽기 = new Regex(
        @"(?:\.\s*line\s*\.|\.\s*lines\s*\[[^\]]*\]\s*\.|\bline\s*\.)\s*(?:text|speaker)\b(?!\s*=[^=])");

    [Test]
    public void 대사_한_줄을_직접_읽는_곳이_없다()
    {
        var root = Path.Combine(Application.dataPath, "02.Scripts");
        var 위반 = new List<string>();
        var 허용했는데_없는_파일 = new List<string>(대사를_아직_안_거친_파일);
        int 검사한파일 = 0;

        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Relative(path);

            // 접근자 자신과 초안 도구는 당연히 원문을 읽는다.
            if (relative.StartsWith("Assets/02.Scripts/Domain/Localization/")) continue;
            // 필드를 선언하는 SO 자신.
            if (relative.EndsWith("SO.cs")) continue;
            // 하네스는 "화면에 뜬 글자가 에셋의 값과 같은가"를 물어야 하므로 원문을 읽는다.
            if (relative.StartsWith("Assets/02.Scripts/Testing/")) continue;

            검사한파일++;
            bool 허용 = 허용했는데_없는_파일.Remove(relative);
            if (허용) continue;

            var text = LocSourceScanner.StripComments(File.ReadAllText(path));
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (대사직접읽기.IsMatch(lines[i]))
                    위반.Add($"{relative}:{i + 1}  {lines[i].Trim()}");
        }

        Assert.Greater(검사한파일, 100, "코드를 못 찾았다 — 검사가 공회전한다");

        Assert.IsEmpty(위반,
            "대사 한 줄을 DataText를 거치지 않고 직접 읽는 자리가 있다. " +
            "그 자막만 로케일을 안 따라오고, 그 로케일로 그 장면을 보기 전까지 아무 신호도 없다:\n  " +
            string.Join("\n  ", 위반));

        Assert.IsEmpty(허용했는데_없는_파일,
            "예외 목록에 이제 없는 파일이 남아 있다. 목록을 줄여라:\n  " +
            string.Join("\n  ", 허용했는데_없는_파일));
    }

    /// <summary>
    /// <b>음성 확인.</b> 고치기 전의 <see cref="Survive.Narrative.SequenceDirector"/>가
    /// 쓰던 꼴을 그대로 먹여 본다. 게이트가 그것을 못 물면 0건은 아무 뜻이 없다.
    /// </summary>
    [Test]
    public void 배선을_걷어낸_꼴을_먹이면_잡힌다()
    {
        var 물어야하는것 = new[]
        {
            "subtitle?.Show(line.speaker, line.text);",
            "if (line == null || string.IsNullOrWhiteSpace(line.text)) continue;",
            "LastLine = discovery.line.text;",
            "var 글 = sequence.lines[i].text;",
            "view.Show(so.lines[0].speaker, so.lines[0].text);",
        };

        foreach (var 줄 in 물어야하는것)
            Assert.IsTrue(대사직접읽기.IsMatch(줄), $"게이트가 이 꼴을 못 문다: {줄}");

        // 반대쪽. 오탐이 하나라도 있으면 다음 사람이 게이트를 깎는다.
        var 물면_안_되는것 = new[]
        {
            "subtitle?.Show(DataText.Speaker(sequence, i), DataText.Line(sequence, i));",
            "label.text = Loc.F(\"UI\", \"subtitle_line\", speaker, text);",
            "outline.text = \"\";",                       // TMP_Text
            "_pending.Enqueue(line);",                    // 줄을 넘기기만 한다
            "line.holdSeconds = 3f;",                     // 글자가 아니다
            "line.text = draft;",                         // 대입은 세우기다
        };

        foreach (var 줄 in 물면_안_되는것)
            Assert.IsFalse(대사직접읽기.IsMatch(줄), $"게이트가 멀쩡한 꼴을 문다: {줄}");
    }

    static string Relative(string full) =>
        "Assets" + full.Substring(Application.dataPath.Length).Replace('\\', '/');
}
