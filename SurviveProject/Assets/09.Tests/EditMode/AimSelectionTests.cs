using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.Interaction;

/// <summary>
/// 조준 선별 규칙. 사용자가 적은 그대로의 증상에서 나왔다 —
/// "지금 내가 바라보고 있는 물체와 자막에 뜨는 물체가 달라",
/// "범위가 너무 커서 다른 게 장애물로 있어도 보인다".
///
/// 원인은 둘이었다. ① 조준 판정에 쓰는 것이 대상의 몸이 아니라 채집 편의를 위해
/// 붙여 둔 넓은 트리거였다(0.3m짜리 잔해에 1.6m 상자, 재 고사리에 4.7m 상자).
/// 그 부피의 옆면은 언제나 코앞에 있으므로, "가장 가까운 것이 이긴다"는 옛 규칙에서는
/// 겨누지도 않은 것이 늘 이겼다. ② 가림 검사가 아예 없었다.
///
/// 둘 다 참/거짓 하나로 갈리는 판단이라 씬을 켜서 눈으로 볼 것이 아니라
/// 값으로 확인해야 한다. 그래서 규칙을 순수부로 떼어 냈다.
/// </summary>
public class AimSelectionTests
{
    // ── 거들 것들 ────────────────────────────────────────────────

    /// <summary>원점에서 정면(+Z)을 보고, 3m까지, 시선에서 0.3m까지 봐주는 눈.</summary>
    static AimView 정면(float 거리 = 3f, float 반경 = 0.3f) =>
        new AimView(Vector3.zero, Vector3.forward, 거리, 반경);

    /// <summary>한 변이 <paramref name="크기"/>인 정육면체를 <paramref name="중심"/>에 둔다.</summary>
    static AimCandidate 물체(int 번호, Vector3 중심, float 크기 = 0.3f) =>
        new AimCandidate(번호, new Bounds(중심, Vector3.one * 크기));

    /// <summary>정해 준 번호만 막혔다고 답하는 가짜 가림 검사.</summary>
    class 막힌것 : IAimObstruction
    {
        readonly HashSet<int> _번호;
        public int 물어본횟수;

        public 막힌것(params int[] 번호) => _번호 = new HashSet<int>(번호);

        public bool IsBlocked(in AimScore score)
        {
            물어본횟수++;
            return _번호.Contains(score.Id);
        }
    }

    static bool 고른다(IReadOnlyList<AimCandidate> 후보, AimView 눈, IAimObstruction 가림,
                     out AimScore 결과) =>
        AimSelection.TrySelect(후보, 눈, 가림, new List<AimScore>(), out 결과);

    static bool 고른다(IReadOnlyList<AimCandidate> 후보, out AimScore 결과) =>
        고른다(후보, 정면(), null, out 결과);

    // ── ① 막힌 것은 안 잡힌다 ────────────────────────────────────

    [Test]
    public void 막힌_것은_잡히지_않는다()
    {
        var 후보 = new[] { 물체(0, new Vector3(0f, 0f, 2f)) };

        Assert.IsFalse(고른다(후보, 정면(), new 막힌것(0), out _),
                       "사이가 막혔는데도 조준됐다");
    }

    [Test]
    public void 막힌_것을_건너뛰고_뒤의_뚫린_것을_고른다()
    {
        // 둘 다 정면. 앞의 것이 막혀 있으면 그 다음 순위가 이어받아야 한다 —
        // 막혔다고 조준 자체를 포기하면 뒤에 진짜로 보이는 것을 놓친다.
        var 후보 = new[]
        {
            물체(0, new Vector3(0f, 0f, 1f)),
            물체(1, new Vector3(0f, 0f, 2f)),
        };

        Assert.IsTrue(고른다(후보, 정면(), new 막힌것(0), out var 결과));
        Assert.AreEqual(1, 결과.Id);
    }

    [Test]
    public void 이길_만한_것부터_물어본다()
    {
        // 가림 검사는 물리 질의라 비싸다. 순위가 정해진 뒤 이길 것부터 물어
        // 첫 번째가 뚫려 있으면 거기서 멈춰야 한다.
        var 후보 = new[]
        {
            물체(0, new Vector3(0f, 0f, 2.5f)),
            물체(1, new Vector3(0f, 0f, 1.0f)),
        };

        var 가림 = new 막힌것();
        Assert.IsTrue(고른다(후보, 정면(), 가림, out _));
        Assert.AreEqual(1, 가림.물어본횟수, "뚫려 있는데도 나머지까지 물어봤다");
    }

