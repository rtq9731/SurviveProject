using System.IO;
using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;

/// <summary>
/// <b>너울은 구역을 정하지 않는다.</b>
///
/// <c>HoverDrifter</c>는 몸을 ±0.1m 위아래로 흔든다(액면의 느린 물결). 그 값이
/// 오래 <c>transform.position.y</c>에 그대로 들어갔고, 그 y가 <c>Sample</c>의 지형
/// 광선 시작 높이가 되어 <b>구역 판정으로 새어 들었다</b> — 물가에서 위로 까딱인
/// 프레임에 광선이 한 뼘 높은 데서 출발해 발밑에 없는 것을 지형으로 읽었다.
/// 「낫 꼬리」가 세던 <b>「육지로 읽힌 프레임」</b>이 그 자리다.
///
/// <b>위상을 시드로 고정한 것이 원인이 아니었다.</b> 그것은 결함을 <b>드러냈을 뿐</b>이다 —
/// 늘 같은 위상으로 시작하면 늘 같은 프레임에 흔들린다. 그래서 시드를 되돌리지 않고
/// <b>커플링을 끊었다.</b> 무작위로 돌려놓으면 흔들림이 판마다 다른 자리에서 되살아난다.
///
/// 여기서 재는 것은 <b>규칙 쪽 성질</b>이다 — 잰 값이 같으면 답도 같아야 하고,
/// 몸 쪽에서는 그 잰 값에 너울이 섞이지 않아야 한다.
/// </summary>
public class SwellDoesNotDecideZoneTests
{
    const float 액면 = 50.1f;

    [Test]
    public void 같은_지형이면_몸이_어느_높이에_있든_같은_구역이다()
    {
        // Classify는 몸의 높이를 아예 받지 않는다. 그것이 규칙 쪽 보증이다.
        float 물가지형 = 액면 + ScytheHabitat.ShoreRise * 0.5f;

        var a = ScytheHabitat.Classify(true, 액면, true, 물가지형);
        var b = ScytheHabitat.Classify(true, 액면, true, 물가지형);

        Assert.AreEqual(a, b);
        Assert.AreEqual(HabitatZone.Shore, a);
    }

    [Test]
    public void 너울만큼의_지형_차이가_구역을_가른다()
    {
        // <b>왜 0.1m가 문제였는지를 수로 남긴다.</b> 해안선 윗선 바로 아래와
        // 바로 위는 0.1m 차이로 갈린다 — 광선 시작 높이가 그만큼 흔들리면
        // 읽히는 지형이 바뀔 수 있다는 뜻이다.
        float 윗선 = 액면 + ScytheHabitat.ShoreRise;

        Assert.AreEqual(HabitatZone.Shore, ScytheHabitat.Classify(true, 액면, true, 윗선 - 0.05f));
        Assert.AreEqual(HabitatZone.Inland, ScytheHabitat.Classify(true, 액면, true, 윗선 + 0.05f));
    }

    [Test]
    public void 몸이_잰_값에_너울을_섞지_않는다()
    {
        // <b>본문을 훑어 커플링이 되살아나는 것을 막는다.</b> 되돌리기는 쉽고
        // 되살리기는 더 쉽다 — 다음 사람이 "한 줄인데" 하며 합친다.
        string 본문 = File.ReadAllText(Path.Combine(
            Application.dataPath, "02.Scripts/Creatures/HoverDrifter.cs"));

        int 재는곳 = 본문.IndexOf("Sampled Sample(", System.StringComparison.Ordinal);
        Assert.Greater(재는곳, 0, "재는 자리를 찾지 못했다");

        // 구역을 묻는 자리들은 전부 「딛고선자리」를 거친다.
        Assert.IsTrue(본문.Contains("Sample(딛고선자리(here))"),
                      "Update가 딛고 선 높이로 재지 않는다");
        Assert.IsTrue(본문.Contains("Sample(딛고선자리(p))"),
                      "설 수 있는지를 묻는 자리가 딛고 선 높이로 재지 않는다");

        // 그리고 딛고 선 높이 자체에는 너울이 없다.
        int 높이갱신 = 본문.IndexOf("_baseY = Mathf.Lerp", System.StringComparison.Ordinal);
        Assert.Greater(높이갱신, 0, "딛고 선 높이를 따라가는 자리를 찾지 못했다");

        string 그줄 = 본문.Substring(높이갱신, 120);
        Assert.IsFalse(그줄.Contains("swellAmplitude"),
                       "딛고 선 높이에 너울이 섞였다 — 그러면 판정으로 다시 샌다");
    }

    [Test]
    public void 너울은_보이는_몸에만_얹힌다()
    {
        string 본문 = File.ReadAllText(Path.Combine(
            Application.dataPath, "02.Scripts/Creatures/HoverDrifter.cs"));

        Assert.IsTrue(본문.Contains("next.y = _baseY + Mathf.Sin(_swellPhase) * swellAmplitude"),
                      "너울이 보이는 몸에 얹히는 자리가 없다");
    }
}
