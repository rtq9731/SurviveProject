using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Survive.Domain.Art;

/// <summary>
/// <b>빛기둥은 없어지고 색만 남았다 (2026-08-07).</b>
///
/// 무대가 지상으로 나오면서 그 물건의 근거 셋이 한꺼번에 없어졌다 — 천장도 없고,
/// 떨어져서 들어온 것도 아니고, 걸러진 지표광이라는 설명도 폐기됐다. 하던 일도
/// 전부 넘어갔다: 밝기의 근거는 <b>낮</b>이고 랜드마크는 <b>호수</b>다.
///
/// <b>이 게이트가 지키는 것은 「없어졌다」와 「남았다」 둘이다.</b> 물건은 씬에서
/// 사라져야 하고 색 <c>#E8D5A8</c>은 그대로 있어야 한다. 색까지 같이 지우면
/// 광원 4색 규칙에서 한 칸이 비어 <c>MaterialRule</c>·<c>LightRule</c>이 통째로
/// 흔들린다 — 지상에서 그 색은 곧 햇빛이므로 지울 이유도 없다.
///
/// <b>그리고 대상 0인 검사를 남기지 않았는지 본다.</b> 고정 조명 자동 등록 규칙은
/// 빛기둥이 유일한 대상이었다. 그것이 없어진 뒤에도 규칙을 남겨 두면, 통과해도
/// 아무 뜻이 없으면서 다음 사람에게는 <b>"고정 조명이 자동 등록된다"는 거짓 약속</b>을
/// 준다. 그 약속을 믿고 씬에 광원을 놓는 사람이 나오는 날 조용히 어긋난다.
/// </summary>
public class LightShaftRetirementTests
{
    const string MainScenePath = "Assets/01.Scenes/MainScene.unity";
    const string StartScenePath = "Assets/01.Scenes/StartScene.unity";
    const string 빛기둥프리팹 = "Assets/05.Prefabs/Environment/LightShaft.prefab";
    const string 빛기둥셰이더 = "Survive/LightShaft";

    // ── 물건은 없어졌다 ─────────────────────────────────────

    [Test]
    public void 씬에_빛기둥이_하나도_없다()
    {
        var 남은것 = new List<string>();
        foreach (var 경로 in new[] { MainScenePath, StartScenePath })
            남은것.AddRange(씬에서_찾는다(경로));

        Assert.IsEmpty(남은것,
            "빛기둥이 씬에 남아 있다. 근거 셋이 없어졌고 하던 일도 전부 넘어갔다 " +
            "(기획서 §7.8):\n  " + string.Join("\n  ", 남은것));
    }

