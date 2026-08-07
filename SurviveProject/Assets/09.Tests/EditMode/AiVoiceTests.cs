using NUnit.Framework;
using Survive.Narrative;

/// <summary>
/// AI 말투 규격이 쓰는 <b>자</b>가 제대로 재는가 (<see cref="AiVoice"/>).
///
/// <b>왜 따로 시험하는가.</b> 전수 검사(<c>AiSpeechGateTests</c>)는 초록불일 때
/// 아무 말도 하지 않는다. 자가 조용히 망가져 늘 "한 문장"이라고 답해도 통과한다.
/// 자를 따로 재어 두어야 전수 검사의 초록불이 뜻을 갖는다.
/// </summary>
public class AiVoiceTests
{
    const string 규격_본보기 =
        "외계 금속물체 분석... 에너지로 사용가능한 것으로 보입니다. 해당 물질을 사용한 제작법이 있습니다.";

    // ── 문장 세기 ───────────────────────────────────────────────

    /// <summary>
    /// <b>말줄임표도 문장을 끝낸다.</b> 규격의 첫 문장은 "분석..."으로 닫히고,
    /// 마침표만 세면 그 줄이 두 문장으로 잡혀 네 문장짜리 대사가 조용히 통과한다.
    /// </summary>
    [Test]
    public void 말줄임표가_문장을_끝낸다()
    {
        Assert.AreEqual(1, AiVoice.SentenceCount("금속 구조물 분석..."));
        Assert.AreEqual(2, AiVoice.SentenceCount("금속 구조물 분석... 쓸 수 있는 것으로 보입니다."));
    }

    [Test]
    public void 점_여러_개는_한_번만_센다()
    {
        Assert.AreEqual(1, AiVoice.SentenceCount("탐색을 실시합니다..."));
        Assert.AreEqual(1, AiVoice.SentenceCount("탐색을 실시합니다......"));
    }

    [Test]
    public void 규격_본보기는_세_문장이다()
    {
        Assert.AreEqual(3, AiVoice.SentenceCount(규격_본보기));
        Assert.AreEqual(AiVoice.MaxSentences, AiVoice.SentenceCount(규격_본보기),
            "본보기가 상한과 어긋나면 규격이 스스로를 어긴 것이다");
    }

    [Test]
    public void 한_문장_더_붙이면_상한을_넘는다()
    {
        Assert.Greater(AiVoice.SentenceCount(규격_본보기 + " 추가 관측이 필요합니다."),
                       AiVoice.MaxSentences);
    }

    /// <summary>
    /// 연출 자막은 분석 보고가 아니지만 문장 상한은 같이 지킨다.
    ///
    /// <b>표본은 지금 프롤로그에 실제로 있는 세 줄이다</b>(2026-08-07). 예전에는
    /// 폐기된 무대의 자막("근처에 적절한 피난처 감지" — 동굴로 피신하던 시절의 것)을
    /// 표본으로 들고 있었다. 그 줄이 자막에서 빠진 뒤에도 여기 남으면
    /// <b>이 파일만이 폐기된 대사를 붙들게 되고</b>, 저장소 훑개는 그것을
    /// "그냥 테스트 문자열"로 지나친다. 표본은 살아 있는 것으로 든다.
    /// </summary>
    [Test]
    public void 프롤로그_자막의_문장_수를_센다()
    {
        Assert.AreEqual(2, AiVoice.SentenceCount("착륙 완료. 성간 항행에 쓸 연료는 남지 않았습니다."));
        Assert.AreEqual(1, AiVoice.SentenceCount("광역 측량 결과 : 기준이 되는 별이 하나도 잡히지 않았습니다."));
        Assert.AreEqual(2, AiVoice.SentenceCount("주의 : 지표를 덮은 액체가 데이터베이스에 없습니다. 접촉을 피하십시오."));
    }

    [Test]
    public void 빈_글은_영_문장이다()
    {
        Assert.AreEqual(0, AiVoice.SentenceCount(null));
        Assert.AreEqual(0, AiVoice.SentenceCount(""));
        Assert.AreEqual(0, AiVoice.SentenceCount("   "));
        Assert.AreEqual(0, AiVoice.SentenceCount("..."));
    }

    // ── 그림글자 ────────────────────────────────────────────────

    [Test]
    public void 그림글자를_찾아낸다()
    {
        Assert.IsTrue(AiVoice.HasEmoji("분석 완료 \U0001F600"), "얼굴 이모지를 못 잡는다");
        Assert.IsTrue(AiVoice.HasEmoji("경고 ⚠"), "장식 기호를 못 잡는다");
        Assert.IsTrue(AiVoice.HasEmoji("✅ 확인"), "체크 표시를 못 잡는다");
    }

    /// <summary>
    /// <b>문장 부호는 그림글자가 아니다.</b> 프롤로그의 콜론과 가운뎃점이 여기 걸리면
    /// 멀쩡한 대사가 규격 위반이 된다. AI가 조회하지 못한 값을 가리는 <c>$#@%</c>도
    /// 같다 — 기호가 뭉쳐 있을 뿐 그림글자가 아니다.
    /// </summary>
    [Test]
    public void 문장_부호와_한글은_그림글자가_아니다()
    {
        Assert.IsFalse(AiVoice.HasEmoji(규격_본보기));
        Assert.IsFalse(AiVoice.HasEmoji("주의 : 지표를 덮은 액체가 데이터베이스에 없습니다. 접촉을 피하십시오."));
        Assert.IsFalse(AiVoice.HasEmoji("$#@% 등급으로 조회됩니다."));
        Assert.IsFalse(AiVoice.HasEmoji("내구 40 · 이동 3.5"));
        Assert.IsFalse(AiVoice.HasEmoji(null));
    }

    /// <summary>
    /// 화살표는 <see cref="AiVoice.HasEmoji"/>가 보지 않는다. 그림글자이기 이전에
    /// <b>방향 지시</b>라, 게이트의 방향 지시어 목록이 잡아야 실패 문구가 옳은 말을 한다.
    /// </summary>
    [Test]
    public void 화살표는_그림글자로_치지_않는다()
    {
        Assert.IsFalse(AiVoice.HasEmoji("접근 개체 → 감지."));
    }

    // ── 자르기 ──────────────────────────────────────────────────

    [Test]
    public void 자른_문장에서_구두점이_떨어진다()
    {
        var 문장들 = AiVoice.SplitSentences(규격_본보기);

        Assert.AreEqual(3, 문장들.Count);
        Assert.AreEqual("외계 금속물체 분석", 문장들[0]);
        Assert.AreEqual("에너지로 사용가능한 것으로 보입니다", 문장들[1]);
        Assert.AreEqual("해당 물질을 사용한 제작법이 있습니다", 문장들[2]);
    }
}
