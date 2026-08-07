using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Localization;
using Survive.Narrative;

/// <summary>
/// <b>연출 자막이 번역 표를 거친다</b> (2026-08-07).
///
/// <b>무엇이 뚫려 있었는가.</b> 이 저장소의 제일 굵은 규율은 <i>"화면 문자열은 코드에
/// 적을 수 없다. 표를 거친다"</i>인데, <see cref="SequenceDirector"/>만 그 밖에 있었다 —
/// <c>line.text</c>를 그대로 <see cref="SubtitleView.Show"/>에 넘겼다.
/// <see cref="DataText.Line(SequenceSO,int)"/>는 이미 있었고 <b>프로덕션 호출자가
/// 하나도 없었다.</b> 표와 에셋의 값이 지금 같아서 결과가 같았을 뿐이다.
///
/// <b>같은 것과 거쳐 가는 것은 다르다.</b> 값이 같으면 한국어 화면에서는 아무 신호도
/// 나지 않는다. 로케일을 바꾸는 순간에만 드러나고, 그때는 프롤로그만 한국어로 남는다.
/// 그래서 여기서 재는 것은 "글자가 맞는가"가 아니라 <b>어느 길로 왔는가</b>다.
///
/// <b>에셋 원문은 무엇을 위한 것인가.</b> 초안 생성 도구가 그 원문으로 표의 줄을
/// 짓는다(<c>docs/번역-체계.md</c> §10). 즉 에셋은 <b>초안의 원본</b>이고 화면의
/// 원본이 아니다. 갈라지면 표가 이기고, 표가 통째로 없으면 그때만 원문이 선다 —
/// 배포본에서 CSV가 빠져도 게임이 한국어로 돌아가게 하는 마지막 그물이다.
///
/// 배선 자체가 남아 있는지는 <c>DataTextGateTests.대사_한_줄을_직접_읽는_곳이_없다</c>가
/// 저장소 전체에서 지킨다. 여기서는 <b>값이 실제로 표에서 나오는지</b>를 본다.
/// </summary>
public class SequenceSubtitleLocTests
{
    const string 프롤로그자막 = "Assets/08.Data/Sequences/Prologue_Intro.asset";

    SequenceSO _시퀀스;
    string _처음로케일;

    [SetUp]
    public void 준비한다()
    {
        _시퀀스 = AssetDatabase.LoadAssetAtPath<SequenceSO>(프롤로그자막);
        Assert.IsNotNull(_시퀀스, $"{프롤로그자막}를 못 읽었다");
        Assert.IsNotEmpty(_시퀀스.lines, "프롤로그 자막이 비었다 — 검사가 헛돈다");

        _처음로케일 = Loc.CurrentLocale;
        Loc.Load(LocalizationTestBootstrap.LoadCatalogFromDisk());
        Loc.SetLocale(StringCatalog.DefaultLocale);
    }

    [TearDown]
    public void 되돌린다()
    {
        Loc.Load(LocalizationTestBootstrap.LoadCatalogFromDisk());
        Loc.SetLocale(_처음로케일 ?? StringCatalog.DefaultLocale);
    }

    // ── ① 표가 이긴다 ───────────────────────────────────────────

    /// <summary>
    /// <b>에셋과 표가 갈라지면 표가 이긴다.</b> 표만 고쳐 놓고 조회했을 때 고친 값이
    /// 나와야 한다. 지금은 두 값이 같아서 <b>같은 답이 두 길에서 나오므로</b>,
    /// 일부러 갈라 놓지 않으면 어느 길로 왔는지 알 수 없다.
    /// </summary>
    [Test]
    public void 에셋과_표가_갈라지면_표가_이긴다()
    {
        const string 딴글 = "표에서만 고친 자막";
        var 열쇠 = DataText.LineKey(_시퀀스, 0);

        Loc.Load(표를_고쳐서(열쇠, 딴글));

        Assert.AreEqual(딴글, DataText.Line(_시퀀스, 0),
            "표를 고쳤는데 화면 값이 안 바뀌었다 — 조회가 에셋을 직접 읽고 있다");
        Assert.AreNotEqual(_시퀀스.lines[0].text.Trim(), DataText.Line(_시퀀스, 0),
            "에셋 원문이 그대로 나왔다");
    }

    /// <summary>
    /// <b>표를 통째로 치우면 에셋 원문이 선다.</b> 이것이 에셋 원문의 쓸모다 —
    /// 초안의 원본이자 마지막 그물. <see cref="Loc"/>의 계약(키를 그대로 내주기)과
    /// 다른 자리라, 이 한 줄이 <see cref="DataText"/>를 쓰는 이유 자체다.
    /// </summary>
    [Test]
    public void 표가_없으면_에셋_원문으로_물러선다()
    {
        Loc.Load(StringCatalog.Empty);

        for (int i = 0; i < _시퀀스.lines.Length; i++)
        {
            var line = _시퀀스.lines[i];
            if (line == null || string.IsNullOrWhiteSpace(line.text)) continue;

            Assert.AreEqual(line.text.Trim(), DataText.Line(_시퀀스, i),
                $"{i}번째 줄이 표 없이 원문을 내지 않는다");
            Assert.AreEqual((line.speaker ?? "").Trim(), DataText.Speaker(_시퀀스, i),
                $"{i}번째 줄의 화자가 표 없이 원문을 내지 않는다");
        }
    }

