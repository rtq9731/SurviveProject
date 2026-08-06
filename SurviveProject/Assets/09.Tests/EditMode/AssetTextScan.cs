using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// 저장소 본문에서 <b>있으면 안 되는 말</b>을 찾는 공용 훑개.
///
/// <b>왜 따로 뺐는가.</b> 같은 일을 하는 게이트가 둘이다 — 폐기한 것이 정말 사라졌는가
/// (<see cref="RetiredContentGateTests"/>)와 옛 이름이 정말 사라졌는가
/// (<see cref="RenamedNameGateTests"/>). 훑는 자리 목록과 유니코드를 푸는 규칙을
/// 두 벌로 두면 언젠가 한쪽에만 폴더를 더하게 되고, 그날 다른 게이트는 눈이 먼다.
/// 목록이 하나면 어느 게이트를 고쳐도 둘 다 강해진다.
/// </summary>
public static class AssetTextScan
{
    /// <summary>
    /// 훑는 자리. <b>우리가 쓴 것만</b> 본다.
    ///
    /// <c>Assets/polyperfect</c>는 사 온 아트 팩이라 밖에 둔다 — 남의 팩을 손대면
    /// 다음 갱신 때 되돌아온다. <c>Plan/</c>·<c>docs/</c>의 서술도 밖이다.
    /// 거기 남은 것은 "예전에는 이랬다"는 역사이고, 역사를 지우면 왜 바뀌었는지가 사라진다.
    /// </summary>
    public static readonly (string 폴더, string 무늬)[] 검사범위 =
    {
        ("Assets/02.Scripts", "*.cs"),
        ("Assets/09.Tests",   "*.cs"),
        ("Assets/08.Data",    "*.asset"),
        ("Assets/01.Scenes",  "*.unity"),
        ("Assets/05.Prefabs", "*.prefab"),
        ("Assets/Resources/Localization", "*.csv"),
    };

    /// <summary>
    /// <paramref name="금지어"/>가 나오는 자리를 <c>경로:줄</c> 꼴로 모아 준다.
    /// 대소문자는 가리지 않는다 — <c>Foo</c>로 되살려 두는 것도 되살린 것이다.
    /// </summary>
    public static List<string> 찾는다(IEnumerable<string> 금지어)
    {
        var 말들 = 금지어.ToArray();
        var 걸린것 = new List<string>();

        foreach (var (폴더, 무늬) in 검사범위)
        {
            string 절대경로 = Path.Combine(Directory.GetCurrentDirectory(), 폴더);
            if (!Directory.Exists(절대경로))
            {
                Assert.Fail($"검사 범위에 적힌 폴더가 없다: {폴더}");
                continue;
            }

            foreach (var 파일 in Directory.GetFiles(절대경로, 무늬, SearchOption.AllDirectories))
            {
                string 본문 = 유니코드를_푼다(File.ReadAllText(파일));
                foreach (var 말 in 말들)
                {
                    int 줄 = 몇번째_줄인가(본문, 말);
                    if (줄 > 0) 걸린것.Add($"{상대경로(파일)}:{줄}");
                }
            }
        }

        return 걸린것;
    }

    /// <summary>
    /// 에셋 YAML은 한글을 <c>\u...</c> 꼴로 적는다. 그대로 찾으면 데이터 쪽을
    /// 통째로 놓친다 — 실제로 아이템 설명문이 그렇게 숨어 있었다.
    /// </summary>
    public static string 유니코드를_푼다(string s) =>
        Regex.Replace(s, @"\\u([0-9a-fA-F]{4})",
                      m => ((char)System.Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

    static int 몇번째_줄인가(string 본문, string 말)
    {
        int i = 본문.IndexOf(말, System.StringComparison.OrdinalIgnoreCase);
        if (i < 0) return 0;
        return 본문.Take(i).Count(c => c == '\n') + 1;
    }

    static string 상대경로(string 절대) =>
        절대.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');
}
