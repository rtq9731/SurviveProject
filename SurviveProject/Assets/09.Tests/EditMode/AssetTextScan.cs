using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    public static List<string> 찾는다(IEnumerable<string> 금지어) => 찾는다(금지어, false);

    /// <summary>
    /// 대소문자를 가려야 하는 말을 위한 갈래.
    ///
    /// <b>왜 갈래가 필요한가.</b> <b>라틴 글자 한 자에 붙임표와 숫자를 단</b> 구역
    /// 이름은 대소문자를 안 가리면 언젠가 <c>delta-1</c>·<c>meta-2</c> 같은 멀쩡한 말의
    /// 꼬리를 문다. 지금 저장소에는 그런 자리가 없지만, 잘못 잡는 게이트는
    /// 없느니만 못하므로 <b>잡을 수 있는 오탐을 미리 닫는다.</b>
    /// 한글 금지어에는 대소문자가 없으니 이 갈래가 필요 없다.
    /// </summary>
    public static List<string> 찾는다(IEnumerable<string> 금지어, bool 대소문자를_가린다)
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
                var (본문, 줄번호) = 훑을_꼴로_고친다(File.ReadAllText(파일), 무늬);
                foreach (var 말 in 말들)
                {
                    int 자리 = 어디인가(본문, 말, 대소문자를_가린다);
                    if (자리 >= 0) 걸린것.Add($"{상대경로(파일)}:{줄번호[자리]}");
                }
            }
        }

        return 걸린것;
    }

    /// <summary>
    /// 본문 한 조각에 그 말이 들어 있는가. <b>음성 확인용으로 뺐다</b> —
    /// 금지어를 조각으로 지으면 오타 하나가 아무것도 못 잡는 말을 만드는데,
    /// 파일을 훑는 검사만으로는 그것이 0건과 구별되지 않는다.
    /// 여기에 표본 문장을 넣어 보면 그 말이 정말로 무는지 알 수 있다.
    /// </summary>
    public static bool 걸리는가(string 본문, string 말, bool 대소문자를_가린다 = false) =>
        어디인가(본문, 말, 대소문자를_가린다) >= 0;

    /// <summary>
    /// 에셋 YAML은 한글을 <c>\u...</c> 꼴로 적는다. 그대로 찾으면 데이터 쪽을
    /// 통째로 놓친다 — 실제로 아이템 설명문이 그렇게 숨어 있었다.
    /// </summary>
    public static string 유니코드를_푼다(string s) =>
        Regex.Replace(s, @"\\u([0-9a-fA-F]{4})",
                      m => ((char)System.Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

    /// <summary>
    /// YAML은 긴 문자열을 <b>줄바꿈 + 들여쓰기</b>로 접는다. 접힌 자리를 그대로 두면
    /// 두 낱말짜리 구절이 통째로 눈에서 사라진다 — 「합금 더미」가 파일에는
    /// <c>합금</c> 개행 들여쓰기 <c>더미</c>로 적혀 있는 식이다.
    ///
    /// <b>실제로 두 건을 놓치고 있었다.</b> 2026-08-07에 재 보니 폐기어 두 개가
    /// 접힌 줄 뒤에 숨어 있었고, 그중 하나는 스펙이 콕 집어 고치라고 적어 둔
    /// 프롤로그 자막이었다. 사람이 눈으로 훑어 찾은 것을 기계가 못 찾고 있었다.
    ///
    /// <b>YAML에만 편다.</b> C#은 문자열을 줄로 나누지 않고(<c>+</c>로 잇는다),
    /// 모든 줄을 이어 붙이면 <c>///</c> 주석 두 줄이 붙어 없던 구절이 생긴다.
    /// 접는 것은 YAML의 문법이므로 YAML에서만 되돌린다.
    ///
    /// 돌려주는 <c>줄번호</c>는 편 본문의 글자마다 <b>원본 줄</b>을 적어 둔 것이다.
    /// 이것이 없으면 편 뒤의 줄 수로 자리를 알려 주게 되고, 실패 메시지가 가리키는
    /// 줄에 아무것도 없게 된다.
    /// </summary>
    static (string 본문, int[] 줄번호) 훑을_꼴로_고친다(string 원문, string 무늬)
    {
        string s = 유니코드를_푼다(원문);
        bool YAML인가 = 무늬 == "*.asset" || 무늬 == "*.prefab" || 무늬 == "*.unity";

        var 글자 = new StringBuilder(s.Length);
        var 줄 = new List<int>(s.Length);
        int 현재줄 = 1;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\r') continue;

            if (c == '\n')
            {
                현재줄++;
                if (YAML인가)
                {
                    int j = i + 1;
                    while (j < s.Length && (s[j] == ' ' || s[j] == '\t')) j++;
                    if (j > i + 1)
                    {
                        // 접힌 줄이다. 개행과 들여쓰기를 공백 하나로 되돌린다.
                        글자.Append(' ');
                        줄.Add(현재줄 - 1);
                        i = j - 1;
                        continue;
                    }
                }
                글자.Append('\n');
                줄.Add(현재줄 - 1);
                continue;
            }

            글자.Append(c);
            줄.Add(현재줄);
        }

        return (글자.ToString(), 줄.ToArray());
    }

    /// <summary>
    /// 빈 말은 <b>아무것도 아닌 것</b>으로 친다. <c>IndexOf("")</c>는 0을 돌려주므로
    /// 조각으로 짓다 실수해 빈 문자열이 생기면 저장소의 모든 파일이 걸린다 —
    /// 눈이 먼 게이트보다 나쁘다.
    /// </summary>
    static int 어디인가(string 본문, string 말, bool 대소문자를_가린다) =>
        string.IsNullOrEmpty(말)
            ? -1
            : 본문.IndexOf(말, 대소문자를_가린다
                              ? System.StringComparison.Ordinal
                              : System.StringComparison.OrdinalIgnoreCase);

    static string 상대경로(string 절대) =>
        절대.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');
}
