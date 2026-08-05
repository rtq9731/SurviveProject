using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.Combat;

/// <summary>
/// 백로그 22 — 자가 타격 수정. MeleeSwing에서 뽑아낸 대상 선별 규칙.
///
/// 자해는 눈으로 잡기 어려운 결함이었다. 판정 구의 중심이 카메라라
/// 자기 CharacterController가 늘 후보로 잡히는데, 유일한 필터인 전방 원뿔은
/// 시선을 54도쯤 내리면 자기 몸을 통과시킨다. 발치의 광맥을 캐는 아주 평범한
/// 자세에서 곡괭이 피해가 그대로 자기 체력에 들어왔다.
///
/// 원뿔 경계와 "자기 몸인가"는 둘 다 참/거짓 하나로 갈리는 판단이라
/// 씬을 켜서 확인할 것이 아니라 값으로 확인해야 한다.
/// </summary>
public class MeleeTargetingTests
{
    readonly List<GameObject> _만든것 = new List<GameObject>();

    [TearDown]
    public void 정리()
    {
        foreach (var go in _만든것)
            if (go != null) Object.DestroyImmediate(go);
        _만든것.Clear();
    }

    GameObject 오브젝트(string 이름)
    {
        var go = new GameObject(이름);
        _만든것.Add(go);
        return go;
    }

    /// <summary>정면을 0도로 두고 수평으로 <paramref name="각도"/>만큼 튼 방향.</summary>
    static Vector3 방향(float 각도) =>
        Quaternion.AngleAxis(각도, Vector3.up) * Vector3.forward;

    // ── 전방 원뿔의 경계 ────────────────────────────────────────────────

    [Test]
    public void 원뿔_각도는_반각의_코사인이_된다()
    {
        Assert.AreEqual(Mathf.Cos(45f * Mathf.Deg2Rad), MeleeTargeting.ConeCosLimit(90f), 1e-5f);
    }

    [Test]
    public void 정면은_닿는다()
    {
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Assert.IsTrue(MeleeTargeting.IsWithinCone(Vector3.forward, 방향(0f) * 3f, 한계));
    }

    [Test]
    public void 반각_안쪽은_닿는다()
    {
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Assert.IsTrue(MeleeTargeting.IsWithinCone(Vector3.forward, 방향(44.9f) * 2f, 한계));
    }

    [Test]
    public void 반각_바깥은_닿지_않는다()
    {
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Assert.IsFalse(MeleeTargeting.IsWithinCone(Vector3.forward, 방향(45.1f) * 2f, 한계));
    }

    [Test]
    public void 코사인_한계_위에_정확히_선_것은_닿는다()
    {
        // 각도 0인 원뿔의 한계는 1 — 정면과의 내적이 정확히 그 값이다.
        Assert.IsTrue(MeleeTargeting.IsWithinCone(Vector3.forward, Vector3.forward,
                                                  MeleeTargeting.ConeCosLimit(0f)));
    }

    [Test]
    public void 뒤쪽은_닿지_않는다()
    {
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Assert.IsFalse(MeleeTargeting.IsWithinCone(Vector3.forward, Vector3.back * 5f, 한계));
    }

    [Test]
    public void 거리는_원뿔_판정을_바꾸지_않는다()
    {
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Assert.IsTrue(MeleeTargeting.IsWithinCone(Vector3.forward, 방향(30f) * 0.01f, 한계));
        Assert.IsTrue(MeleeTargeting.IsWithinCone(Vector3.forward, 방향(30f) * 100f, 한계));
    }

    [Test]
    public void 원점과_겹친_대상은_닿지_않는다()
    {
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Assert.IsFalse(MeleeTargeting.IsWithinCone(Vector3.forward, Vector3.zero, 한계));
    }

    [Test]
    public void 발치를_보면_자기_몸_방향도_원뿔_안에_들어온다()
    {
        // 결함의 재현: 시선을 54도 내리면 바로 아래(-Y)가 원뿔 안이다.
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Vector3 시선 = Quaternion.AngleAxis(54f, Vector3.right) * Vector3.forward;
        Assert.IsTrue(MeleeTargeting.IsWithinCone(시선, Vector3.down, 한계),
                      "원뿔만으로는 자기 몸을 걸러내지 못한다 — 그래서 루트 비교가 필요하다");
    }

