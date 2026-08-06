using NUnit.Framework;
using Survive.Domain.Art;
using Survive.Narrative;

/// <summary>
/// <b>깊이 내려갈수록 AI의 말수가 준다</b>는 규칙을 못 박는다 (기획서 §12 · 스펙 §13).
///
/// 초반 수다와 심부 침묵은 같은 인물의 기분이 아니라 <b>한 함수의 양 끝</b>이다.
/// 그래서 이것은 대사 배치의 문제가 아니라 값의 문제이고, 값이라면 시험할 수 있다.
///
/// <b>아직 아무도 이 값을 읽지 않는다.</b> 반복 발화 채널이 없기 때문이다
/// (<c>UnlockService.Announce</c>는 원장에 걸린 한 번짜리 알림이다). 그래서 여기 있는
/// 것은 규칙과 그 규칙을 지키는 시험뿐이고, 채널이 생기는 날 이 함수가 문지기가 된다.
/// 값이 먼저 서 있어야 그날 "얼마나 줄일지"를 새로 논의하지 않는다.
/// </summary>
public class AiVoiceDepthTests
{
    // ── 표가 규칙을 지키는가 ────────────────────────────────────

    [Test]
    public void 지금_말수_표가_규칙을_지킨다()
    {
        Assert.IsTrue(AiVoiceDepth.ValidateTable(out var 이유), 이유);
    }

    [Test]
    public void 말수_표의_길이가_깊이_사다리와_같다()
    {
        Assert.AreEqual(DepthFog.Bands.Length, AiVoiceDepth.UtterancesPerMinute.Length,
            "안개 밴드와 말수 표의 칸 수가 다르다 — 사다리는 하나여야 한다");
        Assert.AreEqual(DepthFog.Bands.Length, AiVoiceDepth.StageCount);
    }

    [Test]
    public void 깊어질수록_발화_빈도가_줄어든다()
    {
        for (int i = 1; i < AiVoiceDepth.StageCount; i++)
            Assert.Less(AiVoiceDepth.RatePerMinute(i), AiVoiceDepth.RatePerMinute(i - 1),
                $"{i}단이 {i - 1}단보다 말이 줄지 않았다");
    }

    [Test]
    public void 제일_얕은_곳에서는_말한다()
    {
        Assert.Greater(AiVoiceDepth.RatePerMinute(0), 0f,
            "초반 수다가 없으면 심부 침묵이 대조를 잃는다");
    }

    [Test]
    public void 심부에서는_말하지_않는다()
    {
        Assert.AreEqual(0f, AiVoiceDepth.RatePerMinute(AiVoiceDepth.StageCount - 1),
            "침묵은 \"드물게\"가 아니라 0이다");
    }

    // ── 간격 ────────────────────────────────────────────────────

    [Test]
    public void 발화_간격은_깊어질수록_늘어난다()
    {
        for (int i = 1; i < AiVoiceDepth.StageCount; i++)
            Assert.Greater(AiVoiceDepth.MinIntervalSeconds(i), AiVoiceDepth.MinIntervalSeconds(i - 1));
    }

    [Test]
    public void 심부의_발화_간격은_무한대다()
    {
        Assert.AreEqual(float.PositiveInfinity,
            AiVoiceDepth.MinIntervalSeconds(AiVoiceDepth.StageCount - 1));
    }

    [Test]
    public void 간격을_채우기_전에는_말하지_않는다()
    {
        float 섬위 = DepthFog.Bands[0].Y + 5f;
        float 간격 = AiVoiceDepth.MinIntervalSeconds(0);

        Assert.IsFalse(AiVoiceDepth.MaySpeak(섬위, 간격 - 0.1f));
        Assert.IsTrue(AiVoiceDepth.MaySpeak(섬위, 간격));
        Assert.IsTrue(AiVoiceDepth.MaySpeak(섬위, 간격 + 100f));
    }

    [Test]
    public void 심부에서는_아무리_기다려도_말하지_않는다()
    {
        float 심부 = DepthFog.Bands[DepthFog.Bands.Length - 1].Y - 50f;

        Assert.IsFalse(AiVoiceDepth.MaySpeak(심부, 60f));
        Assert.IsFalse(AiVoiceDepth.MaySpeak(심부, 100000f));
    }

    // ── 높이 → 단계 ─────────────────────────────────────────────

    [Test]
    public void 밴드_높이마다_그_단계가_나온다()
    {
        for (int i = 0; i < DepthFog.Bands.Length; i++)
            Assert.AreEqual(i, AiVoiceDepth.StageAt(DepthFog.Bands[i].Y),
                $"{i}번째 밴드 높이가 {i}단으로 잡히지 않는다");
    }

