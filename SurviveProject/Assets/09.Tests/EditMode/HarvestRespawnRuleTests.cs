using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Crafting;
using Survive.Harvesting;
using Survive.Items;
using Survive.World;

/// <summary>
/// <b>세계가 마르지 않는다.</b>
///
/// 랜턴은 매 초 배터리를 태우고 배터리는 스크랩에서 나온다. 그런데 A섬에 서 있는
/// 스크랩은 유한하고, 재생이 0이면 그 뒤로 순증가가 영영 0이다 — 길게 보면 반드시
/// 음수이고, 그러면 랜턴 지속시간을 어떻게 잡든 압박 곡선이 성립하지 않는다.
///
/// 여기서 재는 것은 셋이다.
/// <list type="number">
/// <item><b>경계</b> — 다 캔 직후 / 주기 직전 / 주기 직후.</item>
/// <item><b>장부</b> — 세계의 정상 상태 공급이 랜턴 소모를 넘는가. 열한 번째 분을
///   넘겨도 넘는가(스크랩 총량 54개가 랜턴 11.25분어치라 그 언저리가 갈림길이다).</item>
/// <item><b>0이 실수가 아니라는 것</b> — 「합금 더미」가 돌아오지 않는 것은 설계다.
///   테스트가 그 근거를 한 마디씩 밟는다. 근거가 끊기면 여기가 빨개진다.</item>
/// </list>
/// </summary>
public class HarvestRespawnRuleTests
{
    const string 채집물폴더 = "Assets/08.Data/HarvestNodes";
    const string 씬 = "Assets/01.Scenes/MainScene.unity";
    const string 배터리레시피 = "Assets/08.Data/Recipes/battery_cell.asset";

    /// <summary>랜턴은 티어 1로 잰다. 사람이 처음부터 끝까지 들고 다니는 것이 그것이다.</summary>
    const int 티어 = 1;

    // ── 경계 ────────────────────────────────────────────────────

    [Test]
    public void 다_캔_직후에는_돌아오지_않았다()
    {
        Assert.IsFalse(HarvestRespawnRule.HasRespawned(100f, 100f, 180f));
        Assert.AreEqual(180f, HarvestRespawnRule.Remaining(100f, 100f, 180f), 0.001f);
    }

    [Test]
    public void 주기_직전에는_아직이다()
    {
        Assert.IsFalse(HarvestRespawnRule.HasRespawned(100f, 279.99f, 180f));
        Assert.AreEqual(0.01f, HarvestRespawnRule.Remaining(100f, 279.99f, 180f), 0.001f);
    }

    /// <summary>경계는 안쪽으로 친다 — 정확히 그 시각이면 돌아온 것이다.</summary>
    [Test]
    public void 주기가_되는_순간_돌아왔다()
    {
        Assert.IsTrue(HarvestRespawnRule.HasRespawned(100f, 280f, 180f));
        Assert.AreEqual(0f, HarvestRespawnRule.Remaining(100f, 280f, 180f), 0.001f);
    }

    [Test]
    public void 주기_직후에도_돌아와_있다()
    {
        Assert.IsTrue(HarvestRespawnRule.HasRespawned(100f, 280.01f, 180f));
        Assert.IsTrue(HarvestRespawnRule.HasRespawned(100f, 100000f, 180f));
    }

    /// <summary>
    /// 같은 경계 관례를 쓰는 이웃들과 어긋나지 않는다. 채집물마다 "정확히 그 시각"의
    /// 뜻이 다르면 플레이어는 무엇을 배워도 다음 자리에서 틀린다.
    /// </summary>
    [Test]
    public void 군락_갓과_그루터기와_같은_경계를_쓴다()
    {
        Assert.AreEqual(GlowGroveRule.HasRegrown(10f, 190f, 180f),
                        HarvestRespawnRule.HasRespawned(10f, 190f, 180f));
        Assert.AreEqual(MushroomLumberRule.HasRegrown(10f, 310f, 300f),
                        HarvestRespawnRule.HasRespawned(10f, 310f, 300f));
    }

    // ── 0은 실수가 아니다 ───────────────────────────────────────