    [Test]
    public void 눈이_안에_들어간_것은_잡히지_않는다()
    {
        // 거대 버섯의 경계 상자는 21m다 — 갓 아래 어디에 서든 눈이 그 안에 있다.
        // 이것을 차선책으로라도 잡아 주면 아무것도 안 겨눴을 때마다 벌목 안내가 뜬다.
        var 후보 = new[] { 물체(0, Vector3.zero, 2f) };

        Assert.IsFalse(고른다(후보, out _));
        Assert.AreEqual(AimReject.Inside, AimSelection.Measure(후보[0], 정면()).Reject);
    }

    // ── ② 넓은 부피가 옆에 있어도 겨눈 것이 이긴다 ───────────────

    [Test]
    public void 탈락한_후보에게는_가림을_묻지_않는다()
    {
        // 가림 검사는 물리 질의다. 이미 시선 밖으로 떨어진 것까지 물어보면
        // 매 프레임 쓸데없는 레이가 나간다.
        var 후보 = new[]
        {
            물체(0, new Vector3(3f, 0f, 2f)),      // 옆으로 벗어남
            물체(1, new Vector3(0f, 0f, 9f)),      // 너무 멂
            물체(2, new Vector3(0f, 0f, 2f)),      // 겨눔
        };

        var 가림 = new 막힌것();
        Assert.IsTrue(고른다(후보, 정면(), 가림, out var 결과));
        Assert.AreEqual(2, 결과.Id);
        Assert.AreEqual(1, 가림.물어본횟수);
    }

    [Test]
    public void 옆에_붙은_넓은_부피보다_겨눈_것이_이긴다()
    {
        // 실제 씬의 재 고사리: 몸이 3.45m나 되는 덤불이 왼쪽 코앞에 있고,
        // 정면 2.5m에 캐려는 광맥이 있다. 옛 규칙("가장 가까운 것")은 고사리를 골랐다.
        var 고사리 = new AimCandidate(0, new Bounds(new Vector3(-2f, 0f, 0.5f),
                                                  new Vector3(3.45f, 1.2f, 3.45f)));
        var 광맥 = 물체(1, new Vector3(0f, 0f, 2.5f), 0.9f);

        Assert.IsTrue(고른다(new[] { 고사리, 광맥 }, out var 결과));
        Assert.AreEqual(1, 결과.Id, "옆에 붙은 넓은 부피가 겨눈 것을 이겼다");
    }

    [Test]
    public void 가까워도_시선에서_벗어났으면_진다()
    {
        // 0.6m 앞이지만 옆으로 0.5m 비켜 있는 것 vs 2.5m 앞 정면.
        // 거리로 겨루면 앞의 것이 이기지만, 사람이 겨눈 것은 뒤의 것이다.
        var 후보 = new[]
        {
            물체(0, new Vector3(0.5f, 0f, 0.6f)),
            물체(1, new Vector3(0f, 0f, 2.5f)),
        };

        Assert.IsTrue(고른다(후보, out var 결과));
        Assert.AreEqual(1, 결과.Id);
    }

    [Test]
    public void 각도가_비슷하면_가까운_쪽이_이긴다()
    {
        // 둘 다 정확히 정면. 이때는 눈에 먼저 들어오는 가까운 쪽이 맞다.
        var 후보 = new[]
        {
            물체(0, new Vector3(0f, 0f, 2.5f)),
            물체(1, new Vector3(0f, 0f, 1.0f)),
        };

        Assert.IsTrue(고른다(후보, out var 결과));
        Assert.AreEqual(1, 결과.Id);
    }

    [Test]
    public void 몸을_뚫고_지나간_지점이_있으면_상자보다_그것을_믿는다()
    {
        // 거대 버섯처럼 상자가 통째로 큰 것은 상자만 봐서는 어디를 겨눴는지 알 수 없다.
        // 상자는 눈을 감싸고 있어 그것만 보면 탈락이지만, 시선이 실제로 밑동을 뚫었다.
        var 버섯 = new AimCandidate(0, new Bounds(Vector3.zero, Vector3.one * 20f),
                                   new Vector3(0f, 0f, 2f));

        Assert.IsTrue(고른다(new[] { 버섯 }, out var 결과), "실제로 뚫고 지나간 지점을 두고도 상자를 봤다");
        Assert.AreEqual(2f, 결과.Distance, 0.001f);
    }