    [Test]
    public void 씬_파일에_빛기둥_프리팹_참조가_없다()
    {
        // 위의 검사는 <b>열린 씬</b>을 본다. 프리팹이 깨져 있으면 오브젝트가
        // 안 만들어져서 통과할 수 있으므로, 파일 자체도 한 번 훑는다.
        string guid = AssetDatabase.AssetPathToGUID(빛기둥프리팹);
        Assert.IsNotEmpty(guid, 빛기둥프리팹 + " 를 찾지 못했다 — 검사가 헛돌고 있다");

        foreach (var 경로 in new[] { MainScenePath, StartScenePath })
        {
            string 본문 = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), 경로));
            Assert.IsFalse(본문.Contains(guid), $"{경로}에 빛기둥 프리팹 참조가 남아 있다");
        }
    }

    // ── 색은 남았다 ─────────────────────────────────────────

    [Test]
    public void 지표광_회백은_값_그대로다()
    {
        Assert.AreEqual(ArtPalette.FromHex(0xE8D5A8), ArtPalette.LightShaft,
                        "지표광 색이 움직였다. 물건은 없어져도 색은 남는다");
    }

    [Test]
    public void 지표광은_여전히_광원_4색_안에_있다()
    {
        Assert.Contains(ArtPalette.LightShaft, ArtPalette.AllowedEmission.ToArray(),
                        "네 색 규칙에서 한 칸이 비면 MaterialRule과 LightRule이 함께 흔들린다");
    }

    [Test]
    public void 그_색은_이제_햇빛이다()
    {
        // 색이 남은 근거가 "지우기 귀찮아서"가 아니라 "지상에서 그것이 햇빛이라서"임을
        // 코드로 붙들어 둔다. 태양이 다른 색을 쓰기 시작하면 여기가 빨개진다.
        Assert.AreEqual(ArtPalette.LightShaft, Survive.World.DayNightCycle.SunColor);
    }

    // ── 대상 0인 검사를 남기지 않았다 ────────────────────────

    [Test]
    public void 고정_조명_자동_등록_규칙이_저장소에서_사라졌다()
    {
        // 낱말을 조각으로 잇는다 — 이 파일 자신이 걸리면 검사가 제 꼬리를 문다.
        var 사라진것 = new[] { "FixedLight" + "Rule", "FixedLight" + "Zone" };

        var 걸린것 = AssetTextScan.찾는다(사라진것);
        Assert.IsEmpty(걸린것,
            "빛기둥이 유일한 대상이던 규칙이 아직 남아 있다. 대상 0인 검사는 " +
            "통과해도 아무 뜻이 없으면서 다음 사람에게 거짓 약속을 준다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    [Test]
    public void 그_규칙의_테스트_파일도_함께_사라졌다()
    {
        string 경로 = Path.Combine(Directory.GetCurrentDirectory(),
                                   "Assets/09.Tests/EditMode", "FixedLight" + "RuleTests.cs");
        Assert.IsFalse(File.Exists(경로), "규칙은 지웠는데 그 테스트가 남아 있다");
    }

    [Test]
    public void 검사가_비어_돌지_않는다()
    {
        // 위 두 검사는 "없다"만 말한다. 조각을 잇다 오타를 내면 아무것도 안 물면서
        // 초록불이 되므로, 그 말이 실제로 무는지를 한 번 확인한다.
        foreach (var 말 in new[] { "FixedLight" + "Rule", "FixedLight" + "Zone" })
            Assert.IsTrue(AssetTextScan.걸리는가($"앞말 {말} 뒷말", 말, false),
                          $"「{말}」이 자기가 들어 있는 문장도 못 잡는다");
    }

    // ── 훑개 ────────────────────────────────────────────────

    static IEnumerable<string> 씬에서_찾는다(string 경로)
    {
        var 이전활성 = SceneManager.GetActiveScene();
        var 씬 = 연다(경로, out bool 이미열려있었다);
        var 걸린것 = new List<string>();
        try
        {
            Assert.IsTrue(씬.IsValid() && 씬.isLoaded, $"씬을 열지 못했다: {경로}");

            foreach (var 루트 in 씬.GetRootGameObjects())
            {
                foreach (var t in 루트.GetComponentsInChildren<Transform>(true))
                    if (t.name.Contains("LightShaft") || t.name.Contains("빛기둥"))
                        걸린것.Add($"{경로}: 오브젝트 '{Path이름(t)}'");

                foreach (var r in 루트.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && m.shader != null && m.shader.name == 빛기둥셰이더)
                            걸린것.Add($"{경로}: '{Path이름(r.transform)}'가 빛기둥 셰이더를 쓴다");
            }
        }
        finally
        {
            SceneManager.SetActiveScene(이전활성);
            if (!이미열려있었다) EditorSceneManager.CloseScene(씬, true);
        }
        return 걸린것;
    }

    /// <summary>
    /// 이미 열려 있는 씬은 그대로 쓴다 — 사람의 미저장 편집을 밟지 않기 위해서다
    /// (<c>SceneArtSettingsTests</c>와 같은 규약).
    /// </summary>
    static Scene 연다(string 경로, out bool 이미열려있었다)
    {
        var 있던것 = SceneManager.GetSceneByPath(경로);
        if (있던것.IsValid() && 있던것.isLoaded)
        {
            이미열려있었다 = true;
            return 있던것;
        }
        이미열려있었다 = false;
        return EditorSceneManager.OpenScene(경로, OpenSceneMode.Additive);
    }

    static string Path이름(Transform t)
    {
        string s = t.name;
        for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
        return s;
    }
}