    [Test]
    public void 재생이_0인_채집물은_아무리_기다려도_돌아오지_않는다()
    {
        Assert.IsFalse(HarvestRespawnRule.HasRespawned(0f, 1f, 0f));
        Assert.IsFalse(HarvestRespawnRule.HasRespawned(0f, 86400f, 0f));
        Assert.IsTrue(float.IsPositiveInfinity(HarvestRespawnRule.Remaining(0f, 86400f, 0f)),
            "영영 안 돌아오는 것에 남은 시간이 0이면 「방금 돌아왔다」와 구별되지 않는다");
    }

    /// <summary>
    /// <b>「합금 더미」가 0인 것은 설계다.</b> 표가 그렇게 말하고, 다른 채집물은
    /// 하나도 0이 아니다 — 0이 여기저기 흩어져 있으면 어느 것이 실수인지 알 수 없다.
    /// </summary>
    [Test]
    public void 돌아오지_않는_채집물은_합금_더미_하나뿐이다()
    {
        var 안돌아오는것 = HarvestRespawnRule.Names
            .Where(HarvestRespawnRule.NeverOnPurpose)
            .ToList();

        CollectionAssert.AreEqual(new[] { "OreVein" }, 안돌아오는것,
            "재생 0인 채집물이 「합금 더미」 말고 또 있다 — 실수인지 설계인지 표에 적어라");
    }

    /// <summary>
    /// 근거 한 마디: <b>노동은 게이트가 아니다.</b> 외계 합금이 무한히 나오면
    /// 티어 3 관문이 "시간만 들이면 열리는 것"이 된다. 그러니 매장 총량은 유한해야 한다.
    /// </summary>
    [Test]
    public void 합금_더미는_다_캐면_없어지는_매장지다()
    {
        var 광맥 = 정의("OreVein");
        Assert.AreEqual(0f, 광맥.respawnSeconds,
            "「합금 더미」가 재생하기 시작했다. 그러면 외계 합금 관문이 노동으로 열린다");
        Assert.AreEqual(0f, HarvestRespawnRule.SupplyPerMinute(기댓값(광맥, "alien_alloy"),
                                                              씬에_선_수("OreVein"),
                                                              광맥.respawnSeconds), 0.0001f);
    }

    /// <summary>
    /// 근거 두 마디: <b>그래도 잠기지 않는다.</b> 매장 총량이 두 레시피가 요구하는
    /// 합을 넉넉히 넘는다. 넘지 못하면 유한한 것이 관문이 아니라 <b>막다른 길</b>이 된다.
    /// </summary>
    [Test]
    public void 매장된_외계_합금이_두_레시피를_덮고도_남는다()
    {
        int 곳 = 씬에_선_수("OreVein");
        float 매장량 = 기댓값(정의("OreVein"), "alien_alloy") * 곳;

        int 필요 = 요구량("Assets/08.Data/Recipes/surface_walker.asset", "alien_alloy")
                 + 요구량("Assets/08.Data/Recipes/breach_pod.asset", "alien_alloy");

        Assert.Greater(필요, 0, "외계 합금을 요구하는 레시피가 사라졌다 — 예외의 근거가 끊겼다");
        Assert.Greater(매장량, 필요 * 1.5f,
            $"매장량 {매장량:F1}개로 요구량 {필요}개를 여유 있게 덮지 못한다 " +
            $"({곳}곳). 유한한 것이 관문이 아니라 막다른 길이 된다");
    }

    /// <summary>
    /// 근거 세 마디: <b>더 필요하면 길이 있다 — 낫이다.</b> 그 길은 노동이 아니라
    /// 위험을 지나간다. 이것이 끊기면 매장량을 다 쓴 판이 진짜로 잠긴다.
    /// </summary>
    [Test]
    public void 외계_합금은_낫에서도_나온다()
    {
        var 낫표 = AssetDatabase.LoadAssetAtPath<LootTableSO>("Assets/08.Data/LootTables/Scythe.asset");
        Assert.IsNotNull(낫표, "낫 전리품 표를 못 읽었다");
        Assert.Greater(낫표.entries.Where(e => e?.item != null && e.item.id == "alien_alloy")
                                   .Sum(e => e.chance * e.maxCount), 0f,
            "낫이 외계 합금을 흘리지 않는다 — 매장지가 마르면 되돌릴 길이 없다");
    }

