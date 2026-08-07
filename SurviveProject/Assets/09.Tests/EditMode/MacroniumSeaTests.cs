using NUnit.Framework;
using Survive.World;

/// <summary>
/// 백로그 32 — "섬 사이 바다는 옅은 매크로늄이고, 몸을 담그고 있는 동안 살을 깎는다".
///
/// 규칙은 Unity 없이 도는 순수 정적 클래스이므로 씬도 MonoBehaviour도 쓰지 않는다.
/// 여기서 보는 것은 <b>값을 매기는 규칙</b>이고, 그 규칙이 실제로 사람을 죽이고
/// 가방을 남기는지는 <c>E2EMacroniumSea</c>가 실제 사망 경로로 본다.
/// </summary>
public class MacroniumSeaTests
{
    static bool 딛음 => true;
    static bool 떠있음 => false;

    // ── 깎이는가 ────────────────────────────────────────────────────────────

    [Test]
    public void 헤엄치는_동안은_깎인다()
    {
        Assert.IsTrue(MacroniumSea.Corrodes(SeaImmersion.Swimming, 떠있음));
    }

    [Test]
    public void 헤엄치는_동안은_바닥을_딛어도_깎인다()
    {
        // 깊은 물 바닥에 발이 닿았다고 몸이 물 밖에 있는 것은 아니다.
        // 여기서 무해가 되면 Ctrl로 가라앉아 바닥을 긁으며 건너는 길이 열린다.
        Assert.IsTrue(MacroniumSea.Corrodes(SeaImmersion.Swimming, 딛음));
    }

    [Test]
    public void 바닥을_딛고_선_얕은_물은_무해하다()
    {
        // 2번 섬 해안 채집이 잡무가 되면 안 된다는 결정이 이 한 줄이다.
        Assert.IsFalse(MacroniumSea.Corrodes(SeaImmersion.Wading, 딛음));
    }

    [Test]
    public void 얕게_읽히더라도_떠_있으면_깎인다()
    {
        // 수면에 뜬 채 앞으로 나아가면 몸이 부력과 중력 사이에서 오르내려
        // 대부분의 프레임이 여울로 읽힌다(실측 3972프레임 중 3574). 발을 딛었는지까지
        // 보지 않으면 바다 한가운데를 떠서 건너는 사람이 공짜가 된다.
        Assert.IsTrue(MacroniumSea.Corrodes(SeaImmersion.Wading, 떠있음));
    }

    [Test]
    public void 물_밖은_어느_쪽이든_무해하다()
    {
        Assert.IsFalse(MacroniumSea.Corrodes(SeaImmersion.Dry, 딛음));
        Assert.IsFalse(MacroniumSea.Corrodes(SeaImmersion.Dry, 떠있음));
    }

    // ── 얼마나 깎이는가 ──────────────────────────────────────────────────────

    [Test]
    public void 깎이는_자리에서는_정해진_초당_피해가_나온다()
    {
        Assert.AreEqual(MacroniumSea.CorrosionPerSecond,
                        MacroniumSea.DamagePerSecond(SeaImmersion.Swimming, 떠있음), 0.0001f);
    }

    [Test]
    public void 무해한_자리에서는_초당_피해가_0이다()
    {
        Assert.AreEqual(0f, MacroniumSea.DamagePerSecond(SeaImmersion.Wading, 딛음), 0.0001f);
        Assert.AreEqual(0f, MacroniumSea.DamagePerSecond(SeaImmersion.Dry, 떠있음), 0.0001f);
    }

    [Test]
    public void 버틴_시간에_비례해_쌓인다()
    {
        float 십초 = MacroniumSea.DamageOver(SeaImmersion.Swimming, 떠있음, 10f);
        Assert.AreEqual(MacroniumSea.CorrosionPerSecond * 10f, 십초, 0.0001f);
    }

    [Test]
    public void 시간이_흐르지_않으면_피해도_없다()
    {
        Assert.AreEqual(0f, MacroniumSea.DamageOver(SeaImmersion.Swimming, 떠있음, 0f), 0.0001f);
        Assert.AreEqual(0f, MacroniumSea.DamageOver(SeaImmersion.Swimming, 떠있음, -3f), 0.0001f);
    }

    [Test]
    public void 무해한_자리는_아무리_오래_있어도_0이다()
    {
        Assert.AreEqual(0f, MacroniumSea.DamageOver(SeaImmersion.Wading, 딛음, 600f), 0.0001f);
    }

    // ── 값이 실측과 맞는가 ───────────────────────────────────────────────────

    /// <summary>
    /// 실측한 횡단 시간에 규칙을 곱해 사용자가 정한 구간(25~35%)에 들어가는지 본다.
    ///
    /// 이 검사가 지키는 것은 코드가 아니라 <b>결정</b>이다. 초당 피해를 조용히 두 배로
    /// 올리면 컴파일도 되고 다른 테스트도 전부 통과하지만, 2번 섬은 갈 수 없는 곳이 된다.
    /// </summary>
    [Test]
    public void 맨몸_보통_수영_횡단은_체력의_25에서_35퍼센트를_먹는다()
    {
        const float 보통횡단초 = 10.3f;    // 1→2 실측: 41.2m, Space 없이 보통 수영
        const float 최대체력 = 100f;

        float 비율 = MacroniumSea.DamageOver(SeaImmersion.Swimming, 떠있음, 보통횡단초) / 최대체력;

        Assert.GreaterOrEqual(비율, 0.25f, $"횡단이 너무 싸다 ({비율:P0})");
        Assert.LessOrEqual(비율, 0.35f, $"횡단이 너무 비싸다 ({비율:P0})");
    }

    [Test]
    public void Shift_횡단은_보통_횡단보다_유의미하게_싸다()
    {
        const float 보통횡단초 = 10.3f;
        const float 가속횡단초 = 7.1f;     // 1→2 실측: 같은 구간을 Shift로

        float 보통 = MacroniumSea.DamageOver(SeaImmersion.Swimming, 떠있음, 보통횡단초);
        float 가속 = MacroniumSea.DamageOver(SeaImmersion.Swimming, 떠있음, 가속횡단초);

        // 가속이 생존기여야 한다. 몇 퍼센트 아끼는 정도면 아무도 누르지 않는다.
        Assert.Less(가속, 보통 * 0.8f, $"가속이 아껴 주는 것이 너무 적다 (보통 {보통:F1}, 가속 {가속:F1})");
    }

    // ── 물어뜯는 간격 ────────────────────────────────────────────────────────

    [Test]
    public void 한_입의_피해는_간격만큼의_초당_피해다()
    {
        Assert.AreEqual(MacroniumSea.CorrosionPerSecond * MacroniumSea.BiteInterval,
                        MacroniumSea.BiteDamage, 0.0001f);
        Assert.Greater(MacroniumSea.BiteInterval, 0f, "간격이 0이면 매 프레임 연출이 터진다");
    }

    [Test]
    public void 온전한_체력이_한_입에_사라지지는_않는다()
    {
        // 옅은 구간은 깎는 것이지 죽이는 것이 아니다. 즉사는 짙은 구간(MacroniumContact)의 몫이다.
        Assert.Less(MacroniumSea.BiteDamage, 100f * 0.2f);
    }
}