    [Test]
    public void 눈을_감싼_것은_겨눈_것에_진다()
    {
        // 거대 버섯 갓 아래에 서 있어도, 코앞의 통을 보고 있으면 통이 이긴다.
        var 감싼것 = 물체(0, Vector3.zero, 4f);
        var 겨눈것 = 물체(1, new Vector3(0f, 0f, 2f));

        Assert.IsTrue(고른다(new[] { 감싼것, 겨눈것 }, out var 결과));
        Assert.AreEqual(1, 결과.Id);
    }

    // ── ③ 아무것도 안 겨누면 후보 없음 ───────────────────────────

    [Test]
    public void 아무것도_겨누지_않으면_아무것도_고르지_않는다()
    {
        // 3m 앞에 있지만 옆으로 1.5m 비켜 있다. 시선에 걸리지 않는다.
        var 후보 = new[] { 물체(0, new Vector3(1.5f, 0f, 3f)) };

        Assert.IsFalse(고른다(후보, out _));
    }

    [Test]
    public void 후보가_없으면_고르지_않는다()
    {
        Assert.IsFalse(고른다(new AimCandidate[0], out _));
    }

    [Test]
    public void 등_뒤의_것은_잡히지_않는다()
    {
        var 후보 = new[] { 물체(0, new Vector3(0f, 0f, -2f)) };

        Assert.IsFalse(고른다(후보, out _));
        Assert.AreEqual(AimReject.Behind, AimSelection.Measure(후보[0], 정면()).Reject);
    }

    // ── ④ 거리 밖은 안 잡힌다 ────────────────────────────────────

    [Test]
    public void 손이_닿는_거리_밖은_잡히지_않는다()
    {
        var 후보 = new[] { 물체(0, new Vector3(0f, 0f, 5f), 0.2f) };

        Assert.IsFalse(고른다(후보, out _));
        Assert.AreEqual(AimReject.TooFar, AimSelection.Measure(후보[0], 정면()).Reject);
    }

    [Test]
    public void 거리_경계_바로_안쪽은_잡힌다()
    {
        // 한 변 0.2m 정육면체의 앞면이 2.9m. 경계에서 규칙이 흔들리면 안 된다.
        var 후보 = new[] { 물체(0, new Vector3(0f, 0f, 3f), 0.2f) };

        Assert.IsTrue(고른다(후보, out var 결과));
        Assert.AreEqual(2.9f, 결과.Distance, 0.001f);
    }

    [Test]
    public void 거리_밖의_것이_거리_안의_것을_밀어내지_않는다()
    {
        var 후보 = new[]
        {
            물체(0, new Vector3(0f, 0f, 8f), 0.2f),
            물체(1, new Vector3(0f, 0f, 2f), 0.2f),
        };

        Assert.IsTrue(고른다(후보, out var 결과));
        Assert.AreEqual(1, 결과.Id);
    }

    // ── ⑤ 자기 몸은 안 잡힌다 ────────────────────────────────────

    [Test]
    public void 자기_몸은_잡히지_않는다()
    {
        // 판정 원점이 머릿속이라 자기 콜라이더는 어떤 조준에서도 후보로 잡힌다.
        var 후보 = new[] { new AimCandidate(0, new Bounds(Vector3.zero, Vector3.one * 2f), true) };

        Assert.IsFalse(고른다(후보, out _));
        Assert.AreEqual(AimReject.Self, AimSelection.Measure(후보[0], 정면()).Reject);
    }

    [Test]
    public void 자기_몸이_있어도_앞의_것을_고른다()
    {
        var 후보 = new[]
        {
            new AimCandidate(0, new Bounds(Vector3.zero, Vector3.one * 2f), true),
            물체(1, new Vector3(0f, 0f, 1.5f)),
        };

        Assert.IsTrue(고른다(후보, out var 결과));
        Assert.AreEqual(1, 결과.Id);
    }

    // ── 너그러움의 폭 ────────────────────────────────────────────

    [Test]
    public void 반경_안쪽으로_비켜난_작은_것은_봐준다()
    {
        // 픽셀 단위로 겨누게 하면 그것대로 괴롭다. 시선에서 0.3m까지는 겨눈 것으로 본다.
        var 후보 = new[] { 물체(0, new Vector3(0.2f, 0f, 2f), 0.05f) };

        Assert.IsTrue(고른다(후보, out var 결과));
        Assert.AreEqual(0.175f, 결과.Lateral, 0.01f, "상자의 왼쪽 면까지의 옆거리여야 한다");
    }

