using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Creatures;

/// <summary>
/// 낫이 어디에 있을 수 있는가 (기획서 §4.5 "서식 범위" · 스펙 §3).
///
/// <b>이 규칙 하나가 챕터 1의 뼈대를 셋 지탱한다.</b>
/// <list type="number">
/// <item>내륙과 지하가 안전한 이유 — 평시에는 육지로 올라오지 않는다</item>
/// <item>액면 보행 장비 없이 첫 유물을 얻는 길 — 해안선까지는 나오므로
///       육지에 선 채로 가장자리에서 줍는다. 순환이 여기서 풀린다</item>
/// <item>종막의 압력 — 평생 육지에 오지 않던 것이 발령에 올라온다</item>
/// </list>
///
/// 그래서 경계값이 흔들리면 안 된다. 띠가 넓어지면 내륙이 위험해지고, 좁아지면
/// 물가에서 유물을 못 주워 순환이 되살아난다. 여기서 못 박는다.
/// </summary>
public class ScytheHabitatTests
{
    const float 액면 = 50.1f;

    static HabitatZone 판정(float 지형높이) =>
        ScytheHabitat.Classify(true, 액면, true, 지형높이);

    // ── 구역 판정 ───────────────────────────────────────────

    [Test]
    public void 받칠_것이_없으면_액면_위다()
    {
        Assert.AreEqual(HabitatZone.Liquid, ScytheHabitat.Classify(true, 액면, false, 0f));
    }

    [Test]
    public void 지형이_수면_아래로_잠겨_있으면_여전히_액면_위다()
    {
        Assert.AreEqual(HabitatZone.Liquid, 판정(액면 - 20f));
        Assert.AreEqual(HabitatZone.Liquid, 판정(액면 - 0.01f));
    }

    [Test]
    public void 수면과_같은_높이는_액면_위다()
    {
        Assert.AreEqual(HabitatZone.Liquid, 판정(액면));
    }

    [Test]
    public void 수면_바로_위부터_해안선이다()
    {
        Assert.AreEqual(HabitatZone.Shore, 판정(액면 + 0.01f));
        Assert.AreEqual(HabitatZone.Shore, 판정(액면 + ScytheHabitat.ShoreRise * 0.5f));
    }

    [Test]
    public void 해안선의_윗_경계는_포함한다()
    {
        Assert.AreEqual(HabitatZone.Shore, 판정(액면 + ScytheHabitat.ShoreRise));
    }

    [Test]
    public void 해안선을_한_뼘_넘으면_육지다()
    {
        Assert.AreEqual(HabitatZone.Inland, 판정(액면 + ScytheHabitat.ShoreRise + 0.01f));
        Assert.AreEqual(HabitatZone.Inland, 판정(액면 + 8f));
    }

    [Test]
    public void 액체가_없는_기둥은_지형이_없어도_육지다()
    {
        // 섬 바깥 허공을 "액면 위"로 세면 세계 전체가 서식지가 된다.
        Assert.AreEqual(HabitatZone.Inland, ScytheHabitat.Classify(false, 0f, false, 0f));
        Assert.AreEqual(HabitatZone.Inland, ScytheHabitat.Classify(false, 0f, true, 40f));
    }

    // ── 태세별 진입 가부 ────────────────────────────────────

    [Test]
    public void 평시에_액면과_해안선은_언제나_열려_있다()
    {
        Assert.IsTrue(ScytheHabitat.CanEnter(HabitatZone.Liquid, ScytheAlert.Calm));
        Assert.IsTrue(ScytheHabitat.CanEnter(HabitatZone.Shore, ScytheAlert.Calm));
    }

    [Test]
    public void 평시에_육지_좌표는_거부한다()
    {
        Assert.IsFalse(ScytheHabitat.CanEnter(HabitatZone.Inland, ScytheAlert.Calm));
        Assert.IsFalse(ScytheHabitat.CanOccupy(true, 액면, true, 액면 + 1.45f, ScytheAlert.Calm),
                       "지상 지형 중앙값 높이");
    }

    [Test]
    public void 발령이면_육지도_허용한다()
    {
        Assert.IsTrue(ScytheHabitat.CanEnter(HabitatZone.Inland, ScytheAlert.Alarmed));
        Assert.IsTrue(ScytheHabitat.CanOccupy(true, 액면, true, 액면 + 1.45f, ScytheAlert.Alarmed));
    }

    [Test]
    public void 발령은_액면과_해안선을_막지_않는다()
    {
        // 육지로 올라온다는 것이 "물에서 나온다"는 뜻은 아니다.
        Assert.IsTrue(ScytheHabitat.CanEnter(HabitatZone.Liquid, ScytheAlert.Alarmed));
        Assert.IsTrue(ScytheHabitat.CanEnter(HabitatZone.Shore, ScytheAlert.Alarmed));
    }

