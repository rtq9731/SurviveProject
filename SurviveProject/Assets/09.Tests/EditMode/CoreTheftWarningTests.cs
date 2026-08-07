using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;
using Survive.Localization;
using Survive.Narrative;
using Survive.Progression;

/// <summary>
/// <b>코어를 훔치기 전의 사전 경고</b> (기획서 §4.5, 스펙 §8-4).
///
/// 기획서가 요구하는 것은 "경고가 있어야 불운이 아니라 <b>감수한 결정</b>이 된다"이고,
/// 그 문장이 코드에서 참이 되려면 두 가지가 필요하다 — <b>훔치기 전에</b> 울릴 것,
/// 그리고 <b>한 번만</b> 울릴 것. 앞엣것은 <see cref="CoreTheftWarning.IsMoment"/>가
/// 들고, 뒤엣것은 <b>원장</b>이 든다. 뒤엣것을 컴포넌트의 bool이 들면 저장본을
/// 불러올 때마다 되살아나므로, 여기서는 <b>원장이 든다</b>는 사실 자체를 못 박는다.
///
/// 문체는 여기서 다시 재지 않는다 — <c>AiSpeechGateTests</c>가 <c>Assets/08.Data</c>의
/// 모든 대사를 종류 열거 없이 훑으므로 이 경고도 이미 그 안에 있다. 여기서는
/// <b>그 훑개 안에 실제로 들어와 있는지</b>만 확인한다.
/// </summary>
public class CoreTheftWarningTests
{
    const string 경고에셋 = "Assets/08.Data/Progression/Resources/Warn_CoreTheft.asset";

    // ── ① 언제가 그때인가 ───────────────────────────────────────

    [Test]
    public void 반경_밖에서는_울리지_않는다()
    {
        Assert.IsFalse(CoreTheftWarning.IsMoment(true, CoreTheftWarning.WarnRadius + 0.1f));
    }

    [Test]
    public void 반경_안이고_코어가_둥지에_있으면_울린다()
    {
        Assert.IsTrue(CoreTheftWarning.IsMoment(true, 0f));
        Assert.IsTrue(CoreTheftWarning.IsMoment(true, CoreTheftWarning.WarnRadius));
    }

    /// <summary>
    /// 이미 훔친 뒤에는 경고가 아니다. 손에 들고 되돌려 놓으러 가는 길에
    /// "가져가면 반응한다"고 말하는 것은 틀린 말이다.
    /// </summary>
    [Test]
    public void 코어가_둥지에_없으면_울리지_않는다()
    {
        Assert.IsFalse(CoreTheftWarning.IsMoment(false, 0f));
        Assert.IsFalse(CoreTheftWarning.IsMoment(false, CoreTheftWarning.WarnRadius - 1f));
    }

    /// <summary>
    /// 경고 반경이 넉넉해야 하는 이유는 <b>되돌아설 여유</b>다. 코어에 손이 닿는
    /// 자리에서 울리면 통보이지 경고가 아니고, 낫의 지각 반경 안에서 울리면
    /// 이미 붙잡힌 뒤다.
    /// </summary>
    [Test]
    public void 경고_반경이_둥지_반경과_낫의_지각_반경보다_넓다()
    {
        Assert.Greater(CoreTheftWarning.WarnRadius, NestRule.HomeRadius,
                       "둥지 반경 안에서 울리면 이미 코어에 손이 닿는 자리다");

        // 낫 정의의 기본 지각 반경. 이 숫자가 바뀌면 경고도 따라 넓어져야 한다.
        const float 낫의_지각_반경 = 8f;
        Assert.Greater(CoreTheftWarning.WarnRadius, 낫의_지각_반경,
                       "낫이 이미 알아챈 뒤에 울리는 경고는 고를 여지를 주지 않는다");
    }

    // ── ② 1회성의 주인은 원장이다 ───────────────────────────────

    [Test]
    public void 원장이_경고를_한_번만_내준다()
    {
        var 원장 = new UnlockLedger();

        Assert.IsTrue(CoreTheftWarning.TryClaim(원장), "처음이면 내준다");
        Assert.IsFalse(CoreTheftWarning.TryClaim(원장), "두 번째는 막는다");
        Assert.IsFalse(CoreTheftWarning.TryClaim(원장), "세 번째도 막는다");
    }

    [Test]
    public void 원장이_없으면_울리지_않는다()
    {
        Assert.IsFalse(CoreTheftWarning.TryClaim(null),
                       "셀 수 없는 1회성은 1회성이 아니다");
    }

