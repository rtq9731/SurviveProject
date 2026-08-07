using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Survive.Harvesting;
using Survive.Items;
using Survive.World;

/// <summary>
/// <b>난수에 주인이 있는가.</b>
///
/// 이 저장소의 난수는 주인이 <b>0</b>이었다 — <c>Random.InitState</c> 0건,
/// <c>System.Random</c>은 다섯 곳 전부 시각 시드. 그래서 같은 판을 두 번 돌려도
/// 같은 것이 나오지 않았고, 검증은 매번 다른 세계를 쟀다.
///
/// 여기서 넷을 못 박는다.
/// <list type="number">
/// <item><b>같은 세계·같은 자리는 같은 답</b>이고, <b>다른 자리는 다른 답</b>이다 —
///   시드 하나가 세계를 똑같이 만들지 않는다.</item>
/// <item><b>뽑는 순서에 안 기댄다.</b> 셋을 어떤 차례로 굴리든 각자의 답은 같다.</item>
/// <item><b>뒷문이 막혔다.</b> 난수를 주지 않으면 굴림이 실패한다.</item>
/// <item><b>주인 없는 난수가 코드에 다시 생기지 않는다.</b> 본문을 훑어 잡는다.</item>
/// </list>
/// </summary>
public class WorldSeedTests
{
    const int 씨앗 = 20260807;
    const int 다른씨앗 = 991;

    [SetUp]
    public void 세계를_앉힌다() => WorldSeed.Restore(씨앗);

    [TearDown]
    public void 비운다() => WorldSeed.Reset();

    // ── 표 만들기 ────────────────────────────────────────────

