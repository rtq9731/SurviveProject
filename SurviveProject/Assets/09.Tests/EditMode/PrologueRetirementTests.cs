using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Core;
using Survive.Localization;
using Survive.Narrative;

/// <summary>
/// <b>프롤로그에서 폐기된 것을 걷어냈다 (2026-08-07).</b>
///
/// 옛 프롤로그는 <b>떨어져 부서진 도착 · 지표를 덮은 기상 · 어딘가로 피신 ·
/// 바닥이 무너져 낙하</b> 네 마디였고, <b>그 넷이 전부 폐기 목록에 있다</b>
/// (<c>Plan/세계관.md</c> §8).
/// 새 설정에서 주인공은 스스로 광역 측량을 돌리고, 온도와 수분이 확인된 곳을 골라,
/// 호수 근방에 <b>내려앉는다</b> (기획서 §6.1).
///
/// <b>새 순서는 아직 설계되지 않았다.</b> 그래서 이 게이트는 "무엇을 가르치는가"를
/// 재지 않는다. 재는 것은 <b>걷어냈는가</b> 하나다.
///
/// <b>낱말 게이트만으로는 못 막는다.</b> <see cref="RetiredContentGateTests"/>는 글자를
/// 훑는데, 씬의 파티클 이름은 라틴 글자라 거기 안 걸린다. 실제로 낱말 쪽이 0이 된
/// 뒤에도 <b>폐기된 기상 연출 넷이 첫 화면에서 돌고 있었고</b>, 그 안에는 초당 산소
/// 8을 깎는 지대까지 들어 있었다 — 게이트는 내내 초록이었다. 그래서 여기서는
/// <b>씬 파일을 직접</b> 훑는다.
///
/// <b>무대 전체를 지우지는 않았다.</b> 지형과 배치는 사람의 몫이고(스펙 §10),
/// 씬 전환 지점을 지우면 <c>StartScene</c>이 막다른 길이 된다 — 빌드 설정의 첫 씬이라
/// 거기서 못 나가면 게임이 시작되지 않는다. 그래서 이 게이트가 지키는 것은
/// <b>폐기된 것이 없다</b>이지 <b>무대가 비었다</b>가 아니다.
/// </summary>
public class PrologueRetirementTests
{
    const string 프롤로그자막 = "Assets/08.Data/Sequences/Prologue_Intro.asset";
    const string 프롤로그씬 = "Assets/01.Scenes/StartScene.unity";
    const string 시작씬이름표 = "Assets/08.Data/Scenes/Scene_StartScene.asset";
    const string 기상프리팹 = "Assets/Other Assets/Standard Assets/ParticleSystems/Prefabs/DustStorm.prefab";
    const string 산소지대 = "Assets/02.Scripts/World/OxygenZone.cs";

    /// <summary>
    /// 프롤로그가 말하면 안 되는 것. <b>조각으로 짓는다</b> — 이 파일도
    /// <see cref="AssetTextScan.검사범위"/> 안(<c>Assets/09.Tests</c>)이라 그대로 적으면
    /// 폐기어 게이트가 이 파일을 문다.
    ///
    /// 앞 넷은 세계관 §8의 폐기 목록이고 <b>뒤 셋은 옛 프롤로그에만</b> 있던 말이다.
    /// 뒤 셋을 저장소 전체 게이트에 올리지 않는 이유는 다른 자리에서는 멀쩡한
    /// 말이기 때문이다 — <b>지하는 폐기된 것이 아니라 목적지</b>이고, 챕터 끝에
    /// 스스로 뚫고 내려간다. 프롤로그 자막이라는 <b>좁은 자리에서만</b> 금지한다.
    /// </summary>
    static readonly string[] 폐기된_무대 =
    {
        "화성" + "인", "모래" + "폭풍", "모래 " + "폭풍",
        "불시" + "착", "격" + "추", "비상 " + "착륙",
        "피난" + "처", "동" + "굴", "대" + "피",
    };

    // ── ① 자막이 폐기된 것을 말하지 않는다 ──────────────────