    // ── 겨냥점 고르기: 한가운데가 아니라 가장 가까운 자리 ───────────────
    //
    // 대상을 점 하나로 줄이면 어디를 고르든 억울한 일이 생긴다. 예전에는 콜라이더
    // 경계 상자의 한가운데만 봤는데, 거대 버섯처럼 큰 통짜 메시는 그 중심이 머리 위
    // 빈 공간이라 밑동 앞에 서서 휘둘러도 원뿔 밖이었다. 지형에 반쯤 파묻힌 광맥도
    // 중심이 흙 속으로 내려가 드러난 부분을 똑바로 보고 빗나갔다.
    //
    // 여기 규칙: 구할 수 있는 겨냥점 중 하나라도 원뿔 안에 들면 닿은 것으로 본다.
    // 한가운데는 후보로 남겨 두므로 판정은 후해지기만 하고 짜지지 않는다.

    [Test]
    public void 한가운데가_원뿔_안이면_예전처럼_닿는다()
    {
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Vector3 원점 = Vector3.zero;
        Vector3 최근접 = 원점;                    // 구하지 못한 셈 치고 원점을 그대로
        Vector3 한가운데 = new Vector3(0f, 0f, 5f);

        Assert.IsTrue(MeleeTargeting.TrySelectAim(Vector3.forward, 한계, 원점,
                                                  최근접, 한가운데, out Vector3 겨냥));
        Assert.AreEqual(한가운데, 겨냥);
    }

    [Test]
    public void 한가운데는_머리_위여도_밑동이_원뿔_안이면_닿는다()
    {
        // 거대 버섯의 재현: 밑동은 눈앞이지만 통짜 메시의 중심은 6m 위다.
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Vector3 원점 = Vector3.zero;
        Vector3 밑동 = new Vector3(0f, -0.3f, 2f);
        Vector3 머리위 = new Vector3(0f, 6f, 2f);

        Assert.IsFalse(MeleeTargeting.IsWithinCone(Vector3.forward, 머리위 - 원점, 한계),
                       "한가운데만 보던 예전 판정은 여기서 빗나갔다");
        Assert.IsTrue(MeleeTargeting.TrySelectAim(Vector3.forward, 한계, 원점,
                                                  밑동, 머리위, out Vector3 겨냥));
        Assert.AreEqual(밑동, 겨냥);
    }

    [Test]
    public void 파묻힌_대상은_드러난_쪽으로_겨냥한다()
    {
        // 광맥의 재현: 중심은 흙 속이라 시선에서 벗어나 있고, 드러난 윗면은 눈앞이다.
        float 한계 = MeleeTargeting.ConeCosLimit(60f);
        Vector3 원점 = new Vector3(0f, 1.6f, 0f);
        Vector3 시선 = Vector3.forward;
        Vector3 윗면 = new Vector3(0f, 1.0f, 1.5f);
        Vector3 중심 = new Vector3(0f, -0.5f, 1.5f);

        Assert.IsFalse(MeleeTargeting.IsWithinCone(시선, 중심 - 원점, 한계));
        Assert.IsTrue(MeleeTargeting.TrySelectAim(시선, 한계, 원점, 윗면, 중심, out Vector3 겨냥));
        Assert.AreEqual(윗면, 겨냥);
    }

    [Test]
    public void 최근접점이_먼저다()
    {
        // 둘 다 원뿔 안이면 더 정확한 쪽을 고른다 — 타격 지점이 표면에 찍혀야 한다.
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Vector3 원점 = Vector3.zero;
        Vector3 최근접 = new Vector3(0f, 0f, 1f);
        Vector3 한가운데 = new Vector3(0f, 0f, 3f);

        Assert.IsTrue(MeleeTargeting.TrySelectAim(Vector3.forward, 한계, 원점,
                                                  최근접, 한가운데, out Vector3 겨냥));
        Assert.AreEqual(최근접, 겨냥);
    }