    [Test]
    public void 태세로_갈리는_것은_육지_하나뿐이다()
    {
        foreach (HabitatZone zone in Enum.GetValues(typeof(HabitatZone)))
        {
            bool 평시 = ScytheHabitat.CanEnter(zone, ScytheAlert.Calm);
            bool 발령 = ScytheHabitat.CanEnter(zone, ScytheAlert.Alarmed);
            if (zone == HabitatZone.Inland) Assert.AreNotEqual(평시, 발령, zone.ToString());
            else Assert.AreEqual(평시, 발령, zone.ToString());
        }
    }

    // ── 실제 지형의 경계값 ──────────────────────────────────

    [Test]
    public void 실측한_지상_물가는_해안선이고_스폰_일대는_육지다()
    {
        // MainScene 실측 (액면 50.1): 걸을 수 있는 지형 334곳을 재니 최고 52.32,
        // 중앙값 51.55, 물가가 50.3~50.8이었다. 이 둘이 서로 다른 구역으로 갈리지
        // 않으면 규칙이 지도 위에서 아무것도 하지 않는다 — 띠가 넓으면 섬의 절반이
        // 해안선이 되어 "내륙이 안전하다"가 그냥 거짓이 된다.
        foreach (float 물가 in new[] { 50.3f, 50.4f, 50.5f, 50.7f, 50.8f })
            Assert.AreEqual(HabitatZone.Shore, 판정(물가), $"물가 {물가}");

        foreach (float 내륙 in new[] { 51.0f, 51.55f, 52.0f, 52.32f })
            Assert.AreEqual(HabitatZone.Inland, 판정(내륙), $"내륙 {내륙}");
    }

    [Test]
    public void 해안선_띠는_한_걸음_턱을_넘지_않는다()
    {
        // 넓어지면 내륙이 위험해지고, 0이면 물가에서 유물을 못 줍는다.
        // 실측한 지상 지형의 중앙값이 액면 위 1.45m이므로, 띠가 그보다 넓으면
        // 섬의 절반이 낫의 영역이 된다.
        Assert.Greater(ScytheHabitat.ShoreRise, 0f);
        Assert.Less(ScytheHabitat.ShoreRise, 1.45f);
    }

    // ── 떠 있는 높이 ────────────────────────────────────────

    [Test]
    public void 바다_위에서는_해저가_아니라_수면에_붙는다()
    {
        // 비행체의 규칙(지면 + 고도)을 그대로 쓰면 해저 47 위 0.6 = 47.6,
        // 곧 수면 아래에 잠긴다. 낫은 꼬리로 액체를 훑어야 한다.
        Assert.AreEqual(액면 + 0.6f, ScytheHabitat.FloatHeight(true, 액면, true, 47f, 0.6f), 1e-4f);
    }

    [Test]
    public void 해안선에서는_올라온_지형을_기준으로_삼는다()
    {
        Assert.AreEqual(51.0f + 0.6f,
                        ScytheHabitat.FloatHeight(true, 액면, true, 51.0f, 0.6f), 1e-4f);
    }

    [Test]
    public void 액체가_없으면_지형을_기준으로_삼는다()
    {
        Assert.AreEqual(60f + 0.6f,
                        ScytheHabitat.FloatHeight(false, 0f, true, 60f, 0.6f), 1e-4f);
    }

    // ── 데이터가 규칙과 맞는가 ──────────────────────────────

    [Test]
    public void 낫은_생태계_밖이고_부유로_적혀_있다()
    {
        var 낫 = AssetDatabase.FindAssets("t:CreatureDefinitionSO")
                              .Select(g => AssetDatabase.LoadAssetAtPath<CreatureDefinitionSO>(
                                               AssetDatabase.GUIDToAssetPath(g)))
                              .FirstOrDefault(c => c != null && c.id == "scythe");

        Assert.IsNotNull(낫, "낫 정의를 찾았다");
        Assert.AreEqual(TrophicTier.Outside, 낫.tier,
                        "다리가 없으므로 포식 차수 0 — 먹이사슬 어디에도 서지 않는다");
        Assert.AreEqual(LocomotionType.Hovering, 낫.locomotion,
                        "걷지 않고 액면 위를 미끄러진다");
    }

    [Test]
    public void 부유로_움직이는_것은_낫_하나뿐이다()
    {
        // 생태계의 것들은 전부 다리가 있다(§4.5). 다른 종이 부유로 바뀌면
        // "다리가 없다 = 생태계 밖"이라는 식별 정보가 그 순간 죽는다.
        var 부유 = AssetDatabase.FindAssets("t:CreatureDefinitionSO")
                                .Select(g => AssetDatabase.LoadAssetAtPath<CreatureDefinitionSO>(
                                                 AssetDatabase.GUIDToAssetPath(g)))
                                .Where(c => c != null && c.locomotion == LocomotionType.Hovering)
                                .Select(c => c.id)
                                .ToArray();

        CollectionAssert.AreEquivalent(new[] { "scythe" }, 부유);
    }
}