    static ItemDataSO 아이템(string id)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = 99;
        return it;
    }

    /// <summary>실제 전리품 표를 닮은 것 — 수량 범위도 있고 확률 항목도 있다.</summary>
    static LootTableSO 흔들리는표()
    {
        var t = ScriptableObject.CreateInstance<LootTableSO>();
        t.entries = new[]
        {
            new LootTableSO.Entry { item = 아이템("scrap"), minCount = 1, maxCount = 4, chance = 1f },
            new LootTableSO.Entry { item = 아이템("wing"),  minCount = 1, maxCount = 2, chance = 0.35f },
        };
        return t;
    }

    static string 적는다(IEnumerable<ItemStack> 굴림) =>
        string.Join(",", 굴림.Select(s => $"{s.item.id}x{s.count}"));

    static string 굴린다(LootTableSO 표, Vector3 자리, int 번째 = 0) =>
        적는다(표.Roll(WorldSeed.Rng(WorldSeedBranch.HarvestLoot, 자리, 번째)));

    // ── ① 같은 세계·같은 자리 → 같은 결과 ───────────────────

    [Test]
    public void 같은_시드_같은_자리는_같은_결과다()
    {
        var 표 = 흔들리는표();
        var 자리 = new Vector3(12.5f, 3f, -7f);

        string 처음 = 굴린다(표, 자리);
        for (int i = 0; i < 20; i++)
            Assert.AreEqual(처음, 굴린다(표, 자리), "같은 자리를 다시 굴렸는데 답이 달라졌다");
    }

    [Test]
    public void 세계를_다시_앉히면_처음부터_같은_결과가_나온다()
    {
        var 표 = 흔들리는표();
        var 자리 = new Vector3(4f, 0f, 4f);

        string 첫판 = 굴린다(표, 자리);

        WorldSeed.Reset();
        WorldSeed.Restore(씨앗);

        Assert.AreEqual(첫판, 굴린다(표, 자리),
            "같은 시드로 다시 세운 세계가 다른 것을 떨궜다 — 그러면 저장을 건널 수 없다");
    }

    // ── ② 다른 자리 → 다른 결과 ─────────────────────────────

    [Test]
    public void 시드_하나가_세계를_똑같이_만들지_않는다()
    {
        var 표 = 흔들리는표();
        var 나온것 = new HashSet<string>();

        for (int x = 0; x < 12; x++)
            for (int z = 0; z < 12; z++)
                나온것.Add(굴린다(표, new Vector3(x * 3f, 0f, z * 3f)));

        // 표가 낼 수 있는 모양은 scrap 1~4 × (wing 없음·1·2) = 12가지다.
        // 절반도 못 채우면 자리가 결과에 거의 안 섞이고 있다는 뜻이다.
        Assert.Greater(나온것.Count, 6,
            $"자리 144곳에서 나온 모양이 {나온것.Count}가지뿐이다. " +
            "시드 하나를 그대로 쓰면 모든 채집물이 같은 것을 떨군다");
    }

    [Test]
    public void 바로_옆_자리도_따로_논다()
    {
        // 이웃한 씨앗이 닮은 값을 내는 것은 System.Random의 알려진 성질이다.
        // 흩기(Avalanche)가 없으면 여기서 결이 보인다.
        var 나온것 = new HashSet<int>();
        for (int x = 0; x < 40; x++)
            나온것.Add(WorldSeed.Rng(WorldSeedBranch.HarvestLoot,
                                     new Vector3(x * WorldId.Grid, 0f, 0f)).Next(0, 1000));

        Assert.Greater(나온것.Count, 30,
            $"나란히 선 40자리에서 나온 값이 {나온것.Count}가지뿐이다 — 이웃이 닮았다");
    }

    [Test]
    public void 갈래가_다르면_같은_자리에서도_다른_답이다()
    {
        var 자리 = new Vector3(9f, 1f, 2f);
        Assert.AreNotEqual(WorldSeed.Mix(WorldSeedBranch.HarvestLoot, 자리, 0),
                           WorldSeed.Mix(WorldSeedBranch.DropScatter, 자리, 0),
            "갈래를 안 섞으면 전리품과 흩뿌림이 같은 흐름을 나눠 쓴다");
    }

    [Test]
    public void 굴림_횟수가_다르면_다시_자란_뒤에_다른_것이_나온다()
    {
        var 자리 = new Vector3(-6f, 0f, 11f);
        var 나온것 = new HashSet<int>();
        for (int n = 0; n < 20; n++)
            나온것.Add(WorldSeed.Mix(WorldSeedBranch.HarvestLoot, 자리, n));

        Assert.AreEqual(20, 나온것.Count,
            "같은 자리의 굴림 20번이 서로 다른 파생을 못 냈다 — 다시 자란 덤불이 늘 같은 것을 떨군다");
    }

    [Test]
    public void 씨앗이_다르면_같은_자리에서도_세계가_갈린다()
    {
        var 표 = 흔들리는표();
        var 자리들 = Enumerable.Range(0, 30).Select(i => new Vector3(i * 2f, 0f, i)).ToArray();

        var 이쪽 = 자리들.Select(p => 굴린다(표, p)).ToArray();

        WorldSeed.Restore(다른씨앗);
        var 저쪽 = 자리들.Select(p => 굴린다(표, p)).ToArray();

        int 갈린수 = 이쪽.Where((v, i) => v != 저쪽[i]).Count();
        Assert.Greater(갈린수, 10,
            $"씨앗을 바꿨는데 자리 30곳 중 {갈린수}곳만 갈렸다 — 시드가 결과에 안 닿고 있다");
    }

    // ── ③ 뽑는 순서에 안 기댄다 ─────────────────────────────

    [Test]
    public void 뽑는_순서에_기대지_않는다()
    {
        var 표 = 흔들리는표();
        var 자리들 = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(5f, 0f, 5f),
            new Vector3(-3f, 2f, 8f),
            new Vector3(21f, 0f, -14f),
        };

        var 차례대로 = 자리들.Select(p => 굴린다(표, p)).ToArray();

        // 거꾸로, 그리고 사이사이에 <b>다른 갈래의 굴림을 끼워</b> 다시 굴린다.
        // 흐름을 나눠 쓰는 구현이라면 여기서 답이 어긋난다.
        WorldSeed.Restore(씨앗);
        var 거꾸로 = new string[자리들.Length];
        for (int i = 자리들.Length - 1; i >= 0; i--)
        {
            WorldSeed.Rng(WorldSeedBranch.DropScatter, 자리들[i]).NextDouble();
            거꾸로[i] = 굴린다(표, 자리들[i]);
            WorldSeed.Rng(WorldSeedBranch.CreatureLoot, 자리들[i]).Next();
        }

        CollectionAssert.AreEqual(차례대로, 거꾸로,
            "굴리는 차례를 바꿨더니 답이 달라졌다 — 코옵에서는 이것이 곧 세계가 갈리는 자리다");
    }

    // ── ④ 뒷문이 막혔다 ─────────────────────────────────────

    [Test]
    public void 난수를_주지_않으면_굴림이_실패한다()
    {
        var 표 = 흔들리는표();
        Assert.Throws<System.ArgumentNullException>(() => 표.Roll(null),
            "주인 없는 난수를 만들어 메우면 그 자리가 눈에 안 띈다");
    }

    /// <summary>
    /// <b>음성 확인.</b> 위의 실패가 「굴림이 원래 안 되는 것」이 아니라
    /// <b>난수를 안 줘서</b>임을 보인다 — 주면 멀쩡히 굴러간다.
    /// </summary>
    [Test]
    public void 난수를_주면_그_굴림은_성공한다()
    {
        var 표 = 흔들리는표();
        Assert.DoesNotThrow(() => 표.Roll(WorldSeed.Rng(WorldSeedBranch.HarvestLoot, Vector3.zero)));
    }

    /// <summary>
    /// <b>뒷문이 다시 열리는 것을 막는다.</b> 폴백 한 줄은 지우기는 쉬워도
    /// 되살리기는 더 쉽다 — 다음 사람이 "널 체크가 없네" 하며 되돌린다.
    /// 본문을 훑어 <b>시각 시드로 짓는 난수</b>가 하나도 없음을 못 박는다.
    /// </summary>
    [Test]
    public void 본문에_주인_없는_난수가_없다()
    {
        var 무늬 = new Regex(@"new\s+(System\s*\.\s*)?Random\s*\(\s*\)", RegexOptions.Compiled);
        var 걸린것 = new List<string>();

        foreach (string 폴더 in new[] { "02.Scripts", "09.Tests" })
        {
            string 뿌리 = Path.Combine(Application.dataPath, 폴더);
            if (!Directory.Exists(뿌리)) continue;

            foreach (string 파일 in Directory.GetFiles(뿌리, "*.cs", SearchOption.AllDirectories))
            {
                // 주석은 뺀다. 이 뒷문의 역사를 코드 옆에 적어 두는 것은 옳은 일이다.
                string 본문 = Survive.Localization.LocSourceScanner.StripComments(File.ReadAllText(파일));
                var m = 무늬.Match(본문);
                if (!m.Success) continue;

                int 줄 = 본문.Take(m.Index).Count(c => c == '\n') + 1;
                걸린것.Add($"{파일.Substring(Application.dataPath.Length + 1).Replace('\\', '/')}:{줄}");
            }
        }

        Assert.IsEmpty(걸린것,
            "시각으로 씨를 뿌리는 난수가 남아 있다. 난수의 주인은 WorldSeed 하나다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    // ── 파생의 약속 ─────────────────────────────────────────

    [Test]
    public void 파생값은_언제나_System_Random이_받을_수_있는_값이다()
    {
        // System.Random은 int.MinValue를 못 받는다. 음수를 안 내는 것으로 닫는다.
        for (int i = 0; i < 500; i++)
        {
            WorldSeed.Restore(i * 7919 - 100000);
            int v = WorldSeed.Mix(WorldSeedBranch.CreatureLoot,
                                  new Vector3(i * -13.5f, i, i * 0.5f), i);
            Assert.GreaterOrEqual(v, 0, $"{i}번째 파생이 음수다");
            Assert.DoesNotThrow(() => new System.Random(v).Next());
        }
    }

    [Test]
    public void 격자보다_가까운_흔들림은_같은_자리로_친다()
    {
        // 원장의 신원과 같은 격자여야 「저장된 그 자리」와 「굴린 그 자리」가
        // 어긋나지 않는다. 물리가 1mm 밀어 놓은 것으로 전리품이 바뀌면 안 된다.
        Assert.AreEqual(WorldSeed.Mix(WorldSeedBranch.HarvestLoot, new Vector3(10f, 0f, 4f), 0),
                        WorldSeed.Mix(WorldSeedBranch.HarvestLoot, new Vector3(10.004f, 0.003f, 3.998f), 0));
    }

    [Test]
    public void 새_세계는_0을_시드로_삼지_않는다()
    {
        // 0은 저장본에서 「시드를 적기 전」이라는 뜻으로 쓰인다.
        for (int i = 0; i < 200; i++) Assert.AreNotEqual(0, WorldSeed.Fresh());
    }

    [Test]
    public void 새_세계마다_다른_시드다()
    {
        var 본것 = new HashSet<int>();
        for (int i = 0; i < 100; i++) 본것.Add(WorldSeed.NewWorld());
        Assert.Greater(본것.Count, 95, "새 세계를 100번 세웠는데 시드가 겹쳤다");
    }

    [Test]
    public void 비우면_세워지지_않은_상태로_돌아간다()
    {
        WorldSeed.NewWorld();
        Assert.IsTrue(WorldSeed.Started);

        WorldSeed.Reset();
        Assert.IsFalse(WorldSeed.Started);
        Assert.AreEqual(0, WorldSeed.Value);
    }

    // ── 저장 ────────────────────────────────────────────────

    [Test]
    public void 시드가_세계_절에_실린다()
    {
        var 원장 = new WorldLedger();
        원장.Put(new WorldRecord { id = "harvest@1_0_1", kind = WorldLedgerScope.Harvest, gone = true, at = 3f });

        var 절 = 원장.Capture(12.5f, 씨앗);
        Assert.AreEqual(씨앗, 절.seed, "시드는 시각과 같은 절에 실린다");
        Assert.AreEqual(12.5f, 절.clockSeconds);
    }

    [Test]
    public void 시드가_없던_저장본은_0으로_읽힌다()
    {
        // 0을 만나면 불러오는 쪽이 이번 판의 시드를 그대로 둔다
        // (WorldLedgerService.RestoreState). 옛 저장본 전부를 같은 세계로
        // 만들어 버리지 않는 것이 이 0의 쓸모다.
        var 절 = new WorldLedger().Capture(1f);
        Assert.AreEqual(0, 절.seed);
    }
}
