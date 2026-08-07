using System.IO;
using NUnit.Framework;
using Survive.Core;

/// <summary>
/// <b>저장본이 앉는 자리.</b> <see cref="SaveSlotTests"/>가 「어느 슬롯에 쓰는가」를
/// 못 박는다면 여기는 「어느 폴더에 쓰는가」를 못 박는다.
///
/// <b>제일 중요한 검사는 첫 번째다</b> — 빌드에서는 경로가 한 치도 안 바뀐다.
/// 이 갈래는 <b>에디터 안의 문제</b>를 풀려고 넣은 것이고, 사람이 받는 게임의
/// 저장본이 옮겨 가면 그 순간 이어하기가 통째로 사라진다.
///
/// <b>음성 확인이 붙어 있다.</b> 갈래를 걷어내면 「에디터에서는 자리가 갈린다」와
/// 「클론끼리 다른 자리를 쓴다」가 즉시 빨개진다 — 그러지 않으면 이 검사들은
/// 늘 참인 문장을 읽고 있는 것이다.
/// </summary>
public class SaveLocationTests
{
    const string 공유 = "/data/LocalLow/DefaultCompany/SurviveProject";
    const string 메인 = @"E:\SurviveProject\SurviveProject\Assets";
    const string 클론A = @"E:\SurviveProjectClones\A\SurviveProject\SurviveProject\Assets";
    const string 클론C = @"E:\SurviveProjectClones\C\SurviveProject\SurviveProject\Assets";

    // ── 빌드는 건드리지 않는다 ────────────────────────────────

    [Test]
    public void 빌드에서는_저장_경로가_한_치도_안_바뀐다()
    {
        // 이 한 줄이 이 파일의 전부다. 조합도 덧붙임도 없이 준 것 그대로여야 한다.
        Assert.AreEqual(공유, SaveLocation.RootFor(공유, 메인, insideEditor: false));
        Assert.AreEqual(공유, SaveLocation.RootFor(공유, 클론C, insideEditor: false));

        // 프로젝트 경로가 무엇이든, 심지어 없어도 빌드는 흔들리지 않는다.
        Assert.AreEqual(공유, SaveLocation.RootFor(공유, null, insideEditor: false));
        Assert.AreEqual(공유, SaveLocation.RootFor(공유, "", insideEditor: false));
    }

    // ── 에디터는 갈린다 ───────────────────────────────────────

    [Test]
    public void 에디터에서는_공유_폴더와_다른_자리로_간다()
    {
        var root = SaveLocation.RootFor(공유, 메인, insideEditor: true);

        // 갈래를 걷어내면(RootFor가 그냥 공유를 돌려주면) 여기서 빨개진다.
        Assert.AreNotEqual(공유, root, "에디터가 공유 폴더에 그대로 쓰면 사람의 저장본이 밟힌다");
        Assert.IsTrue(SaveLocation.IsEditorFolder(Path.GetFileName(root)),
                      $"갈래 폴더 이름이 머리말로 시작해야 한다: {root}");
    }

    [Test]
    public void 갈래_폴더는_공유_폴더_아래에_있다()
    {
        var root = SaveLocation.RootFor(공유, 클론C, insideEditor: true);

        // 위로 올라가면 아무도 예상 못 한 자리에 파일이 생긴다.
        Assert.IsTrue(root.StartsWith(공유), $"공유 폴더 밖으로 나갔다: {root}");
        Assert.AreEqual(공유, Path.GetDirectoryName(root)?.Replace('\\', '/'),
                        "갈래는 공유 폴더 바로 아래 한 칸이어야 한다");
    }

    [Test]
    public void 클론마다_다른_자리를_쓴다()
    {
        var 메인자리 = SaveLocation.RootFor(공유, 메인, insideEditor: true);
        var A자리 = SaveLocation.RootFor(공유, 클론A, insideEditor: true);
        var C자리 = SaveLocation.RootFor(공유, 클론C, insideEditor: true);

        // 이것이 이 라운드가 산 것이다 — 클론이 무엇을 저장하든 메인에 닿지 못한다.
        Assert.AreNotEqual(메인자리, A자리);
        Assert.AreNotEqual(메인자리, C자리);
        Assert.AreNotEqual(A자리, C자리);
    }

    // ── 같은 프로젝트는 언제나 같은 자리 ──────────────────────

    [Test]
    public void 같은_프로젝트는_언제_물어도_같은_자리다()
    {
        // 자리가 흔들리면 에디터를 껐다 켤 때마다 이어하기가 사라진 것처럼 보인다.
        // 그래서 해시를 손으로 적었다 — string.GetHashCode는 세션마다 달라질 수 있다.
        var 처음 = SaveLocation.EditorFolderFor(클론C);
        for (int i = 0; i < 5; i++)
            Assert.AreEqual(처음, SaveLocation.EditorFolderFor(클론C));
    }

    [Test]
    public void 경로_표기가_달라도_같은_자리다()
    {
        var 기준 = SaveLocation.EditorFolderFor(@"E:\SurviveProject\SurviveProject\Assets");

        // Unity가 어느 표기를 주는지는 부르는 자리에 달렸다.
        Assert.AreEqual(기준, SaveLocation.EditorFolderFor("E:/SurviveProject/SurviveProject/Assets"));
        Assert.AreEqual(기준, SaveLocation.EditorFolderFor("E:/SurviveProject/SurviveProject/Assets/"));
        Assert.AreEqual(기준, SaveLocation.EditorFolderFor("e:/surviveproject/surviveproject/assets"));

        // dataPath(Assets까지)와 프로젝트 루트가 같은 자리를 얻어야 한다.
        Assert.AreEqual(기준, SaveLocation.EditorFolderFor("E:/SurviveProject/SurviveProject"));
    }

    [Test]
    public void 갈래_폴더_이름은_파일_이름으로_쓸_수_있다()
    {
        var name = SaveLocation.EditorFolderFor(클론C);

        Assert.AreEqual(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()),
                        $"폴더 이름에 못 쓰는 글자가 들어 있다: {name}");
        Assert.IsFalse(name.Contains(" "), "빈칸이 들어가면 경로를 따옴표 없이 못 넘긴다");
    }

    [Test]
    public void 머리말이_없으면_갈래_폴더가_아니다()
    {
        Assert.IsTrue(SaveLocation.IsEditorFolder(SaveLocation.EditorFolderFor(클론C)));
        Assert.IsFalse(SaveLocation.IsEditorFolder("save_auto.json"));
        Assert.IsFalse(SaveLocation.IsEditorFolder(""));
        Assert.IsFalse(SaveLocation.IsEditorFolder(null));
    }

    // ── 두 규칙은 포개진다 ────────────────────────────────────

    [Test]
    public void 폴더가_갈려도_슬롯_규칙은_그대로다()
    {
        // 자리를 가른 것이 슬롯 격리를 대신하지는 않는다. 한 에디터 안에서
        // 검사와 사람의 저장을 가르는 것은 여전히 SaveSlots의 몫이다.
        SaveSlots.Release();
        Assert.AreEqual(SaveSlots.Default, SaveSlots.Resolve(null));

        SaveSlots.Isolate("e2e_777");
        Assert.AreEqual("e2e_777", SaveSlots.Resolve(SaveSlots.Default));
        SaveSlots.Release();
    }
}
