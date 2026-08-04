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