    /// <summary>
    /// <b>에셋과 표를 둘 다 본다.</b> 화면에 실제로 나가는 것은 표 쪽이고 에셋은
    /// 폴백이다. 한쪽만 고치면 다른 쪽으로 옛 문구가 되살아난다.
    /// </summary>
    [Test]
    public void 프롤로그_자막이_폐기된_무대를_말하지_않는다()
    {
        var 걸린것 = 자막_전수().SelectMany(줄 => 문다(줄.글).Select(말 => $"{줄.자리}: \"{줄.글}\" <- {말}")).ToList();

        Assert.IsEmpty(걸린것,
            $"프롤로그 자막이 폐기된 무대를 말한다 ({걸린것.Count}줄). " +
            "정본은 기획서 §6.1이다 — 스스로 측량하고 골라서 내려앉았다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary>
    /// <b>훑개가 비어 돌지 않는다.</b> 위 검사는 자막을 하나도 못 읽어도 초록이다.
    /// 에셋과 표 양쪽에서 실제로 줄을 주웠는지, 그리고 <b>줄 수가 같은지</b>까지 본다 —
    /// 한쪽에서만 자막을 빼면 화면과 폴백이 다른 말을 하게 된다.
    /// </summary>
    [Test]
    public void 에셋과_표가_같은_수의_자막을_들고_있다()
    {
        var 에셋줄 = 자막_전수().Where(l => l.자리.StartsWith("에셋")).ToList();
        var 표줄 = 자막_전수().Where(l => l.자리.StartsWith("표")).ToList();

        Assert.IsNotEmpty(에셋줄, "에셋에서 프롤로그 자막을 하나도 못 읽었다 — 검사가 헛돈다");
        Assert.IsNotEmpty(표줄, "번역 표에서 프롤로그 자막을 하나도 못 읽었다 — 검사가 헛돈다");
        Assert.AreEqual(에셋줄.Count, 표줄.Count,
            "에셋과 표의 자막 수가 다르다. 한쪽에서만 줄을 빼면 폴백이 옛 말을 한다:\n  에셋 " +
            string.Join(" / ", 에셋줄.Select(l => l.글)) + "\n  표 " +
            string.Join(" / ", 표줄.Select(l => l.글)));

        CollectionAssert.AreEqual(에셋줄.Select(l => l.글).ToList(), 표줄.Select(l => l.글).ToList(),
            "에셋과 표의 자막이 순서까지 같아야 한다 — 순서가 곧 맥락이다");
    }

    // ── ② 이름표가 반전을 흘리지 않는다 ─────────────────────

    /// <summary>
    /// <b>주인공은 여기가 어디인지 모른다.</b> 그것을 모른다는 사실이 세계관 §6의
    /// 반전 전체를 떠받친다 — 인류가 고향을 잃은 것이 아니라 고향이 변해서
    /// 알아볼 수 없게 된 것이다. 첫 화면의 이름표가 행성 이름을 적으면 그 반전이
    /// 게임 시작 30초에 새어 나간다.
    /// </summary>
    [Test]
    public void 씬_이름표가_행성_이름을_흘리지_않는다()
    {
        string 금지 = "화" + "성";

        var 이름표 = AssetDatabase.LoadAssetAtPath<SceneReferenceSO>(시작씬이름표);
        Assert.IsNotNull(이름표, $"{시작씬이름표}를 못 읽었다");
        StringAssert.DoesNotContain(금지, 이름표.displayName ?? "",
            "프롤로그 씬 이름표가 행성 이름을 적고 있다 (기획서 §6.1)");

        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk()
                                          .TableFor(StringCatalog.DefaultLocale);
        var 이름표들 = 표.Where(p => p.Key.Category == "Scene").ToList();
        Assert.IsNotEmpty(이름표들, "번역 표에서 씬 이름표를 하나도 못 찾았다 — 검사가 헛돈다");

        var 걸린것 = 이름표들.Where(p => (p.Value ?? "").Contains(금지))
                            .Select(p => $"{p.Key}: \"{p.Value}\"").ToList();
        Assert.IsEmpty(걸린것, "씬 이름표가 행성 이름을 적고 있다:\n  " + string.Join("\n  ", 걸린것));
    }

    // ── ③ 무대에서 폐기된 것이 빠졌다 ───────────────────────

    /// <summary>
    /// <b>파티클 이름은 라틴 글자라 낱말 게이트에 안 걸린다.</b> 그래서 여기서만
    /// 잡을 수 있다. 프리팹 참조(GUID)와 이름 양쪽을 본다 — 프리팹을 다른 것으로
    /// 갈아 놓고 이름만 남기는 되살림도 있고, 그 반대도 있다.
    /// </summary>
    [Test]
    public void 프롤로그_씬에_폐기된_기상_연출이_없다()
    {
        string 본문 = 씬본문();

        string guid = AssetDatabase.AssetPathToGUID(기상프리팹);
        if (!string.IsNullOrEmpty(guid))
            Assert.IsFalse(본문.Contains(guid),
                $"{프롤로그씬}에 폐기된 기상 프리팹 참조가 남아 있다. " +
                "지표를 덮은 것은 기상 현상이 아니라 매크로늄 바다다 (세계관 §8)");

        // 프리팹이 없어져도 이쪽은 돈다. 이름을 조각으로 짓는 이유는 위와 같다.
        string 이름 = "Dust" + "Storm";
        Assert.IsFalse(본문.Contains(이름),
            $"{프롤로그씬}에 폐기된 기상 연출의 이름이 남아 있다");
    }

    /// <summary>
    /// <b>압박도 함께 빠졌다.</b> 폐기된 기상 넷은 초당 산소 8을 깎는 지대를
    /// 품고 있었다 — 화면의 연출만 지우고 그것을 남기면, 아무것도 안 보이는데
    /// 플레이어만 숨이 막힌다.
    ///
    /// <b>0을 요구하는 것이 맞다.</b> 옛 프롤로그는 지표를 덮은 기상이라는 압박
    /// 하나로 이동·산소·안전지대를 한 번에 가르쳤는데, 그 장치가 없어졌다.
    /// <b>무엇이 그 자리를 대신할지는 사람이 정한다</b> (기획서 §6.1 [미결]).
    /// 그때 이 검사는 함께 고쳐야 하고, 고쳐야 한다는 사실이 여기 적혀 있다.
    /// </summary>
    [Test]
    public void 프롤로그_씬에_산소를_깎는_자리가_없다()
    {
        string guid = AssetDatabase.AssetPathToGUID(산소지대);
        Assert.IsNotEmpty(guid, $"{산소지대}를 찾지 못했다 — 검사가 헛돌고 있다");

        Assert.IsFalse(씬본문().Contains(guid),
            $"{프롤로그씬}에 산소를 깎는 지대가 남아 있다. 폐기된 기상이 들고 있던 " +
            "압박이고, 대신할 것은 아직 정해지지 않았다 (기획서 §6.1)");
    }

    // ── ④ 음성 확인 ─────────────────────────────────────────

    /// <summary>
    /// <b>금지어마다 무는지 확인한다.</b> 조각으로 지으면 오타 하나가 아무것도
    /// 물지 않는 말을 만드는데, 0건과 구별되지 않아 게이트가 눈이 먼 채로 초록이 된다.
    /// </summary>
    [Test]
    public void 금지어마다_실제로_무는지_확인한다()
    {
        var 이상한것 = new List<string>();

        foreach (var 말 in 폐기된_무대)
        {
            if (말.Length < 2) { 이상한것.Add($"「{말}」 — 조각을 잇다 만 것 같다"); continue; }
            if (문다($"앞말 {말} 뒷말").Count == 0) 이상한것.Add($"「{말}」 — 자기가 들어 있는 문장도 못 잡는다");
        }

        Assert.IsEmpty(이상한것, "금지어가 제구실을 못 한다:\n  " + string.Join("\n  ", 이상한것));
        Assert.IsEmpty(문다("착륙 완료. 성간 항행에 쓸 연료는 남지 않았습니다."),
            "지금 쓰는 첫 자막이 금지어에 걸린다 — 목록이 너무 넓다");
    }

    /// <summary>
    /// <b>지금 참인 것은 걸리지 않는다.</b> 잘못 잡는 게이트는 없느니만 못하다.
    /// 지하는 폐기된 것이 아니라 목적지이고, 착륙과 측량은 새 설정 그 자체다.
    /// </summary>
    [Test]
    public void 지금_참인_문장은_걸리지_않는다()
    {
        var 참인문장 = new[]
        {
            "광역 측량 결과 : 기준이 되는 별이 하나도 잡히지 않았습니다.",
            "주의 : 지표를 덮은 액체가 데이터베이스에 없습니다. 접촉을 피하십시오.",
            "파일럿의 생존을 위해 주변 탐색을 실시합니다...",
            "잘리고 남은 절반인 지하로 내려간다",
            "레이더가 지하 구조물을 짚어낸다",
        };

        var 오탐 = 참인문장.SelectMany(문장 => 문다(문장).Select(말 => $"「{말}」이 참인 문장을 물었다: \"{문장}\""))
                          .ToList();

        Assert.IsEmpty(오탐, "금지어를 좁혀라:\n  " + string.Join("\n  ", 오탐));
    }

    // ── 훑개 ────────────────────────────────────────────────

    static List<string> 문다(string 글) =>
        string.IsNullOrEmpty(글)
            ? new List<string>()
            : 폐기된_무대.Where(말 => 글.IndexOf(말, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();

    static string 씬본문() =>
        File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), 프롤로그씬));

    /// <summary>에셋과 번역 표에 적힌 프롤로그 자막 전부. 재생 순서대로.</summary>
    static List<(string 자리, string 글)> 자막_전수()
    {
        var 모은것 = new List<(string, string)>();

        var 시퀀스 = AssetDatabase.LoadAssetAtPath<SequenceSO>(프롤로그자막);
        Assert.IsNotNull(시퀀스, $"{프롤로그자막}를 못 읽었다");
        for (int i = 0; i < 시퀀스.lines.Length; i++)
        {
            var line = 시퀀스.lines[i];
            if (line == null || string.IsNullOrWhiteSpace(line.text)) continue;
            모은것.Add(($"에셋 .lines[{i}]", line.text));
        }

        // 기준 로케일만 본다. 번역이 덜 된 로케일은 칸이 비어 있어 함께 세면
        // "에셋과 표의 줄 수가 같다"가 번역 진척도에 따라 흔들린다.
        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk()
                                          .TableFor(StringCatalog.DefaultLocale);
        // .line1부터 재생 순서대로다. 번호로 훑어야 순서가 보존된다.
        for (int n = 1; ; n++)
        {
            var 열쇠 = new LocKey("Sequence", $"{시퀀스.id}.line{n}.text");
            if (!표.TryGetValue(열쇠, out var 글) || string.IsNullOrWhiteSpace(글)) break;
            모은것.Add(($"표 {열쇠}", 글));
        }

        return 모은것.Select(t => (자리: t.Item1, 글: t.Item2)).ToList();
    }
}
