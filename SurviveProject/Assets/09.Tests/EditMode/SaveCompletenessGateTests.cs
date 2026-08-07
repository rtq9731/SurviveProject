using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Building;
using Survive.Crafting;
using Survive.World;

/// <summary>
/// <b>저장이 무엇을 놓치는지 세는 게이트.</b>
///
/// 앞선 라운드가 「몸」을 저장에 실었다 — 세운 것과 떨군 것이 불러오기를 건넌다.
/// 그런데 <b>몸에 딸린 것</b>은 아직 아니었고, 그것은 시간이 아니라 <b>물건</b>을
/// 먹는 갈래다. 보관함에 맡긴 물건, 걸어 둔 제작, 뽑아 놓은 결과물.
///
/// 여기 있는 것은 코드가 아니라 <b>데이터가 규칙을 넘지 않는가</b>를 보는 검사다.
/// 코드가 아무리 옳아도 보관함 칸을 마흔으로 늘려 놓으면 저장이 다시 물건을 먹는데,
/// 그 순간은 코드를 안 고치므로 코드 검사로는 안 잡힌다.
/// </summary>
public class SaveCompletenessGateTests
{
    const string CatalogPath = "Assets/08.Data/Buildables/BuildCatalog.asset";
    const string MainScenePath = "Assets/01.Scenes/MainScene.unity";

    static BuildCatalogSO 청사진목록()
    {
        var c = AssetDatabase.LoadAssetAtPath<BuildCatalogSO>(CatalogPath);
        Assert.IsNotNull(c, $"{CatalogPath}를 못 읽었다 — 경로가 바뀌었는가");
        return c;
    }

    // ══ 데이터가 상한을 넘지 않는가 ════════════════════════════

    /// <summary>
    /// <b>보관함 칸 수가 몸마다의 무더기 상한 안에 있다.</b>
    ///
    /// 상한을 넘긴 칸은 저장본에 안 실린다 — 곧 <b>맡긴 물건이 사라진다</b>.
    /// 칸을 늘리는 일은 프리팹 인스펙터에서 일어나고 그때 아무도 코드를 안 보므로,
    /// 세는 자리를 여기 둔다.
    /// </summary>
    [Test]
    public void 세울_수_있는_보관함의_칸_수가_저장_상한_안에_있다()
    {
        var 보관함형 = 런타임형("Survive.Interaction.StorageContainer");
        int 본것 = 0;

        foreach (var 정의 in 청사진목록().entries)
        {
            if (정의?.prefab == null) continue;

            foreach (var mb in 정의.prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !보관함형.IsInstanceOfType(mb)) continue;

                본것++;
                int 칸 = 칸수(mb);
                Assert.LessOrEqual(칸, SpawnLedgerRule.StacksPerBody,
                    $"{정의.id}의 보관함 칸이 {칸}개인데 저장은 몸마다 " +
                    $"{SpawnLedgerRule.StacksPerBody}무더기까지만 싣는다 — " +
                    "SpawnLedgerRule.StacksPerBody도 함께 올려야 한다");
            }
        }