    [Test]
    public void 둘_다_원뿔_밖이면_닿지_않는다()
    {
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Vector3 원점 = Vector3.zero;

        Assert.IsFalse(MeleeTargeting.TrySelectAim(Vector3.forward, 한계, 원점,
                                                   방향(120f) * 2f, 방향(150f) * 3f, out _));
    }

    [Test]
    public void 뒤쪽은_최근접점으로도_닿지_않는다()
    {
        // 판정이 후해졌다고 등 뒤가 열리면 안 된다. 최근접점은 언제나 등 뒤 대상의
        // 앞면이라 원점에 더 가깝지만, 그래도 원뿔 밖이다.
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Vector3 원점 = Vector3.zero;
        Vector3 앞면 = new Vector3(0f, 0f, -1f);
        Vector3 중심 = new Vector3(0f, 0f, -3f);

        Assert.IsFalse(MeleeTargeting.TrySelectAim(Vector3.forward, 한계, 원점,
                                                   앞면, 중심, out _));
    }

    [Test]
    public void 겨냥점을_못_구하면_한가운데로_돌아간다()
    {
        // 오목한 메시나 원점이 대상 안에 든 경우 — 최근접점은 원점 그대로 돌아온다.
        // 방향이 서지 않으므로 예전 겨냥점만 남는다.
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Vector3 원점 = new Vector3(3f, 1.6f, -2f);

        Assert.IsFalse(MeleeTargeting.TrySelectAim(Vector3.forward, 한계, 원점,
                                                   원점, 원점 + Vector3.back * 4f, out _));
        Assert.IsTrue(MeleeTargeting.TrySelectAim(Vector3.forward, 한계, 원점,
                                                  원점, 원점 + Vector3.forward * 4f, out Vector3 겨냥));
        Assert.AreEqual(원점 + Vector3.forward * 4f, 겨냥);
    }

    [Test]
    public void 못_골랐어도_겨냥점은_한가운데로_채워_둔다()
    {
        // 부르는 쪽이 실수로 써도 원점이 튀어나오지 않게.
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Vector3 한가운데 = new Vector3(0f, 0f, -5f);

        MeleeTargeting.TrySelectAim(Vector3.forward, 한계, Vector3.zero,
                                    Vector3.zero, 한가운데, out Vector3 겨냥);
        Assert.AreEqual(한가운데, 겨냥);
    }

    [Test]
    public void 예전에_맞던_것은_전부_그대로_맞는다()
    {
        // 불변식을 각도로 훑는다. 한가운데가 원뿔 안이면 최근접점이 무엇이든 닿는다.
        float 한계 = MeleeTargeting.ConeCosLimit(90f);
        Vector3 원점 = Vector3.zero;

        for (float 각 = -44f; 각 <= 44f; 각 += 4f)
        {
            Vector3 한가운데 = 방향(각) * 3f;
            foreach (var 최근접 in new[] { 원점, 방향(각 + 90f) * 1f, 방향(180f) * 0.5f })
            {
                Assert.IsTrue(MeleeTargeting.TrySelectAim(Vector3.forward, 한계, 원점,
                                                          최근접, 한가운데, out _),
                              $"{각}도의 한가운데는 예전에도 닿았다");
            }
        }
    }

    // ── 자기 몸 판정 ────────────────────────────────────────────────────

    [Test]
    public void 같은_계층의_자식은_자기_몸이다()
    {
        var 몸 = 오브젝트("플레이어");
        var 머리 = 오브젝트("카메라");
        머리.transform.SetParent(몸.transform);

        Assert.IsTrue(MeleeTargeting.BelongsToSelf(머리.transform, 몸.transform));
        Assert.IsTrue(MeleeTargeting.BelongsToSelf(몸.transform, 머리.transform));
    }

    [Test]
    public void 자기_자신도_자기_몸이다()
    {
        var 몸 = 오브젝트("플레이어");
        Assert.IsTrue(MeleeTargeting.BelongsToSelf(몸.transform, 몸.transform));
    }

