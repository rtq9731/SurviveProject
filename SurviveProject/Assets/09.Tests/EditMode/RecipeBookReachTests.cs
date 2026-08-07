using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Crafting;

/// <summary>
/// <b>레시피 목록이 빌드에서도 닿는가.</b>
///
/// 저장본은 걸어 둔 제작을 <c>RecipeSO.id</c>라는 <b>글자</b>로 싣는다. 불러올 때
/// 그 글자에서 레시피로 돌아오지 못하면 <b>걸어 둔 제작 하나가 경고와 함께
/// 사라진다</b> — 재료는 걸 때 이미 빠진 뒤다.
///
/// 돌아오는 길을 만드는 <c>RecipeIndex</c>는 예전에 <c>Resources.FindObjectsOfTypeAll</c>
/// 하나에 기댔다. 그것은 <b>이미 메모리에 올라와 있는</b> 것만 주는데, 에디터에서는
/// 프로젝트의 에셋이 대개 올라와 있어서 <b>언제나 성공한다.</b> 즉 그 길이 빌드에서
/// 끊겨도 <b>에디터에서는 영영 안 보인다.</b>
///
/// 그래서 <see cref="Resources.Load"/>로 닿는 종이를 하나 두었고
/// (<see cref="RecipeBookLocatorSO"/>), 이 파일이 <b>그 종이가 제자리에 있고 제대로
/// 꽂혀 있는지</b>를 못 박는다. 목록 에셋 자체는 <c>08.Data/Recipes/</c>에 그대로
/// 둔다 — 그 경로를 물고 있는 검사와 도구가 여럿이다.
///
/// <c>RecipeIndex</c> 자체는 예정된 어셈블리(Assembly-CSharp)에 있어 여기서 못
/// 부른다. <b>런타임 쪽 확인은 E2E(<c>E2ESaveTrust</c>)가 맡는다</b> —
/// 거기서 훑기를 빼고 이 길 하나만으로 표를 지어 본다.
/// </summary>
public class RecipeBookReachTests
{
    const string 종이경로 = "Assets/Resources/RecipeBookLocator.asset";
    const string 목록경로 = "Assets/08.Data/Recipes/RecipeBook.asset";

    static RecipeBookLocatorSO 종이 => AssetDatabase.LoadAssetAtPath<RecipeBookLocatorSO>(종이경로);
    static RecipeBookSO 목록 => AssetDatabase.LoadAssetAtPath<RecipeBookSO>(목록경로);

    [Test]
    public void 가리키는_종이가_Resources에_있다()
    {
        Assert.IsTrue(File.Exists(종이경로),
            $"{종이경로}가 없다. 이것이 빠지면 RecipeIndex는 에디터에서만 도는 " +
            "훑기 하나로 떨어지고, 그 사고는 빌드에서만 보인다.");
        Assert.IsNotNull(종이, "종이를 에셋으로 읽지 못했다");
    }

    [Test]
    public void 선언한_경로와_실제_자리가_같다()
    {
        Assert.AreEqual(RecipeBookLocatorSO.AssetPath, 종이경로,
            "코드가 말하는 자리와 게이트가 보는 자리가 갈리면 둘 다 못 믿는다");
        Assert.AreEqual(RecipeBookLocatorSO.ResourceName,
                        Path.GetFileNameWithoutExtension(종이경로),
            "Resources.Load가 찾는 이름은 파일 이름 그대로여야 한다");
    }

    [Test]
    public void Resources_Load로_실제로_닿는다()
    {
        // 이 한 줄이 빌드에서 도는 길 그 자체다. 에디터에서도 Resources 폴더
        // 규칙은 같으므로, 여기서 null이면 빌드에서도 null이다.
        var 불러온것 = Resources.Load<RecipeBookLocatorSO>(RecipeBookLocatorSO.ResourceName);
        Assert.IsNotNull(불러온것,
            $"Resources.Load<{nameof(RecipeBookLocatorSO)}>(\"{RecipeBookLocatorSO.ResourceName}\")가 null이다");
    }

    [Test]
    public void 종이가_진짜_레시피_목록을_가리킨다()
    {
        Assert.IsNotNull(종이, "종이가 없다");
        Assert.IsNotNull(목록, $"{목록경로}가 없다");
        Assert.AreSame(목록, 종이.Book,
            "종이가 다른 목록을 가리키면 저장이 되찾는 레시피와 제작 화면이 " +
            "보여 주는 레시피가 갈린다");
    }

    [Test]
    public void 그_목록으로_id에서_레시피로_돌아올_수_있다()
    {
        var book = 종이 != null ? 종이.Book : null;
        Assert.IsNotNull(book, "종이에 목록이 안 꽂혀 있다");

        var 살아있는것 = book.recipes.Where(r => r != null && !string.IsNullOrEmpty(r.id)).ToList();
        Assert.Greater(살아있는것.Count, 0, "목록이 비어 있으면 되찾을 수 있는 것이 없다");

        var 겹친것 = 살아있는것.GroupBy(r => r.id).Where(g => g.Count() > 1)
                               .Select(g => g.Key).ToList();
        Assert.IsEmpty(겹친것,
            "겹친 id는 저장이 레시피를 id로 가리키는 지금 조용히 엉뚱한 것을 되찾는다: " +
            string.Join(", ", 겹친것));
    }

    /// <summary>
    /// <b>종이를 통해 빌드에 실린다.</b> <c>Resources/</c> 아래의 것은 참조를 따라
    /// 통째로 실리므로, 제작 화면이 씬에서 빠지는 날에도 저장이 걸어 둔 제작을
    /// 되찾을 수 있다. 그 의존이 실제로 걸려 있는지를 본다.
    /// </summary>
    [Test]
    public void 종이가_레시피들을_빌드로_끌고_들어간다()
    {
        var 딸린것 = AssetDatabase.GetDependencies(종이경로, true);
        Assert.Contains(목록경로, 딸린것, "종이가 목록을 참조하지 않는다");

        var book = 목록;
        Assert.IsNotNull(book);

        var 첫레시피 = book.recipes.FirstOrDefault(r => r != null);
        Assert.IsNotNull(첫레시피, "목록에 레시피가 하나도 없다");

        string 레시피경로 = AssetDatabase.GetAssetPath(첫레시피);
        Assert.Contains(레시피경로, 딸린것,
            "레시피 에셋까지 딸려 들어가야 빌드에서 목록이 온전하다");
    }
}