    // ── ② 로케일을 따라온다 ─────────────────────────────────────

    /// <summary>
    /// <b>로케일을 바꾸면 자막이 바뀐다.</b> 배선이 없던 시절에는 여기가 통과할 수
    /// 없었다 — 에셋에는 로케일이라는 개념이 없다.
    /// </summary>
    [Test]
    public void 로케일을_바꾸면_자막이_바뀐다()
    {
        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk();

        int 확인 = 0;
        for (int i = 0; i < _시퀀스.lines.Length; i++)
        {
            var 열쇠 = DataText.LineKey(_시퀀스, i);
            if (!표.TryGet("en", 열쇠, out var 영어)) continue;

            Loc.SetLocale(StringCatalog.DefaultLocale);
            string 한국어 = DataText.Line(_시퀀스, i);

            Loc.SetLocale("en");
            Assert.AreEqual(영어, DataText.Line(_시퀀스, i), $"{열쇠}가 en 칸을 따라오지 않는다");
            Assert.AreNotEqual(한국어, DataText.Line(_시퀀스, i),
                $"{열쇠}가 로케일을 바꿔도 한국어 그대로다");
            확인++;
        }

        Assert.Greater(확인, 0,
            "en 칸이 하나도 안 차 있다 — 로케일 전환을 잴 수 없으므로 표에 채워라");
    }

    /// <summary>
    /// <b>의사 번역이 부푼다.</b> 부풀지 않으면 그 자리는 표 밖에 있다는 뜻이다
    /// (<c>docs/번역-체계.md</c> §6). 로케일 전환보다 이쪽이 더 넓게 잡는다 —
    /// en 칸이 비어 있는 줄까지 판정할 수 있다.
    /// </summary>
    [Test]
    public void 의사_번역에서_모든_자막이_부푼다()
    {
        Loc.SetLocale(StringCatalog.PseudoLocale);

        for (int i = 0; i < _시퀀스.lines.Length; i++)
        {
            if (_시퀀스.lines[i] == null || string.IsNullOrWhiteSpace(_시퀀스.lines[i].text)) continue;

            string 자막 = DataText.Line(_시퀀스, i);
            Assert.IsTrue(PseudoLocalizer.IsTransformed(자막),
                $"{i}번째 자막이 부풀지 않았다 (\"{자막}\") — 표를 거치지 않는 자리다");
            Assert.IsTrue(PseudoLocalizer.IsTransformed(DataText.Speaker(_시퀀스, i)),
                $"{i}번째 화자가 부풀지 않았다");
        }
    }

    // ── ③ 표에 없는 키를 요구하면 잡힌다 ────────────────────────

    /// <summary>
    /// 기존 번역 게이트 둘(코드 쪽 <c>LocalizationKeyGateTests</c>, 에셋 쪽
    /// <c>DataTextGateTests</c>)과 같은 결이다. 시퀀스만 따로 세우는 이유는
    /// <b>줄 번호가 키에 들어가기</b> 때문이다 — 자막 한 줄을 끼워 넣으면 그 뒤
    /// 줄들의 키가 통째로 한 칸씩 밀리는데, 다른 에셋에는 없는 사고다.
    /// </summary>
    [Test]
    public void 시퀀스의_모든_줄이_표에_있다()
    {
        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk();
        var 없는것 = 시퀀스가_요구하는_열쇠()
            .Where(k => !표.Contains(k))
            .Select(k => k.ToString())
            .ToList();

        Assert.IsEmpty(없는것,
            $"시퀀스 자막 {없는것.Count}줄이 표에 없다. " +
            "메뉴 Tools/Survive/번역/데이터 에셋 초안 갱신 을 돌려라:\n  " +
            string.Join("\n  ", 없는것));
    }

    /// <summary>
    /// <b>음성 확인.</b> 위 검사는 초록불일 때 아무 말도 하지 않는다. 열쇠를 한 줄
    /// 빼 놓고 실제로 잡히는지 본다 — 요구하는 열쇠를 하나도 못 세면 언제나 통과다.
    /// </summary>
    [Test]
    public void 표에서_한_줄을_지우면_잡힌다()
    {
        var 열쇠들 = 시퀀스가_요구하는_열쇠();
        Assert.Greater(열쇠들.Count, 4, "요구하는 열쇠를 너무 적게 셌다 — 검사가 헛돈다");

        var 뺀표 = 표를_고쳐서(열쇠들[0], null);

        Assert.IsFalse(뺀표.Contains(열쇠들[0]), "지웠는데 표에 남아 있다 — 시험체가 틀렸다");
        Assert.IsTrue(열쇠들.Skip(1).All(뺀표.Contains), "한 줄만 지워야 하는데 더 지웠다");
    }