    [Test]
    public void 다른_계층은_남이다()
    {
        var 몸 = 오브젝트("플레이어");
        var 생물 = 오브젝트("생물");
        Assert.IsFalse(MeleeTargeting.BelongsToSelf(몸.transform, 생물.transform));
    }

    [Test]
    public void 없는_것은_자기_몸이_아니다()
    {
        var 몸 = 오브젝트("플레이어");
        Assert.IsFalse(MeleeTargeting.BelongsToSelf(몸.transform, null));
        Assert.IsFalse(MeleeTargeting.BelongsToSelf(null, 몸.transform));
    }

    // ── 때리는 쪽: 자기 몸은 대상에서 빠진다 ────────────────────────────

    [Test]
    public void 자기_계층에_붙은_대상은_때리지_않는다()
    {
        var 몸 = 오브젝트("플레이어");
        var 손 = 오브젝트("도구");
        손.transform.SetParent(몸.transform);
        var 나 = 몸.AddComponent<대상역할>();

        Assert.IsTrue(MeleeTargeting.IsSelfTarget(손.transform, 나));
    }

    [Test]
    public void 남의_대상은_그대로_때린다()
    {
        var 몸 = 오브젝트("플레이어");
        var 생물 = 오브젝트("생물");
        var 남 = 생물.AddComponent<대상역할>();

        Assert.IsFalse(MeleeTargeting.IsSelfTarget(몸.transform, 남));
    }

    [Test]
    public void 씬에_없는_구현체는_남으로_본다()
    {
        var 몸 = 오브젝트("플레이어");
        Assert.IsFalse(MeleeTargeting.IsSelfTarget(몸.transform, new 몸없는대상()));
    }

    [Test]
    public void 대상이_없으면_자기_몸도_아니다()
    {
        var 몸 = 오브젝트("플레이어");
        Assert.IsFalse(MeleeTargeting.IsSelfTarget(몸.transform, null));
    }

    // ── 가시선: 벽 너머는 때리지 않는다 ─────────────────────────────────
    //
    // 백로그 28. 판정이 구(球)라 얇은 벽 하나쯤은 그냥 지나쳐, 방 밖에 서서
    // 안에 있는 것을 때릴 수 있었다. 시전 지점과 대상 사이에 무엇이 걸렸는지는
    // 물리가 알려 주고, 그것을 "가로막은 것"으로 볼지는 여기서 정한다.

    [Test]
    public void 사이에_낀_세운_벽은_타격을_가로막는다()
    {
        var 나 = 오브젝트("플레이어");
        var 대상 = 오브젝트("생물");
        var 벽 = 오브젝트("세운벽");

        Assert.IsTrue(MeleeTargeting.IsOccluder(나.transform, 대상.transform, 벽.transform, false, true));
    }

    [Test]
    public void 지형과_소품은_가로막지_않는다()
    {
        // 이 레벨은 광맥·잔해를 지형과 거대 버섯 안에 반쯤 파묻어 두었다.
        // 지형까지 벽으로 세면 캘 수 있던 것이 통째로 캘 수 없게 된다.
        var 나 = 오브젝트("플레이어");
        var 광맥 = 오브젝트("광맥");
        var 지형 = 오브젝트("섬");

        Assert.IsFalse(MeleeTargeting.IsOccluder(나.transform, 광맥.transform, 지형.transform, false, false));
    }

    [Test]
    public void 대상_자신의_표면은_가로막은_것이_아니다()
    {
        // 레이는 결국 대상에 가서 닿는다. 그것이 곧 명중이다.
        var 나 = 오브젝트("플레이어");
        var 대상 = 오브젝트("생물");

        Assert.IsFalse(MeleeTargeting.IsOccluder(나.transform, 대상.transform, 대상.transform, false, true));
    }