    [Test]
    public void 반경_바깥으로_비켜나면_봐주지_않는다()
    {
        var 후보 = new[] { 물체(0, new Vector3(0.45f, 0f, 2f), 0.05f) };

        Assert.IsFalse(고른다(후보, out _));
        Assert.AreEqual(AimReject.OffAxis, AimSelection.Measure(후보[0], 정면()).Reject);
    }

    [Test]
    public void 너그러움은_거리가_아니라_옆으로_벗어난_거리로_잰다()
    {
        // 같은 0.25m를 비켜나 있어도 멀리 있는 쪽이 화면에서는 훨씬 덜 어긋나 보인다.
        // 잡히는가는 둘 다 같아야 하고(같은 옆거리), 각도만 달라야 한다.
        var 가까운것 = AimSelection.Measure(물체(0, new Vector3(0.25f, 0f, 0.6f), 0.02f), 정면());
        var 먼것 = AimSelection.Measure(물체(1, new Vector3(0.25f, 0f, 2.5f), 0.02f), 정면());

        Assert.IsTrue(가까운것.Accepted);
        Assert.IsTrue(먼것.Accepted);
        Assert.Greater(가까운것.AngleDegrees, 먼것.AngleDegrees);
    }

    // ── 순서 규칙 자체 ───────────────────────────────────────────

    [Test]
    public void 순위는_넣은_순서에_흔들리지_않는다()
    {
        var a = 물체(0, new Vector3(0.06f, 0f, 1.0f), 0.1f);
        var b = 물체(1, new Vector3(0.10f, 0f, 1.4f), 0.1f);
        var c = 물체(2, new Vector3(0.02f, 0f, 2.2f), 0.1f);

        var 앞에서 = new List<AimScore>();
        var 뒤에서 = new List<AimScore>();
        AimSelection.Rank(new[] { a, b, c }, 정면(), 앞에서);
        AimSelection.Rank(new[] { c, b, a }, 정면(), 뒤에서);

        Assert.AreEqual(앞에서.Count, 뒤에서.Count);
        for (int i = 0; i < 앞에서.Count; i++)
            Assert.AreEqual(앞에서[i].Id, 뒤에서[i].Id, $"{i}번째 순위가 입력 순서에 따라 달라졌다");
    }

    [Test]
    public void 탈락한_것은_순위에_남지_않는다()
    {
        var 후보 = new[]
        {
            물체(0, new Vector3(3f, 0f, 2f)),          // 옆으로 벗어남
            물체(1, new Vector3(0f, 0f, 9f)),          // 너무 멂
            물체(2, new Vector3(0f, 0f, -2f)),         // 등 뒤
            물체(3, new Vector3(0f, 0f, 2f)),          // 겨눔
        };

        var 순위 = new List<AimScore>();
        AimSelection.Rank(후보, 정면(), 순위);

        Assert.AreEqual(1, 순위.Count);
        Assert.AreEqual(3, 순위[0].Id);
    }

    // ── 겨냥점 찾기 ──────────────────────────────────────────────

    [Test]
    public void 시선이_상자를_뚫으면_들어가는_지점을_준다()
    {
        var 상자 = new Bounds(new Vector3(0f, 0f, 2f), Vector3.one);
        var 점 = AimSelection.ClosestPointOnRay(상자, Vector3.zero, Vector3.forward, 3f);

        Assert.AreEqual(1.5f, 점.z, 0.001f, "앞면이 아니라 다른 자리를 겨눴다");
    }

    [Test]
    public void 눈이_상자_안에_있으면_겨냥점이_눈_자리다()
    {
        // 방향이 서지 않는다는 것을 값으로 못 박는다. Measure가 이것을 보고 떨어뜨린다.
        var 상자 = new Bounds(Vector3.zero, Vector3.one * 6f);
        var 점 = AimSelection.ClosestPointOnRay(상자, Vector3.zero, Vector3.forward, 3f);

        Assert.AreEqual(0f, 점.magnitude, 0.001f);
    }

    [Test]
    public void 시선이_빗나가면_상자에서_시선에_가장_가까운_점을_준다()
    {
        var 상자 = new Bounds(new Vector3(1f, 0f, 2f), Vector3.one);
        var 점 = AimSelection.ClosestPointOnRay(상자, Vector3.zero, Vector3.forward, 3f);

        Assert.AreEqual(0.5f, 점.x, 0.001f, "상자의 왼쪽 면이 아니다");
        Assert.AreEqual(2f, 점.z, 0.001f);
    }
}
