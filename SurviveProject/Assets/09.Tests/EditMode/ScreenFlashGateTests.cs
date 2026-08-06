using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// 화면 전체를 순간적으로 번쩍이게 하는 것을 못 들어오게 막는다.
///
/// <b>왜 지웠는가.</b> 사용자가 직접 말했다 — "화면 전체 플래쉬가 좀 있는데,
/// 이건 진짜 눈아프니까 싹다 없애줘"(2026-08-06). 세기를 낮추는 절충은 하지 않았다.
/// <b>광과민성은 취향 문제가 아니다.</b> 다시 넣고 싶어지면 그때 끌 수 있는
/// 옵션으로 넣는 것이 맞는 순서다.
///
/// <b>왜 게이트가 필요한가.</b> 지운 것은 씬에 직렬화된 피드백 여덟 개와
/// 화면을 덮던 판 하나다. 씬은 손으로 만지는 파일이라, Feel의 피드백 목록에서
/// 드롭다운 한 번이면 조용히 되살아난다. 되살아난 것을 알아채는 방법은
/// <b>다시 눈이 아파 보는 것</b>뿐이고, 그때는 이미 사용자가 겪은 뒤다.
///
/// <b>남긴 것 — 헷갈리지 마라.</b> <c>ScreenFader</c>(장면 전환의 검은 페이드)는
/// 그대로다. 그것은 번쩍임이 아니라 서서히 어두워지는 것이고, 사용자가 문제 삼은
/// 것이 아니다. 피드백 묶음의 다른 항목(흔들림·파티클·프리즈 프레임)도 남겼다 —
/// 플래시만 빼면 타격감은 그대로 있다.
/// </summary>
public class ScreenFlashGateTests
{
    /// <summary>
    /// 찾을 말. 피드백 항목(<c>MMF_Flash</c>)과 그것이 때리는 판(<c>MMFlash</c>) 둘 다 본다.
    /// 앞의 것만 보면 판이 남아 아무 일도 안 하는 오브젝트가 되고,
    /// 뒤의 것만 보면 판 없이 방송만 하는 피드백이 남는다.
    /// </summary>
    static readonly string[] 금지형 = { "MMF_Flash", "Feedbacks.MMFlash" };

    /// <summary>
    /// 훑는 자리. <b>우리가 쓴 씬과 프리팹만</b> 본다.
    ///
    /// <c>Assets/Feel</c>은 사 온 패키지이고 그 안의 데모 씬·프리팹은 플래시를
    /// 보여 주는 것이 목적이다. 게임은 그 씬을 빌드에 넣지 않는다.
    /// 남의 패키지를 고치면 다음 갱신 때 되돌아오므로 검사 밖에 둔다.
    /// </summary>
    static readonly (string 폴더, string 무늬)[] 검사범위 =
    {
        ("Assets/01.Scenes", "*.unity"),
        ("Assets/05.Prefabs", "*.prefab"),
        ("Assets/Resources", "*.prefab"),
        ("Assets/02.Scripts", "*.cs"),
    };

    [Test]
    public void 화면_전체_플래시가_씬에도_프리팹에도_코드에도_없다()
    {
        var 걸린것 = new List<string>();

        foreach (var (폴더, 무늬) in 검사범위)
        {
            string 절대 = Path.Combine(Directory.GetCurrentDirectory(), 폴더);
            if (!Directory.Exists(절대))
            {
                Assert.Fail($"검사 범위에 적힌 폴더가 없다: {폴더}");
                continue;
            }

            foreach (var 파일 in Directory.GetFiles(절대, 무늬, SearchOption.AllDirectories))
            {
                string 본문 = File.ReadAllText(파일);
                foreach (var 말 in 금지형)
                {
                    int i = 본문.IndexOf(말, System.StringComparison.Ordinal);
                    if (i < 0) continue;
                    int 줄 = 본문.Take(i).Count(c => c == '\n') + 1;
                    걸린것.Add($"{상대경로(파일)}:{줄}  ({말})");
                }
            }
        }

        Assert.IsEmpty(걸린것,
            $"화면 전체를 번쩍이게 하는 것이 {걸린것.Count}군데 되살아났다. " +
            "사용자가 눈이 아프다고 없애 달라고 한 것이다 — 세기를 낮추지 말고 빼라. " +
            "장면 전환의 검은 페이드(ScreenFader)는 이것과 다른 것이고 남겨 두는 것이 맞다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary>
    /// 플래시만 뺐지 <b>피드백 묶음까지 없애지는 않았다</b>는 것을 못 박는다.
    /// 타격감이 통째로 사라지면 그것은 이 작업의 실패다 — 지울 때 묶음째
    /// 지우기가 훨씬 쉬웠고, 그래서 실수하기도 쉬웠다.
    /// </summary>
    [Test]
    public void 때리고_죽는_감각은_남아_있다()
    {
        string scene = File.ReadAllText(
            Path.Combine(Directory.GetCurrentDirectory(), "Assets/01.Scenes/MainScene.unity"));

        foreach (var 남을것 in new[] { "MMF_FreezeFrame", "MMF_ParticlesInstantiation" })
            Assert.IsTrue(scene.Contains(남을것),
                $"{남을것}까지 사라졌다. 플래시만 빼려던 것이지 묶음을 비우려던 것이 아니다");

        foreach (var 묶음 in new[] { "FB_hurtFeedback", "FB_deathFeedback" })
            Assert.IsTrue(scene.Contains(묶음), $"{묶음} 오브젝트가 사라졌다");
    }

    static string 상대경로(string 절대) =>
        절대.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');
}