    [Test]
    public void 수면_높이는_액면_단계다()
    {
        // 밴드 표는 SeaLevelY를 두 번째 칸에 둔다. 그 자리가 곧 "액면에 서 있다"이고,
        // 챕터 1의 5번(액면 보행)이 벌어지는 높이다.
        Assert.AreEqual(1, AiVoiceDepth.StageAt(DepthFog.SeaLevelY));
    }

    [Test]
    public void 표_바깥의_높이는_끝_단계로_고정된다()
    {
        Assert.AreEqual(0, AiVoiceDepth.StageAt(DepthFog.Bands[0].Y + 1000f),
            "아주 높은 곳은 제일 얕은 단계다");
        Assert.AreEqual(DepthFog.Bands.Length - 1,
            AiVoiceDepth.StageAt(DepthFog.Bands[DepthFog.Bands.Length - 1].Y - 1000f),
            "아주 깊은 곳은 심부다");
    }

    /// <summary>
    /// <b>내려가면서 말수가 한 번도 늘지 않는가.</b> 단계별로 보는 것과 실제 높이를
    /// 훑는 것은 다른 일이다 — 단계 순서와 밴드 순서가 어긋나면 표는 멀쩡한데
    /// 게임에서는 내려갈수록 수다스러워진다.
    /// </summary>
    [Test]
    public void 높이를_훑어_내려가도_말수가_늘지_않는다()
    {
        float 꼭대기 = DepthFog.Bands[0].Y + 30f;
        float 바닥 = DepthFog.Bands[DepthFog.Bands.Length - 1].Y - 30f;

        float 앞선것 = AiVoiceDepth.RateAt(꼭대기);
        for (float y = 꼭대기; y >= 바닥; y -= 0.5f)
        {
            float 지금 = AiVoiceDepth.RateAt(y);
            Assert.LessOrEqual(지금, 앞선것, $"높이 {y}에서 말수가 늘었다");
            앞선것 = 지금;
        }

        Assert.AreEqual(0f, 앞선것, "끝까지 내려갔는데 침묵에 닿지 않았다");
    }

    // ── 규칙 자체를 시험한다 ────────────────────────────────────

    /// <summary>
    /// <b>음성 확인.</b> 규칙이 실제로 무엇을 걸러내는가. 위 검사들은 지금 표가
    /// 옳다는 것만 말하고, 규칙이 텅 비어 있어도 똑같이 초록불이다.
    /// </summary>
    [Test]
    public void 규칙을_어긴_표는_반드시_걸린다()
    {
        int 칸 = AiVoiceDepth.StageCount;

        Assert.IsFalse(AiVoiceDepth.ValidateTable(null, out _), "빈 표를 통과시킨다");
        Assert.IsFalse(AiVoiceDepth.ValidateTable(새표(칸, 3f, 3f), out _),
            "같은 값이 이어지는 표를 통과시킨다 — 단조 감소여야 한다");
        Assert.IsFalse(AiVoiceDepth.ValidateTable(새표(칸, 1f, 5f), out _),
            "깊어지며 늘어나는 표를 통과시킨다");
        Assert.IsFalse(AiVoiceDepth.ValidateTable(새표(칸, 0f, 0f), out _),
            "제일 얕은 곳에서도 말하지 않는 표를 통과시킨다");
        Assert.IsFalse(AiVoiceDepth.ValidateTable(내림표(칸, 6f, 1f), out _),
            "심부가 침묵이 아닌 표를 통과시킨다");
        Assert.IsFalse(AiVoiceDepth.ValidateTable(new[] { 6f, 0f, 0f }, out _),
            "칸 수가 깊이 사다리와 다른 표를 통과시킨다");
    }

    [Test]
    public void 걸린_이유를_말해_준다()
    {
        AiVoiceDepth.ValidateTable(새표(AiVoiceDepth.StageCount, 1f, 5f), out var 이유);
        Assert.IsNotNull(이유, "왜 걸렸는지 말하지 않으면 다음 사람이 표를 고칠 수 없다");
        Assert.IsNotEmpty(이유);
    }

    /// <summary>첫 칸부터 끝 칸까지 고르게 이어지는 표. 끝은 0으로 닫는다.</summary>
    static float[] 새표(int 칸, float 처음, float 다음)
    {
        var 표 = new float[칸];
        for (int i = 0; i < 칸; i++) 표[i] = i == 0 ? 처음 : 다음;
        표[칸 - 1] = 0f;
        if (칸 >= 3) 표[칸 - 2] = 다음;
        return 표;
    }

    /// <summary>줄어들기는 하는데 끝이 0이 아닌 표.</summary>
    static float[] 내림표(int 칸, float 처음, float 끝)
    {
        var 표 = new float[칸];
        for (int i = 0; i < 칸; i++)
            표[i] = 처음 - (처음 - 끝) * i / (칸 - 1);
        return 표;
    }
}
