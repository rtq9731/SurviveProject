using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Localization;

/// <summary>
/// <b>낫이 무슨 모양인지를 글자 쪽에서 못 박는 게이트</b> (2026-08-07 회신 ⑫).
///
/// <b>왜 필요한가.</b> 낫은 설계가 두 번 바뀌었다. 처음에는 <b>빠르게 달리는 두 다리</b>였고,
/// 지금은 <b>둥근 몸통이 떠서 꼬리로 액면을 훑는 것</b>이다. 모델과 행동은 새 설계로
/// 갔는데 <b>글자만 옛 몸을 들고 남았다</b> — 도감은 고쳤는데 아이템 설명문과 연구
/// 대사가 여전히 "어깨에서 뻗어 있던", "고속 이족 보행체의"라고 말하고 있었다.
///
/// 이런 어긋남은 아무 오류도 내지 않는다. 화면에는 다리 없는 기계가 떠다니고
/// 도감 옆 설명문은 그 기계에 어깨가 있다고 적혀 있는데, 그 둘을 같은 화면에서
/// 보는 것은 <b>플레이어뿐</b>이다. 그래서 기계가 대신 본다.
///
/// <b>왜 저장소 전체를 훑지 않는가.</b> 「어깨」도 「이족」도 그 자체로는 멀쩡한 말이다 —
/// 다른 개체가 어깨를 가질 수 있고, 실제로 걷는 기계가 나올 수도 있다. 막으려는 것은
/// 낱말이 아니라 <b>낫에게 붙은 옛 몸</b>이라, 훑는 자리를 낫의 글자로 좁힌다.
/// 넓게 잡은 게이트는 멀쩡한 글을 잡고, 멀쩡한 글을 잡는 게이트는 곧 예외 목록을 낳는다.
/// </summary>
public class ScytheFormGateTests
{
    /// <summary>낫의 글자를 고르는 표식. 열쇠에 이것이 들어 있으면 낫의 것이다.</summary>
    const string 낫표식 = "scythe";

    /// <summary>
    /// 옛 몸의 흔적. <b>조각으로 짓는다</b> — 이 파일 자신이 검사에 걸리면 안 되고,
    /// 저장소를 훑는 다른 게이트가 여기서 낱말을 주워 가지도 않게.
    ///
    /// 「다리」는 넣지 않았다. 지금 도감이 "<b>다리가</b> 관측되지 않는 기계"라고
    /// 적고 있고, 없다는 말을 하려면 그 낱말을 써야 한다. 막으려는 것은
    /// <b>다리가 있다고 말하는 것</b>이다.
    /// </summary>
    static readonly string[] 옛몸 =
    {
        "어" + "깨",        // 팔이 달려 있었다는 말
        "이" + "족",        // 두 다리
        "사" + "족",        // 네 다리도 아니다
        "보" + "행체",      // 걸어 다니는 몸. 「액면 보행 장비」는 걸리지 않는다
        "두 " + "다리",
        "걸" + "어 다니",
    };

    // ── ① 낫의 글자에 옛 몸이 없다 ──────────────────────────────

    [Test]
    public void 낫의_모든_글자에_이족보행의_흔적이_없다()
    {
        var 걸린것 = 걸리는_자리(낫의_모든_글자(), 옛몸);

        Assert.IsEmpty(걸린것,
            $"낫의 글자가 아직 옛 몸을 말한다 ({걸린것.Count}군데). " +
            "지금 낫은 둥근 몸통이 떠서 꼬리로 액면을 훑는 것이다 (회신 ⑫):\n  " +
            string.Join("\n  ", 걸린것));
    }

    // ── ② 음성 확인 — 훑개가 살아 있는가 ────────────────────────

    /// <summary>
    /// 위 검사는 초록불일 때 아무 말도 하지 않는다. 모으는 쪽이 조용히 망가져 늘 빈
    /// 목록을 내도 통과한다. <b>훑는 자리에 진짜 낫의 글자가 들어 있는지</b>를 못 박는다.
    /// </summary>
    [Test]
    public void 훑는_자리에_진짜_낫의_글자가_들어_있다()
    {
        var 글들 = 낫의_모든_글자();

        Assert.Greater(글들.Count, 6, "모은 것이 너무 적다 — 낫의 글자를 못 읽었을 수 있다");

        foreach (var 열쇠 in new[] { "scythe.codex", "part_scythe.desc", "res_codex_scythe.line.text" })
            Assert.IsTrue(글들.Any(g => g.자리.Contains(열쇠)),
                          $"{열쇠}를 못 담았다 — 훑개가 그만큼 눈이 멀었다:\n  " +
                          string.Join("\n  ", 글들.Select(g => g.자리)));

        Assert.IsTrue(글들.Any(g => g.자리.StartsWith("에셋")), "에셋 쪽을 못 담았다");
        Assert.IsTrue(글들.Any(g => g.자리.StartsWith("표")), "번역 표 쪽을 못 담았다");

        // 화면에 실제로 나가는 것은 표 쪽이다. 두 길이 같은 것을 보는지도 같이 본다.
        Assert.IsTrue(글들.Any(g => g.글.Contains("꼬리")),
                      "지금 낫의 몸을 말하는 글이 하나도 없다 — 새 설명이 사라졌는가");
    }

