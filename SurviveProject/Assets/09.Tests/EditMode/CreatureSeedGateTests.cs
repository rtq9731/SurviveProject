using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// <b>생물과 규칙에는 주인 없는 난수가 한 자리도 없다.</b>
///
/// <c>WorldSeedTests</c>가 이미 <c>new Random()</c>(시각 시드)를 막고 있다. 그런데
/// 세계를 바꾸는 난수는 대개 그쪽이 아니라 <c>Random.value</c>·<c>Random.Range</c>로
/// 샌다 — 그것도 결국 판마다 다른 답을 내므로, 저장본을 건너오면 같은 세계가
/// 다른 세계가 된다.
///
/// <b>남겨 둔 것은 눈과 귀뿐이다.</b> 떨어진 물건이 놓이는 각도
/// (<c>Interaction/ItemDropper</c>)와 소리의 높낮이·크기(<c>Audio/AudioService</c>)는
/// 세계 상태가 아니라 연출이라 판마다 달라도 아무것도 어긋나지 않는다. 그래서
/// <b>범위를 좁혀</b> 잠근다 — 온 저장소에 0을 요구하면 저 둘까지 끌려와 게이트가
/// 뜻 없이 빨개진다.
///
/// <b>왜 이 두 폴더인가.</b> <c>Creatures/</c>는 배회와 유물 굴림이 사는 곳이다 —
/// 실측으로 <b>배회 난수가 「낫 꼬리」 36% 플레이크의 진범</b>이었고, 그때는 시나리오
/// 쪽에서 무대를 붙들어 고쳤다. 뿌리를 막으면 그 부류가 통째로 사라진다.
/// <c>Domain/</c>은 규칙 그 자체이고, 규칙이 난수를 직접 뽑으면 순수 함수가 아니게 된다.
/// </summary>
public class CreatureSeedGateTests
{
    static readonly string[] 잠근폴더 = { "02.Scripts/Creatures", "02.Scripts/Domain" };

    [Test]
    public void 생물과_규칙에는_주인_없는_난수가_없다()
    {
        var 무늬 = new Regex(
            @"(?<![\w.])Random\s*\.\s*(value|Range|insideUnitSphere|insideUnitCircle" +
            @"|onUnitSphere|rotation|rotationUniform|ColorHSV|InitState)",
            RegexOptions.Compiled);

        var 걸린것 = new List<string>();

        foreach (string 폴더 in 잠근폴더)
        {
            string 뿌리 = Path.Combine(Application.dataPath, 폴더);
            if (!Directory.Exists(뿌리)) continue;

            foreach (string 파일 in Directory.GetFiles(뿌리, "*.cs", SearchOption.AllDirectories))
            {
                // 주석은 뺀다. 왜 옮겼는지를 코드 옆에 적어 두는 것은 옳은 일이다.
                string 본문 = Survive.Localization.LocSourceScanner.StripComments(
                    File.ReadAllText(파일));

                foreach (Match m in 무늬.Matches(본문))
                {
                    int 줄 = 본문.Take(m.Index).Count(c => c == (char)10) + 1;
                    string 짧은이름 = 파일.Substring(Application.dataPath.Length + 1)
                                        .Replace(Path.DirectorySeparatorChar, '/');
                    걸린것.Add(짧은이름 + ":" + 줄 + " " + m.Value);
                }
            }
        }

        Assert.IsEmpty(걸린것,
            "생물·규칙에 주인 없는 난수가 있다. WorldSeed.Rng로 옮겨라:\n  " +
            string.Join(((char)10) + "  ", 걸린것));
    }
}