    // ── ④ 화자와 대사를 잇는 꼴도 표에서 나온다 ─────────────────

    /// <summary>
    /// <c>SubtitleView</c>가 <c>"{화자} : {대사}"</c>를 코드에서 짓고 있었다.
    /// 가운데 기호는 배치가 아니라 문장의 일부다 — 중국어·일본어는 전각 기호를 쓰고,
    /// 언어에 따라 화자를 뒤에 붙이는 편이 나을 수도 있다(<c>docs/번역-체계.md</c> §3).
    /// </summary>
    [Test]
    public void 자막_한_줄의_틀이_표에서_나온다()
    {
        var 열쇠 = new LocKey("UI", "subtitle_line");
        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk();

        Assert.IsTrue(표.Contains(열쇠), $"{열쇠}가 표에 없다");

        표.TryGet(StringCatalog.DefaultLocale, 열쇠, out var 틀);
        CollectionAssert.AreEquivalent(new[] { 0, 1 }, LocFormat.Indices(틀).ToArray(),
            $"틀에 자리표가 둘이어야 한다 (화자와 대사): \"{틀}\"");

        Assert.AreEqual("우주복 AI : 착륙 완료.",
            Loc.F("UI", "subtitle_line", "우주복 AI", "착륙 완료."),
            "틀이 두 값을 잇지 않는다");

        Loc.SetLocale(StringCatalog.PseudoLocale);
        Assert.IsTrue(PseudoLocalizer.IsTransformed(Loc.F("UI", "subtitle_line", "가", "나")),
            "틀이 부풀지 않는다 — 코드에 박힌 조각이다");
    }

    /// <summary>
    /// <b>화자가 없으면 틀을 쓰지 않는다.</b> 빈 화자를 그대로 끼우면 화면 왼쪽에
    /// 콜론만 덩그러니 남는다. 그 갈래가 <see cref="SubtitleView"/>에 실제로 있는지
    /// 소스로 확인한다 — 값 하나로는 갈래가 있는지 없는지 알 수 없다.
    /// </summary>
    [Test]
    public void 화자가_없는_자막은_틀을_쓰지_않는다()
    {
        string 소스 = File.ReadAllText(Path.Combine(
            Application.dataPath, "02.Scripts", "Narrative", "SubtitleView.cs"));

        StringAssert.Contains("IsNullOrEmpty(speaker)", 소스,
            "화자가 비었을 때의 갈래가 없다 — 콜론만 남은 자막이 나간다");
    }

    // ── 훑개 ────────────────────────────────────────────────────

    static List<LocKey> 시퀀스가_요구하는_열쇠()
    {
        var 열쇠들 = new List<LocKey>();

        foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(SequenceSO)))
        {
            var seq = AssetDatabase.LoadAssetAtPath<SequenceSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (seq == null || seq.lines == null) continue;

            for (int i = 0; i < seq.lines.Length; i++)
            {
                var line = seq.lines[i];
                if (line == null) continue;
                if (!string.IsNullOrWhiteSpace(line.text)) 열쇠들.Add(DataText.LineKey(seq, i));
                if (!string.IsNullOrWhiteSpace(line.speaker)) 열쇠들.Add(DataText.SpeakerKey(seq, i));
            }
        }

        return 열쇠들;
    }

    /// <summary>
    /// 디스크의 표에서 한 줄만 바꾸거나(값을 주면) 빼고(<c>null</c>을 주면) 다시 읽는다.
    /// <b>CSV 글자를 고쳐서 다시 파싱한다</b> — 표를 손으로 지어 넣으면 파서를
    /// 건너뛰게 되고, 그러면 이 검사가 진짜 조회 경로를 밟지 않는다.
    /// </summary>
    static StringCatalog 표를_고쳐서(LocKey 열쇠, string 새값)
    {
        var 원본 = File.ReadAllLines(LocalizationTestBootstrap.CsvPath);
        var 나온것 = new List<string>(원본.Length);
        string 머리 = 열쇠.Category + "," + 열쇠.Key + ",";
        bool 찾았다 = false;

        foreach (var 줄 in 원본)
        {
            if (!줄.StartsWith(머리, System.StringComparison.Ordinal)) { 나온것.Add(줄); continue; }

            찾았다 = true;
            if (새값 == null) continue;                       // 그 줄을 통째로 뺀다

            // 뒤 칸(en·Comment)은 버린다. 모자란 칸은 문제로 적히지 않는다 (§1).
            나온것.Add(머리 + 새값);
        }

        Assert.IsTrue(찾았다, $"표에서 {열쇠} 줄을 못 찾았다 — 시험체가 틀렸다");

        var 표 = StringCatalog.Parse(string.Join("\n", 나온것));
        Assert.IsEmpty(표.Problems, "고쳐 만든 표가 건전하지 않다:\n  " + string.Join("\n  ", 표.Problems));
        return 표;
    }
}