    /// <summary><b>음성 확인 둘.</b> 옛 몸을 일부러 되살려 놓고 게이트가 무너지는지 본다.</summary>
    [Test]
    public void 옛_몸을_되살리면_게이트가_무너진다()
    {
        foreach (var 말 in 옛몸)
        {
            var 시험체 = new[] { (자리: "시험체", 글: $"낫의 {말} 부분이 관측되었다.") };
            Assert.IsNotEmpty(걸리는_자리(시험체, 옛몸),
                $"\"{말}\"을 되살렸는데 게이트가 잡지 못한다 — 0건은 아무 뜻이 없다");
        }
    }

    /// <summary>
    /// <b>멀쩡한 말을 잡지 않는가.</b> 목록이 넓어지면 다음 사람은 글을 고치지 않고
    /// 목록을 깎는다. 지금 실제로 쓰이는, 걸리면 안 되는 말들을 검사가 알고 있게 한다.
    /// </summary>
    [Test]
    public void 옛몸_목록이_멀쩡한_말을_잡지_않는다()
    {
        var 멀쩡한것 = new[]
        {
            "다리가 관측되지 않는 기계. 걷지 않고 액면에 붙어 미끄러진다.",
            "액면 보행 장비를 걸치면 그 위를 걷는다.",   // 「보행」은 살아 있는 말이다
            "뒤로 늘어뜨린 꼬리가 표면을 훑고 지나간다.",
        };

        var 시험체 = 멀쩡한것.Select((s, i) => (자리: $"멀쩡한 글 {i}", 글: s)).ToArray();

        Assert.IsEmpty(걸리는_자리(시험체, 옛몸),
            "옛 몸 목록이 멀쩡한 글을 잡는다 — 목록이 너무 넓다");
    }

    // ── ③ 훑개 ──────────────────────────────────────────────────

    /// <summary>
    /// 낫의 글자 전부. <b>에셋과 표 두 길로 본다</b> — 화면에 나가는 것은 표 쪽이고
    /// 에셋은 폴백이라, 한쪽만 고친 되살림은 두 길을 다 봐야 잡힌다.
    /// </summary>
    static List<(string 자리, string 글)> 낫의_모든_글자()
    {
        var 모은것 = new List<(string, string)>();

        foreach (var e in DataTextAssets.CollectAll())
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Korean)) continue;
            if (e.Key == null || e.Key.IndexOf(낫표식, StringComparison.OrdinalIgnoreCase) < 0) continue;
            모은것.Add(($"에셋 {e.Category},{e.Key} ({AssetDatabase.GetAssetPath(e.Owner)})", e.Korean));
        }

        var catalog = LocalizationTestBootstrap.LoadCatalogFromDisk();
        foreach (var locale in catalog.Locales)
        {
            var table = catalog.TableFor(locale);
            foreach (var pair in table)
            {
                if (pair.Key.Key == null ||
                    pair.Key.Key.IndexOf(낫표식, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (string.IsNullOrWhiteSpace(pair.Value)) continue;
                모은것.Add(($"표 {locale} {pair.Key}", pair.Value));
            }
        }

        return 모은것.Select(t => (자리: t.Item1, 글: t.Item2)).ToList();
    }

    static List<string> 걸리는_자리(IEnumerable<(string 자리, string 글)> 글들, string[] 금지어)
    {
        var 걸린것 = new List<string>();
        foreach (var g in 글들)
        {
            if (string.IsNullOrEmpty(g.글)) continue;
            foreach (var 말 in 금지어)
                if (g.글.IndexOf(말, StringComparison.OrdinalIgnoreCase) >= 0)
                    걸린것.Add($"{g.자리}: \"{g.글.Replace("\n", " / ")}\" ← {말}");
        }
        return 걸린것;
    }
}