    // ── 에셋과 표가 같은 말을 한다 ──────────────────────────────

    /// <summary>
    /// 실행부가 읽는 것은 에셋이고 근거가 적힌 것은 표다. 둘이 어긋나면
    /// 상수를 돌려도 게임이 안 바뀐다 — 화톳불 연료에서 실제로 겪은 일이다.
    /// </summary>
    [Test]
    public void 에셋의_재생_주기가_표와_같다()
    {
        foreach (var 이름 in HarvestRespawnRule.Names)
            Assert.AreEqual(HarvestRespawnRule.SecondsFor(이름), 정의(이름).respawnSeconds, 0.001f,
                            $"{이름}의 재생 주기가 표와 다르다");
    }

    /// <summary>표가 모르는 채집물이 08.Data에 서 있으면 그것은 표에 빠진 것이다.</summary>
    [Test]
    public void 저장소의_모든_채집물이_표에_있다()
    {
        var 빠진것 = AssetDatabase.FindAssets("t:HarvestNodeSO", new[] { 채집물폴더 })
            .Select(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)))
            .Where(n => !HarvestRespawnRule.Knows(n))
            .ToList();

        CollectionAssert.IsEmpty(빠진것,
            "표에 없는 채집물이 있다 — 재생 주기가 0인지 아닌지 근거가 없다: " +
            string.Join(", ", 빠진것));
    }

    // ── 장부 ────────────────────────────────────────────────────

    [Test]
    public void 스크랩_다섯_개가_배터리_셀_하나다()
    {
        var 레시피 = AssetDatabase.LoadAssetAtPath<RecipeSO>(배터리레시피);
        Assert.IsNotNull(레시피, "배터리 추출 레시피를 못 읽었다");

        int 스크랩 = 레시피.ingredients.Where(i => i?.item != null && i.item.id == "scrap")
                                        .Sum(i => i.count);
        Assert.AreEqual(HarvestRespawnRule.ScrapPerBatteryCell, 스크랩,
            "추출 레시피가 먹는 스크랩이 바뀌었다 — 장부의 환율이 어긋난다");
        Assert.AreEqual(1, 레시피.result.count, "셀 하나가 나온다는 전제가 깨졌다");
    }

    [Test]
    public void 랜턴은_분당_스크랩_네개_반을_태운다()
    {
        // 1.6/초 × 60 ÷ 셀당 100 × 스크랩 5개 = 4.8
        Assert.AreEqual(4.8f, HarvestRespawnRule.ScrapBurnedPerMinute(티어), 0.001f);
    }

    /// <summary>
    /// <b>이 라운드의 요점.</b> 세계의 정상 상태 공급이 랜턴 소모를 넘는다.
    /// 넘지 못하면 안 나가는 것이 최적해가 되어 게임이 멈춘다.
    /// </summary>
    [Test]
    public void 세계의_스크랩_공급이_랜턴_소모를_넘는다()
    {
        float 공급 = 스크랩_공급_분당();
        float 소모 = HarvestRespawnRule.ScrapBurnedPerMinute(티어);

        Assert.IsTrue(HarvestRespawnRule.Sustains(공급, 티어),
            $"세계가 마른다 — 공급 {공급:F2}/분 ≤ 소모 {소모:F2}/분");

        // 갓 넘기는 것으로는 모자란다. 스크랩은 랜턴만 먹는 것이 아니라
        // 도구·수리·건축·레이더까지 먹는다. 소모의 두 배는 넘어야 지어 볼 것이 남는다.
        Assert.Greater(공급 / 소모, 2f,
            $"공급이 소모의 {공급 / 소모:F2}배뿐이다 — 버는 것이 전부 불로 들어간다");
    }

    /// <summary>
    /// <b>열한 번째 분을 넘겨도 0으로 떨어지지 않는다.</b> 세계에 서 있는 스크랩
    /// 54개는 랜턴 11.25분어치다. 재생이 없으면 그 뒤로 순증가가 정확히 0이 되고
    /// 곧 음수로 내려간다. 재생을 켠 뒤에는 시간이 갈수록 <b>벌어진다</b>.
    /// </summary>
    [Test]
    public void 열한_번째_분을_넘겨도_순증가가_양수로_남는다()
    {
        float 앞선순증가 = float.NegativeInfinity;

        foreach (float 분 in new[] { 11f, 12f, 20f, 30f, 60f, 180f })
        {
            float 초 = 분 * 60f;
            float 벌이 = 누적_스크랩(초);
            float 태움 = HarvestRespawnRule.ScrapBurnedPerMinute(티어) * 분;
            float 순증가 = 벌이 - 태움;

            Assert.Greater(순증가, 0f,
                $"{분:F0}분 시점에 순증가가 {순증가:F1}이다 — 세계가 말랐다 " +
                $"(벌이 {벌이:F1} / 태움 {태움:F1})");
            Assert.Greater(순증가, 앞선순증가,
                $"{분:F0}분 시점에 순증가가 앞선 시점보다 줄었다");
            앞선순증가 = 순증가;
        }
    }

    /// <summary>
    /// 재생이 없던 예전에는 정확히 그 자리에서 무너졌다. 이 검사가 없으면
    /// 위 검사가 "원래도 통과했을 것"인지 알 수 없다.
    /// </summary>
    [Test]
    public void 재생을_끄면_열한_번째_분에서_무너진다()
    {
        float 재고 = 54f;                                   // 씬에 서 있는 스크랩 총량
        Assert.AreEqual(재고, 누적_스크랩(0f), 0.5f, "서 있는 스크랩 총량이 바뀌었다");

        float 태움 = HarvestRespawnRule.ScrapBurnedPerMinute(티어) * 12f;
        Assert.Less(재고 - 태움, 0f,
            "재생이 없으면 12분에 음수여야 한다 — 이 라운드가 고친 것이 그것이다");
    }

    /// <summary>
    /// <b>앉아서 기다리는 것이 답이 되면 안 된다.</b> 배터리 한 통(62.5초) 안에
    /// 노드가 돌아오면 탐사 대신 "노드 앞에서 한 통 태우기"가 최적해가 된다.
    /// </summary>
    [Test]
    public void 배터리_한_통으로는_어느_채집물도_기다릴_수_없다()
    {
        foreach (var 이름 in HarvestRespawnRule.Names)
        {
            float 초 = HarvestRespawnRule.SecondsFor(이름);
            if (초 <= 0f) continue;
            Assert.IsFalse(HarvestRespawnRule.WaitingBeatsWalking(초, 티어),
                $"{이름}({초:F0}초)은 배터리 한 통" +
                $"({LanternRule.FullBatterySecondsAtTier1:F1}초) 안에 돌아온다");
        }
    }

    /// <summary>도구를 요구하는 것은 맨손으로 줍는 것보다 느리게 돌아온다.</summary>
    [Test]
    public void 도구가_필요한_잔해가_더_느리게_돌아온다()
    {
        Assert.Greater(HarvestRespawnRule.DebrisSeconds, HarvestRespawnRule.LooseScrapSeconds);
        Assert.AreEqual(ToolType.None, 정의("LooseScrap").requiredTool);
        Assert.AreNotEqual(ToolType.None, 정의("Debris").requiredTool);
    }

    /// <summary>기존 재생값들의 자릿수를 벗어나지 않는다. 새 축을 만든 것이 아니다.</summary>
    [Test]
    public void 새_주기가_기존_관례의_자릿수_안에_있다()
    {
        foreach (var 이름 in new[] { "LooseScrap", "Debris" })
        {
            float 초 = HarvestRespawnRule.SecondsFor(이름);
            Assert.GreaterOrEqual(초, HarvestRespawnRule.GlowMushroomSeconds);
            Assert.LessOrEqual(초, MushroomLumberRule.RegrowSeconds);
        }
    }

    // ── 눈앞에서는 돌아오지 않는다 ──────────────────────────────

    [Test]
    public void 눈앞_정면은_보고_있는_것이다()
    {
        Assert.IsFalse(HarvestRespawnRule.IsUnseen(Vector3.zero, Vector3.forward,
                                                   new Vector3(0f, 0f, 3f)));
    }

    [Test]
    public void 등_뒤는_보고_있지_않다()
    {
        Assert.IsTrue(HarvestRespawnRule.IsUnseen(Vector3.zero, Vector3.forward,
                                                  new Vector3(0f, 0f, -3f)));
    }

    /// <summary>시야 반각이 경계다. 안쪽은 보이고 바깥쪽은 안 보인다.</summary>
    [Test]
    public void 시야_반각이_경계다()
    {
        var 눈 = Vector3.zero;
        var 앞 = Vector3.forward;
        float 안쪽 = HarvestRespawnRule.ViewHalfAngleDegrees - 1f;
        float 바깥 = HarvestRespawnRule.ViewHalfAngleDegrees + 1f;

        Assert.IsFalse(HarvestRespawnRule.IsUnseen(눈, 앞, 방위(안쪽, 3f)));
        Assert.IsTrue(HarvestRespawnRule.IsUnseen(눈, 앞, 방위(바깥, 3f)));
    }

    /// <summary>위아래도 함께 본다. 발밑을 보고 있으면 눈높이의 것은 화면에 없다.</summary>
    [Test]
    public void 발밑을_보면_눈높이의_것은_안_보인다()
    {
        Assert.IsTrue(HarvestRespawnRule.IsUnseen(Vector3.zero, Vector3.down,
                                                  new Vector3(0f, 0f, 3f)));
    }

    [Test]
    public void 멀면_어느_쪽이든_돌아와도_된다()
    {
        float 멀리 = HarvestRespawnRule.UnseenDistance + 0.1f;
        Assert.IsTrue(HarvestRespawnRule.IsUnseen(Vector3.zero, Vector3.forward,
                                                  new Vector3(0f, 0f, 멀리)));
        Assert.IsFalse(HarvestRespawnRule.IsUnseen(Vector3.zero, Vector3.forward,
                                                   new Vector3(0f, 0f, 멀리 - 0.2f)));
    }

    /// <summary>
    /// 기준 거리는 사람이 들고 다니는 빛의 끝이다. 그 밖은 애초에 안 보인다.
    ///
    /// <b>고정값을 못 박지 않는다.</b> 랜턴 오프셋은 §16이 사람에게 남긴 손잡이라
    /// 곧 움직인다. 그때 이 검사가 빨개지면 안 되고, 대신 <b>따라 움직였는지</b>를
    /// 본다 — 빛보다 가깝게 잡히면 빛 웅덩이 안에서 물체가 솟는다.
    /// </summary>
    [Test]
    public void 기준_거리는_티어1_랜턴이_정면으로_닿는_거리를_넘는다()
    {
        Assert.AreEqual(LanternRule.ForwardReachForTier(1) + 1f,
                        HarvestRespawnRule.UnseenDistance, 0.001f);
        Assert.Greater(HarvestRespawnRule.UnseenDistance, LanternRule.ForwardReachForTier(1));
        Assert.Less(HarvestRespawnRule.UnseenDistance, 40f,
                    "너무 멀게 잡으면 조건이 채워지지 않아 세계가 실제로 마른다");
    }

    /// <summary>
    /// <b>기다릴 뿐 시계를 되돌리지는 않는다.</b> 규칙에 "본다"가 시각을 미루는 길이
    /// 없다는 것을 경계로 확인한다 — 있으면 노드 옆에 서 있는 것만으로 그 자리를
    /// 영구히 죽일 수 있다.
    /// </summary>
    [Test]
    public void 보고_있어도_주기는_흐른다()
    {
        Assert.IsTrue(HarvestRespawnRule.HasRespawned(0f, 1000f, 180f));
        Assert.AreEqual(0f, HarvestRespawnRule.Remaining(0f, 1000f, 180f), 0.001f);
    }

    // ── 흔적 ────────────────────────────────────────────────────

    /// <summary>
    /// 흔적은 <b>납작</b>하다. 균일하게 줄이면 「작은 잔해 더미」로 읽혀
    /// 다 캔 자리인지 아닌지 구별되지 않는다.
    /// </summary>
    [Test]
    public void 흔적은_가로를_남기고_세로만_눌린다()
    {
        var s = HarvestRespawnRule.RemnantScale;
        Assert.Less(s.y, s.x * 0.5f, "납작하지 않으면 그냥 작은 것으로 읽힌다");
        Assert.AreEqual(s.x, s.z, 0.001f, "가로세로가 다르면 찌그러져 보인다");
        Assert.Greater(s.y, 0f, "0이면 흔적이 아니라 사라진 것이다");
        Assert.Less(s.x, 1f, "줄지 않으면 캔 티가 안 난다");
    }

    // ── 잔 도구들 ───────────────────────────────────────────────

    static Vector3 방위(float 도, float 거리)
    {
        float r = 도 * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(r) * 거리, 0f, Mathf.Cos(r) * 거리);
    }

    static HarvestNodeSO 정의(string 이름)
    {
        var so = AssetDatabase.LoadAssetAtPath<HarvestNodeSO>($"{채집물폴더}/{이름}.asset");
        Assert.IsNotNull(so, $"{이름} 채집물 정의를 못 읽었다");
        return so;
    }

    static float 기댓값(HarvestNodeSO 노드, string 아이템)
    {
        if (노드?.drops?.entries == null) return 0f;
        return 노드.drops.entries
            .Where(e => e?.item != null && e.item.id == 아이템)
            .Sum(e => e.chance * (Mathf.Min(e.minCount, e.maxCount) +
                                  Mathf.Max(e.minCount, e.maxCount)) * 0.5f);
    }

    static int 요구량(string 레시피경로, string 아이템)
    {
        var r = AssetDatabase.LoadAssetAtPath<RecipeSO>(레시피경로);
        if (r?.ingredients == null) return 0;
        return r.ingredients.Where(i => i?.item != null && i.item.id == 아이템).Sum(i => i.count);
    }

    /// <summary>
    /// 씬에 이 채집물이 몇 곳 서 있는가. 씬을 열지 않고 YAML 본문을 읽는다 —
    /// EditMode에서 씬을 열면 사람이 편집 중이던 씬을 밀어낸다
    /// (<c>AIslandResourceTests</c>와 같은 방식).
    /// </summary>
    static int 씬에_선_수(string 이름)
    {
        if (_배치 == null)
        {
            _배치 = new Dictionary<string, int>();
            string 본문 = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), 씬));
            foreach (Match m in Regex.Matches(본문,
                @"definition:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})"))
            {
                string 경로 = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                string n = Path.GetFileNameWithoutExtension(경로);
                if (string.IsNullOrEmpty(n)) continue;
                _배치.TryGetValue(n, out int c);
                _배치[n] = c + 1;
            }
        }
        return _배치.TryGetValue(이름, out int 수) ? 수 : 0;
    }

    static Dictionary<string, int> _배치;

    /// <summary>세계 전체의 스크랩 정상 상태 공급(분당).</summary>
    static float 스크랩_공급_분당()
    {
        float 합 = 0f;
        foreach (var 이름 in HarvestRespawnRule.Names)
        {
            var def = 정의(이름);
            합 += HarvestRespawnRule.SupplyPerMinute(기댓값(def, "scrap"),
                                                    씬에_선_수(이름), def.respawnSeconds);
        }
        return 합;
    }

    /// <summary>시작부터 이만큼 지나는 동안 세계가 내놓을 수 있었던 스크랩 총량.</summary>
    static float 누적_스크랩(float 초)
    {
        float 합 = 0f;
        foreach (var 이름 in HarvestRespawnRule.Names)
        {
            var def = 정의(이름);
            합 += HarvestRespawnRule.CumulativeYield(기댓값(def, "scrap"),
                                                    씬에_선_수(이름), def.respawnSeconds, 초);
        }
        return 합;
    }
}
