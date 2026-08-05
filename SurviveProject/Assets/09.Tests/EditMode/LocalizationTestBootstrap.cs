using System.IO;
using NUnit.Framework;
using UnityEngine;
using Survive.Localization;

/// <summary>
/// EditMode 어셈블리 전체가 시작하기 전에 번역 표를 한 번 읽어 둔다.
///
/// <b>왜 필요한가.</b> 런타임에서는 <c>LocalizationBootstrap</c>이 Resources에서 읽지만
/// 그것은 재생에 들어가야 돈다. EditMode 테스트는 재생하지 않으므로 표가 비어 있고,
/// 그러면 <c>MenuListing.NothingKnownToCraft</c>가 키("craft_empty")를 그대로 돌려준다 —
/// 번역과 무관한 기존 테스트가 영문 모를 이유로 무너진다.
///
/// 이름공간 밖에 둔 <c>SetUpFixture</c>는 NUnit에서 어셈블리 전체의 준비 단계가 된다.
/// </summary>
[SetUpFixture]
public class LocalizationTestBootstrap
{
    /// <summary>번역 표의 실제 파일 경로. Resources 아래에 있어야 런타임에도 읽힌다.</summary>
    public static string CsvPath =>
        Path.Combine(Application.dataPath, "Resources", "Localization", "strings.csv");

    /// <summary>파일에서 읽은 표. 게이트 테스트가 같은 것을 본다.</summary>
    public static StringCatalog LoadCatalogFromDisk()
    {
        Assert.IsTrue(File.Exists(CsvPath), $"번역 표가 없다: {CsvPath}");
        return StringCatalog.Parse(File.ReadAllText(CsvPath));
    }

    [OneTimeSetUp]
    public void 번역_표를_읽어_둔다()
    {
        Loc.Load(LoadCatalogFromDisk());
        Loc.SetLocale(StringCatalog.DefaultLocale);
        Assert.IsTrue(Loc.IsLoaded, "번역 표를 읽지 못했다 — 이 뒤의 모든 화면 문자열 검사가 키를 보게 된다");
    }
}