    [Test]
    public void 대상의_다른_콜라이더도_가로막은_것이_아니다()
    {
        // 몸통을 겨눴는데 머리 콜라이더가 먼저 걸리는 일은 흔하다.
        var 나 = 오브젝트("플레이어");
        var 대상 = 오브젝트("생물");
        var 머리 = 오브젝트("머리");
        머리.transform.SetParent(대상.transform);

        Assert.IsFalse(MeleeTargeting.IsOccluder(나.transform, 대상.transform, 머리.transform, false, true));
    }

    [Test]
    public void 자기_몸은_가로막은_것이_아니다()
    {
        // 판정 원점이 머릿속이라 자기 CharacterController가 늘 먼저 걸린다.
        var 나 = 오브젝트("플레이어");
        var 손 = 오브젝트("들고있는곡괭이");
        손.transform.SetParent(나.transform);
        var 대상 = 오브젝트("광맥");

        Assert.IsFalse(MeleeTargeting.IsOccluder(나.transform, 대상.transform, 손.transform, false, true));
    }

    [Test]
    public void 트리거는_가로막지_않는다()
    {
        // 풀숲·채집 범위 같은 것은 통과해 지나가는 것이지 벽이 아니다.
        var 나 = 오브젝트("플레이어");
        var 대상 = 오브젝트("생물");
        var 풀숲 = 오브젝트("풀숲");

        Assert.IsFalse(MeleeTargeting.IsOccluder(나.transform, 대상.transform, 풀숲.transform, true, true));
    }

    [Test]
    public void 걸린_것이_없으면_가로막히지_않는다()
    {
        var 나 = 오브젝트("플레이어");
        var 대상 = 오브젝트("생물");

        Assert.IsFalse(MeleeTargeting.IsOccluder(나.transform, 대상.transform, null, false, true));
    }

    [Test]
    public void 대상을_모르면_사이에_낀_것은_가로막은_것으로_본다()
    {
        // 보수적으로 — 대상 판별이 안 되는 상황에서 벽을 통과시킬 이유는 없다.
        var 나 = 오브젝트("플레이어");
        var 벽 = 오브젝트("벽");

        Assert.IsTrue(MeleeTargeting.IsOccluder(나.transform, null, 벽.transform, false, true));
    }

    [Test]
    public void 한_몸인지는_루트로_가린다()
    {
        var 몸 = 오브젝트("생물");
        var 다리 = 오브젝트("다리");
        다리.transform.SetParent(몸.transform);
        var 남 = 오브젝트("바위");

        Assert.IsTrue(MeleeTargeting.SharesRoot(몸.transform, 다리.transform));
        Assert.IsFalse(MeleeTargeting.SharesRoot(몸.transform, 남.transform));
        Assert.IsFalse(MeleeTargeting.SharesRoot(몸.transform, null));
        Assert.IsFalse(MeleeTargeting.SharesRoot(null, 몸.transform));
    }

    // ── 맞는 쪽: 자기가 낸 피해만 무시한다 ──────────────────────────────

    [Test]
    public void 자기가_낸_피해는_자기에게_들어오지_않는다()
    {
        var 몸 = 오브젝트("플레이어");
        var 손 = 오브젝트("휘두르는손");
        손.transform.SetParent(몸.transform);

        Assert.IsTrue(MeleeTargeting.IsSelfInflicted(몸.transform, 손));
    }

    [Test]
    public void 생물이_낸_피해는_그대로_들어온다()
    {
        var 몸 = 오브젝트("플레이어");
        var 생물 = 오브젝트("생물");

        Assert.IsFalse(MeleeTargeting.IsSelfInflicted(몸.transform, 생물));
    }

    [Test]
    public void 가해자를_모르는_피해는_막지_않는다()
    {
        var 몸 = 오브젝트("플레이어");
        Assert.IsFalse(MeleeTargeting.IsSelfInflicted(몸.transform, null));
    }

    // ── 테스트용 대역 ───────────────────────────────────────────────────

    class 대상역할 : MonoBehaviour, IDamageable
    {
        public bool IsDead => false;
        public void TakeDamage(in DamageInfo info) { }
    }

    class 몸없는대상 : IDamageable
    {
        public bool IsDead => false;
        public void TakeDamage(in DamageInfo info) { }
    }
}