    /// <summary>
    /// <b>이 검사가 이 파일의 알맹이다.</b> 저장했다 불러온 원장도 두 번째를 막는가.
    /// 막지 못하면 판을 다시 열 때마다 AI가 같은 경고를 되풀이한다.
    /// </summary>
    [Test]
    public void 저장했다_불러온_원장도_두_번째를_막는다()
    {
        var 처음 = new UnlockLedger();
        Assert.IsTrue(CoreTheftWarning.TryClaim(처음));

        var 불러온것 = new UnlockLedger();
        불러온것.Restore(처음.Capture());

        Assert.IsTrue(불러온것.IsUnlocked(CoreTheftWarning.Key), "열쇠가 저장을 건넜다");
        Assert.IsFalse(CoreTheftWarning.TryClaim(불러온것), "불러온 뒤에도 다시 울리지 않는다");
    }

    /// <summary>
    /// <b>음성 확인.</b> 위 검사들은 열쇠가 빈 문자열이어도 통과할 수 있다 —
    /// <see cref="UnlockLedger.Unlock"/>이 빈 열쇠에 false를 돌려주기 때문이다.
    /// 그러면 "언제나 막힌다"가 되어 경고는 <b>한 번도</b> 울리지 않는다.
    /// </summary>
    [Test]
    public void 경고_열쇠가_비어_있지_않다()
    {
        Assert.IsNotEmpty(CoreTheftWarning.Key);
        Assert.IsFalse(new UnlockLedger().IsUnlocked(CoreTheftWarning.Key),
                       "빈 열쇠는 언제나 열려 있는 것으로 친다 — 그러면 경고가 영영 안 울린다");
    }

    /// <summary>발견 열쇠와 섞이지 않는다. 이것은 무언가를 여는 기록이 아니다.</summary>
    [Test]
    public void 경고_열쇠가_발견_열쇠와_섞이지_않는다()
    {
        StringAssert.StartsWith("warn:", CoreTheftWarning.Key);
        Assert.IsFalse(CoreTheftWarning.Key.StartsWith("discovery:"));
    }

    // ── ③ 대사가 실제로 있고, 문체 게이트 안에 있다 ─────────────

    [Test]
    public void 경고_대사_에셋이_제자리에_있다()
    {
        var seq = 경고대사();

        Assert.AreEqual("warn_core_theft", seq.id, "id가 표의 열쇠를 짓는다");
        Assert.AreEqual(1, seq.lines.Length, "사전 경고는 한 줄이다");
        Assert.AreEqual("우주복 AI", seq.lines[0].speaker);
        Assert.IsNotEmpty(seq.lines[0].text);
    }

    /// <summary>
    /// <c>Resources.Load</c>로 찾지 못하면 게임 안에서는 대사가 없는 것과 같다.
    /// 에셋을 옮기면 컴파일도 검사도 통과하면서 경고만 조용히 사라진다.
    /// </summary>
    [Test]
    public void 경고_대사를_Resources로_찾을_수_있다()
    {
        var seq = Resources.Load<SequenceSO>(CoreTheftWarning.ResourceName);
        Assert.IsNotNull(seq, $"Resources/{CoreTheftWarning.ResourceName}을 못 찾는다");
        Assert.AreEqual(경고대사(), seq, "Resources가 다른 에셋을 집어 온다");
    }

    /// <summary>
    /// 문체 게이트(<c>AiSpeechGateTests</c>)는 <c>Assets/08.Data</c> 아래를 훑는다.
    /// 경고 대사가 그 밖에 있으면 게이트가 이 줄만 못 본다.
    /// </summary>
    [Test]
    public void 경고_대사가_문체_게이트의_훑는_자리_안에_있다()
    {
        StringAssert.StartsWith(DataTextAssets.Root, 경고에셋);
        Assert.IsTrue(DataTextAssets.FindAll().Any(so => so == 경고대사()),
                      "문체 게이트의 훑개가 경고 대사를 담지 못한다");
    }

    /// <summary>
    /// 화면에 나가는 글자는 표에서 온다. 표에 열쇠가 없으면 에셋 원문으로
    /// 물러서는데, 그 자리가 곧 로케일을 바꿔도 한국어로 남는 줄이 된다.
    /// </summary>
    [Test]
    public void 경고_대사의_두_열쇠가_표에_있다()
    {
        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk()
                                          .TableFor(StringCatalog.DefaultLocale);
        var seq = 경고대사();

        Assert.IsTrue(표.ContainsKey(DataText.LineKey(seq, 0)),
                      $"{DataText.LineKey(seq, 0)} 가 표에 없다");
        Assert.IsTrue(표.ContainsKey(DataText.SpeakerKey(seq, 0)),
                      $"{DataText.SpeakerKey(seq, 0)} 가 표에 없다");
    }

    static SequenceSO 경고대사()
    {
        var seq = UnityEditor.AssetDatabase.LoadAssetAtPath<SequenceSO>(경고에셋);
        Assert.IsNotNull(seq, $"{경고에셋} 을 못 찾았다");
        return seq;
    }
}
