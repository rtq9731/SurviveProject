using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// <b>난수의 갈래 이름.</b> 시드 하나에서 갈래별로 파생할 때 쓰는 꼬리표다.
    ///
    /// <b>왜 갈래를 나누는가.</b> 세계 시드 하나를 그대로 <c>System.Random</c>에
    /// 넣으면 전리품과 흩뿌림과 수확량이 <b>같은 흐름을 나눠 쓰게</b> 되고,
    /// 그러면 어느 하나가 한 번 더 뽑는 순간 그 뒤의 모든 갈래가 어긋난다.
    /// 갈래 이름을 섞어 두면 갈래마다 <b>서로 무관한</b> 흐름이 된다.
    ///
    /// <b>이름을 바꾸면 세계가 바뀐다.</b> 여기 적힌 글자가 곧 파생의 재료라,
    /// 이름을 고치는 것은 저장본에 적힌 시드의 뜻을 바꾸는 것과 같다.
    /// 갈래를 <b>더하는</b> 것은 안전하고, <b>고치는</b> 것은 아니다.
    /// </summary>
    public static class WorldSeedBranch
    {
        /// <summary>채집 노드가 떨구는 것.</summary>
        public const string HarvestLoot = "loot.harvest";

        /// <summary>자라는 식물이 단계마다 내놓는 것.</summary>
        public const string PlantLoot = "loot.plant";

        /// <summary>생물이 죽으며 떨구는 것.</summary>
        public const string CreatureLoot = "loot.creature";

        /// <summary>군락의 갓 한 무더기에서 나오는 개수.</summary>
        public const string GlowCapYield = "yield.glowcap";

        /// <summary>떨어진 물건이 튀어 내려앉는 자리.</summary>
        public const string DropScatter = "drop.scatter";

        /// <summary>
        /// 배회 방향과 지속. <b>아직 쓰는 데가 없다</b> —
        /// <c>Creatures/</c>는 다른 갈래가 잡고 있다. 이름만 먼저 잡아 둔 것은,
        /// 나중에 이 파일을 다시 열어 이름을 고치면 <b>이미 저장된 시드의 뜻이
        /// 바뀌기</b> 때문이다.
        /// </summary>
        public const string Wander = "move.wander";

        /// <summary>무엇을 흘릴지. <b>아직 쓰는 데가 없다</b>(위와 같은 이유).</summary>
        public const string RelicShed = "shed.relic";
    }

    /// <summary>
    /// <b>세계의 난수. 「이 굴림은 무엇이 정하는가」에 답하는 자리는 여기 하나다.</b>
    ///
    /// <b>주인이 0이었다.</b> <c>Random.InitState</c>는 한 번도 불리지 않았고
    /// <c>System.Random</c>은 전부 <c>new System.Random()</c>(시각 시드)이었다.
    /// 그래서 같은 판을 두 번 돌려도 같은 것이 나오지 않았고, 검증은 매번
    /// <b>다른 세계</b>를 쟀다. 「낫 꼬리」 36% 플레이크가 그 성질이었고
    /// 고치는 데 라운드 하나가 들었다.
    ///
    /// <b>주인은 하나, 갈래는 여럿이다.</b> 저장본에 실리는 것은
    /// <see cref="Value"/> 하나뿐이고, 굴리는 자리마다
    /// <see cref="Rng(string, Vector3, int)"/>가 그 하나에서 파생한다.
    /// <see cref="WorldClock"/>·<see cref="WorldLedgerService"/>와 같은 결이다 —
    /// 창구가 하나여야 서로 다른 답이 나오지 않는다.
    ///
    /// <b>흐름을 나눠 쓰지 않는다.</b> 굴릴 때마다 <b>새 <c>System.Random</c></b>을
    /// 짓고 그 자리에서 버린다. 하나의 흐름을 여럿이 나눠 쓰면 「누가 먼저
    /// 뽑았는가」가 결과를 바꾸는데, <c>CreatureBrain</c>의 주석이 이미 그
    /// 위험을 안다 — <i>"뽑는 순서를 바꾸면 무리 전체의 배회 모양이 달라진다."</i>
    /// 순서에 안 기대는 모양이라야 코옵에서도 안 깨진다. 값이 오로지
    /// <b>(시드, 갈래, 자리, 몇 번째 굴림)</b>에서만 나오므로, 세 자리를
    /// 어떤 차례로 굴리든 각자의 답은 같다.
    ///
    /// <b>자리를 섞는 이유.</b> 시드 하나를 그대로 쓰면 세계의 모든 채집물이
    /// 같은 것을 떨군다. 자리를 섞으면 <b>한 세계 안에서는 자리마다 다르고</b>,
    /// <b>세계가 같으면 그 자리는 언제나 같다</b>. 자리를 반올림하는 격자는
    /// <see cref="WorldId.Grid"/>와 같은 값이다 — 원장의 신원과 어긋나면
    /// 「저장된 그 자리」와 「굴린 그 자리」가 다른 것을 가리키게 된다.
    ///
    /// <b>몇 번째 굴림인가도 섞는다.</b> 안 그러면 다시 자란 덤불이 <b>영영
    /// 같은 것</b>만 떨구고, 한 번에 여러 개를 떨구면 전부 <b>같은 자리</b>에
    /// 겹쳐 떨어진다. 이 숫자는 굴리는 쪽이 자기 것으로 들고 센다 —
    /// 여기에 표를 두면 그것이 곧 숨은 전역 상태가 되고, 검증이 비울 수 없다.
    ///
    /// <b>저장에 들어간다.</b> 시드는 <b>세계 상태</b>다. 불러온 세계가 다른
    /// 것을 떨구면 그것은 같은 세계가 아니고, 저장 직전에 본 「저 덤불에는
    /// 무엇이 있다」가 불러오는 순간 거짓이 된다. 시각이 저장되는 것과 같은
    /// 이유이고, 싣는 자리도 같다 — 저장본의 「세계」 절
    /// (<see cref="WorldLedgerState.seed"/>).
    ///
    /// <b>굴림 자체는 그때그때다.</b> 저장하는 것은 시드뿐이고 굴림의 결과는
    /// 적지 않는다. 결과를 적으면 <b>세계의 모든 자리</b>에 대해 미리 굴려
    /// 두어야 하는데, 그것은 원장이 「달라진 것만 적는다」로 이미 거절한 모양이다.
    /// </summary>
    public static class WorldSeed
    {
        /// <summary>이 세계의 시드. 저장본에 실리는 값이 이것 하나다.</summary>
        public static int Value { get; private set; }

        /// <summary>세계가 한 번이라도 세워졌는가.</summary>
        public static bool Started { get; private set; }

        /// <summary>
        /// <b>재생을 켤 때마다 새 세계다.</b> 정적인 값은 도메인 리로드를 끈
        /// 에디터에서 앞 판을 살아서 넘어온다 — <see cref="WorldClock"/>이
        /// 같은 이유로 같은 자리에서 비운다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void 판이_바뀌면_새_세계다()
        {
            Reset();
            NewWorld();
        }

        /// <summary>
        /// 새 세계의 시드를 뽑아 앉힌다. 불러오기가 뒤따르면
        /// <see cref="Restore"/>가 이 값을 덮는다.
        /// </summary>
        /// <returns>앉힌 시드.</returns>
        public static int NewWorld()
        {
            Value = Fresh();
            Started = true;
            return Value;
        }

        /// <summary>
        /// 이 시드의 세계로 갈아 끼운다. <b>불러오기가 이 문을 쓴다</b> —
        /// 그리고 검증이 「같은 세계를 두 번」 만들 때도 이 문이다.
        /// </summary>
        public static void Restore(int seed)
        {
            Value = seed;
            Started = true;
        }

        /// <summary>검증·씬 전환 사이에 비운다. 시드는 0, 세워지지 않은 상태다.</summary>
        public static void Reset()
        {
            Value = 0;
            Started = false;
        }

        /// <summary>
        /// 새 세계에 줄 시드 하나. <b>0은 내지 않는다</b> — 저장본의 0은
        /// 「시드를 적기 전의 저장본」이라는 뜻으로 쓰이기 때문이다
        /// (<see cref="WorldLedgerState.seed"/>).
        ///
        /// 시각이 아니라 <c>Guid</c>에서 뽑는다. 세 벌의 에디터가 같은 밀리초에
        /// 재생을 켜는 일이 실제로 있고, 그때 시각 시드는 <b>같은 세계</b>를 준다.
        /// </summary>
        public static int Fresh()
        {
            int v = System.Guid.NewGuid().GetHashCode();
            return v == 0 ? 1 : v;
        }

        // ── 파생 ─────────────────────────────────────────────────

        /// <summary>
        /// 이 갈래의, 이 자리의, 이 번째 굴림에 쓸 난수.
        /// <b>쓰고 버린다</b> — 들고 있으면 그 순간부터 순서에 기대게 된다.
        /// </summary>
        /// <param name="branch"><see cref="WorldSeedBranch"/>의 이름 중 하나.</param>
        /// <param name="site">세계 좌표. <see cref="WorldId.Grid"/>로 반올림된다.</param>
        /// <param name="occasion">
        /// 이 자리에서 <b>몇 번째</b> 굴림인가. 한 자리에서 두 번 굴리는데 같은
        /// 값을 주면 같은 답이 나온다 — 그것을 원할 때만 같은 값을 준다.
        /// </param>
        public static System.Random Rng(string branch, Vector3 site, int occasion = 0)
            => new System.Random(Mix(branch, site, occasion));

        /// <summary>
        /// 파생 시드 그 자체. 항상 0 이상이다 —
        /// <c>System.Random</c>은 <c>int.MinValue</c>를 못 받는다.
        /// </summary>
        public static int Mix(string branch, Vector3 site, int occasion)
        {
            uint h = FnvOffset;

            Feed(ref h, unchecked((uint)Value));
            Feed(ref h, unchecked((uint)Grid(site.x)));
            Feed(ref h, unchecked((uint)Grid(site.y)));
            Feed(ref h, unchecked((uint)Grid(site.z)));
            Feed(ref h, unchecked((uint)occasion));

            if (!string.IsNullOrEmpty(branch))
                for (int i = 0; i < branch.Length; i++)
                    Feed(ref h, branch[i]);

            return (int)(Avalanche(h) & 0x7FFFFFFFu);
        }

        /// <summary>자리를 원장과 <b>같은 격자</b>로 반올림한다.</summary>
        static int Grid(float v) => Mathf.RoundToInt(v / WorldId.Grid);

        // ── 섞개 ─────────────────────────────────────────────────
        //
        // <b>왜 string.GetHashCode를 안 쓰는가.</b> 그 값은 런타임이 정하는 것이라
        // 판마다·플랫폼마다 다를 수 있다. 시드가 저장본을 건너야 하므로 섞는
        // 규칙도 우리 것이어야 한다. FNV-1a는 짧고, 여기 적힌 그대로가 곧 규칙이다.

        const uint FnvOffset = 2166136261u;
        const uint FnvPrime = 16777619u;

        static void Feed(ref uint h, uint value)
        {
            for (int i = 0; i < 4; i++)
            {
                h ^= (value >> (i * 8)) & 0xFFu;
                h *= FnvPrime;
            }
        }

        /// <summary>
        /// 마지막 한 번 더 흩는다(murmur3의 마무리).
        ///
        /// <b>없으면 이웃한 자리가 닮은 답을 낸다.</b> <c>System.Random</c>의
        /// 씨앗은 뺄셈식 생성기의 초기 배열을 <b>씨앗에 선형으로</b> 채우므로,
        /// 가까운 씨앗은 <b>첫 몇 개의 값</b>이 나란히 움직인다. 그러면 옆에
        /// 붙어 선 두 덤불이 매번 같은 개수를 떨구는, 눈에 보이는 결이 생긴다.
        /// </summary>
        static uint Avalanche(uint h)
        {
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }
}