        Assert.Greater(본것, 0, "세울 수 있는 보관함이 하나도 없다 — 이 검사가 눈이 멀었다");
    }

    /// <summary>
    /// 회수함도 같다. 이쪽은 코드가 정하지만, 상한이 그보다 작아지면
    /// 뽑아 놓은 결과물이 사라진다.
    /// </summary>
    [Test]
    public void 회수함_칸_수가_저장_상한_안에_있다()
    {
        Assert.LessOrEqual(StationCraftQueue.DefaultOutputSlots,
                           SpawnLedgerRule.StacksPerBody);
        Assert.LessOrEqual(CraftQueue.DefaultCapacity, SpawnLedgerRule.JobsPerBody);
    }

    // ══ 저장 경로가 없는 몸이 씬에 서 있지 않은가 ══════════════

    /// <summary>
    /// <b>씬에 놓인 화톳불이 없다.</b>
    ///
    /// 화톳불의 연료와 추출 대기열은 <b>생성 목록의 줄</b>이 싣는다 — 곧 사람이
    /// <b>세운</b> 화톳불만 저장을 건넌다. 씬에 미리 놓인 화톳불은 몸의 주인이
    /// 씬이라 그 줄이 없고, 지금 그것을 위한 절도 없다.
    ///
    /// <b>안 만들기로 했다.</b> 오늘 씬에 화톳불이 하나도 없으므로, 만들어 두면
    /// 그것은 <b>아무도 안 밟는 저장 경로</b>가 된다. 안 밟는 경로는 썩고,
    /// 썩은 경로는 없느니만 못하다. 대신 <b>놓는 날 여기가 빨개진다</b> —
    /// 그때 이 주석이 무엇을 만들어야 하는지 말해 준다
    /// (<see cref="CraftingBench"/>가 씬에 놓인 제작대를 위해 한 그대로).
    /// </summary>
    [Test]
    public void 저장_경로가_없는_화톳불이_씬에_서_있지_않다()
    {
        Assert.AreEqual(0, 씬에_몇_개인가("Assets/02.Scripts/Building/Campfire.cs"),
            "씬에 화톳불을 놓았다 — 그 불의 연료와 추출 대기열은 저장을 못 건넌다. " +
            "CraftingBench처럼 씬 몸을 위한 ISaveable 절을 먼저 만들어라");
    }

    /// <summary>
    /// <b>음성 확인.</b> 위 검사가 정말로 세는가. 씬에 제작대가 하나 있고,
    /// 같은 훑개로 그것이 잡혀야 한다 — 안 잡히면 위의 0은 「없다」가 아니라
    /// 「눈이 멀었다」다.
    /// </summary>
    [Test]
    public void 씬을_세는_훑개가_실제로_센다()
    {
        Assert.Greater(씬에_몇_개인가("Assets/02.Scripts/Crafting/CraftingBench.cs"), 0,
            "씬에 제작대가 하나도 안 잡혔다 — 훑개가 아무것도 못 세고 있다");
    }

    /// <summary>
    /// 씬에 놓인 제작대는 <b>제 절을 갖는다</b>. 이 검사는 그 절이 정말로
    /// 붙어 있는지를 본다 — 인터페이스를 떼면 컴파일은 지나가고 대기열만 조용히 사라진다.
    /// </summary>
    [Test]
    public void 씬에_놓인_제작대는_제_저장_절을_갖는다()
    {
        저장_대상인가("Survive.Crafting.CraftingBench",
                    "제작대가 저장 대상이 아니다 — 걸어 둔 제작이 사라진다");
        저장_대상인가("Survive.Interaction.StorageContainer",
                    "씬에 놓인 보관함의 내용물이 사라진다");
        저장_대상인가("Survive.Crafting.HandCraftingService",
                    "손 제작 대기열은 몸이 없으므로 사람이 싣는다");
    }

    static void 저장_대상인가(string 이름, string 사유) =>
        Assert.IsTrue(typeof(Survive.Core.ISaveable).IsAssignableFrom(런타임형(이름)), 사유);

    // ══ 잔 도구 ════════════════════════════════════════════════

    /// <summary>
    /// 인스펙터에만 있는 값을 읽는다. 공개 문을 내면 코드에서 칸 수를 바꿀 수
    /// 있게 되는데, 칸 수는 프리팹이 정하는 값이다.
    /// </summary>
    static int 칸수(MonoBehaviour 보관함)
    {
        var so = new SerializedObject(보관함);
        var p = so.FindProperty("slotCount");
        Assert.IsNotNull(p, "StorageContainer.slotCount의 이름이 바뀌었다");
        return p.intValue;
    }

    /// <summary>
    /// 검사 어셈블리는 <c>Survive.Domain</c>만 본다 — 순수 로직을 Unity 없이
    /// 재는 것이 그 경계의 값이다. 그런데 여기서 세려는 것은 <b>MonoBehaviour가
    /// 저장 대상인가</b>이고 그 몸은 경계 밖에 있다. 이름으로 집는다 —
    /// 이름이 바뀌면 이 검사가 빨개지고, 그것이 옳다.
    /// </summary>
    static Type 런타임형(string 이름)
    {
        var t = Type.GetType(이름 + ", Assembly-CSharp");
        Assert.IsNotNull(t, $"{이름}을 못 찾았다 — 형이 옮겨졌거나 이름이 바뀌었다");
        return t;
    }

    /// <summary>
    /// 씬을 열지 않고 YAML에서 센다. 씬을 여는 검사는 느리고, 무엇보다 열어 둔
    /// 씬을 되돌리다 사람의 작업을 밟는다.
    /// </summary>
    static int 씬에_몇_개인가(string 스크립트경로)
    {
        string guid = AssetDatabase.AssetPathToGUID(스크립트경로);
        Assert.IsFalse(string.IsNullOrEmpty(guid),
                       $"{스크립트경로}의 guid를 못 찾았다 — 파일이 옮겨졌는가");

        string 경로 = Path.Combine(Directory.GetCurrentDirectory(), MainScenePath);
        Assert.IsTrue(File.Exists(경로), $"{MainScenePath}가 없다");

        return File.ReadAllLines(경로).Count(l => l.Contains(guid));
    }
}
