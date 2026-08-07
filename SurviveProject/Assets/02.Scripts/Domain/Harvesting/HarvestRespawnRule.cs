using UnityEngine;
using Survive.World;

namespace Survive.Harvesting
{
    /// <summary>
    /// 채집물이 다시 차오르는 규칙. <b>세계가 마르지 않게 하는 자리는 여기 하나다.</b>
    ///
    /// <b>왜 이것이 없으면 게임이 성립하지 않는가.</b> 랜턴은 매 초 배터리를 태우고
    /// 배터리는 스크랩에서 나온다(추출 레시피 스크랩 5개 → 셀 1개). 그런데 지상에
    /// 서 있는 스크랩의 총량은 유한하고, 재생이 0이면 그 뒤로 순증가가 영영 0이다.
    /// 즉 <b>길게 보면 반드시 음수</b>이고, 랜턴 지속시간을 어떻게 잡든 압박 곡선이
    /// 성립하지 않는다 — 안 나가는 것이 최적해가 되는 순간 게임이 멈춘다.
    ///
    /// <b>왜 새 축이 아니라 이미 있는 축인가.</b> 재생 자체는 이미 있다 —
    /// 발광 버섯 90초, 무광버섯 150초, 매크로늄 석영 300초, 거대 버섯 그루터기
    /// 300초(<see cref="MushroomLumberRule.RegrowSeconds"/>), 군락의 갓 180초
    /// (<see cref="GlowGroveRule.RegrowSeconds"/>). 잔해만 0으로 남아 있었다.
    /// 그러니 여기서 하는 일은 <b>이미 있는 축을 잔해에도 켜는 것</b>이고,
    /// 그래서 값도 그 관례의 자릿수 안에서 고른다.
    ///
    /// 수치는 08.Data의 에셋이 들고 있고 실행부가 그것을 읽지만, <b>주인은 여기다</b> —
    /// 에셋과 이 표가 어긋나면 EditMode 테스트가 빨개진다. 프리팹에 사본이 있으면
    /// 상수를 돌려도 게임이 안 바뀐다는 것은 화톳불 연료에서 이미 겪은 일이고,
    /// 여기서는 반대로 <b>에셋이 원본이고 상수가 검사자</b>가 되게 묶었다.
    /// </summary>
    public static class HarvestRespawnRule
    {
        // ══ 주기 ════════════════════════════════════════════════
        // 두 가지가 값을 정한다. 하나는 <b>기존 관례의 자릿수</b>(90~300초)이고,
        // 다른 하나는 <b>세계의 공급률이 랜턴 소모를 얼마나 넘느냐</b>다.
        // 아래 둘을 고르면 지상 전체의 스크랩 공급이 정확히 분당 14.4개가 되고,
        // 랜턴이 태우는 4.8개의 <b>3배</b>가 된다 (아래 「장부」 참조).

        /// <summary>
        /// 「흩어진 잔해」— <b>180초.</b> 맨손으로 줍는 가장 흔한 것이라
        /// 군락의 갓(<see cref="GlowGroveRule.RegrowSeconds"/>)과 같은 자리에 둔다.
        ///
        /// <b>기다림이 답이 되면 안 된다.</b> 티어 1 랜턴은 완충 100을 초당 1.6으로
        /// 태우므로 한 통이 62.5초다. 재생이 그보다 짧으면 "노드 앞에 앉아 랜턴
        /// 켜 놓고 기다리기"가 정답이 되고, 그러면 탐사가 사라진다. 180초는 배터리
        /// 세 통에 가까워 앉아서 버틸 수 있는 길이가 아니다 — 군락 갓이 180초인
        /// 이유와 같은 셈이다.
        /// </summary>
        public const float LooseScrapSeconds = 180f;

        /// <summary>
        /// 「기계 잔해」— <b>300초.</b> 곡괭이가 있어야 부술 수 있고 한 자리에서
        /// 스크랩 3개가 나온다. <b>도구를 요구하는 것은 느리게 돌아온다</b>는 것이
        /// 이 세계의 관례다 (매크로늄 석영 300초, 거대 버섯 300초). 맨손으로 줍는
        /// 것과 같은 속도로 돌아오면 곡괭이를 만든 값이 산출량 한 줄로만 남는다.
        /// </summary>
        public const float DebrisSeconds = 300f;

        /// <summary>「발광 버섯」— 90초. 이미 그렇게 서 있던 값이다.</summary>
        public const float GlowMushroomSeconds = 90f;

        /// <summary>「무광버섯」— 150초. 이미 그렇게 서 있던 값이다.</summary>
        public const float MatteMushroomSeconds = 150f;

        /// <summary>「매크로늄 석영」— 300초. 이미 그렇게 서 있던 값이다(지하용).</summary>
        public const float MacroniumQuartzSeconds = 300f;

        /// <summary>
        /// 「합금 더미」— <b>0. 돌아오지 않는다. 이것은 실수가 아니라 설계다.</b>
        ///
        /// <list type="number">
        /// <item><b>노동은 게이트가 아니다</b>(기획서의 관통 원칙). 이종 합금은
        ///   「액면 보행 장비」(6개)와 「돌파정」(4개)이 요구하는 티어 3 물질이고,
        ///   무한히 나오면 그 관문은 <b>시간만 들이면 열리는 것</b>이 된다.
        ///   광맥은 다 캐면 없어지는 <b>매장지</b>여야 관문이 관문으로 남는다.</item>
        /// <item><b>그래도 잠기지 않는다.</b> 씬에 열세 곳이 서 있고 한 곳이
        ///   평균 1.5개를 주므로 매장 총량은 19.5개다. 두 레시피가 요구하는
        ///   합은 10개이므로 <b>거의 두 배의 여유</b>가 있다.</item>
        /// <item><b>재생하는 출처가 이미 따로 있다 — 낫이다.</b> 낫을 잡으면
        ///   70% 확률로 1~2개가 나온다(LootTables/Scythe). 즉 이종 합금이 더
        ///   필요하면 길은 있고, 그 길은 <b>노동이 아니라 위험</b>을 지나간다.
        ///   그것이 티어 3 물질에 맞는 모양이다.</item>
        /// </list>
        /// </summary>
        public const float OreVeinNever = 0f;

        /// <summary>08.Data/HarvestNodes에 있는 채집물 에셋 이름들. 표의 열쇠다.</summary>
        public static readonly string[] Names =
        {
            "LooseScrap", "Debris", "Mushroom", "MatteMushroom", "MacroniumQuartz", "OreVein"
        };

        /// <summary>
        /// 이 에셋의 재생 주기(초). 0이면 <b>일부러</b> 돌아오지 않는 것이고,
        /// 표에 없는 이름이면 <see cref="float.NaN"/>이다 — 모르는 것을 0으로
        /// 답하면 "재생 안 함"과 "표에 없음"이 구별되지 않는다.
        /// </summary>
        public static float SecondsFor(string assetName)
        {
            switch (assetName)
            {
                case "LooseScrap": return LooseScrapSeconds;
                case "Debris": return DebrisSeconds;
                case "Mushroom": return GlowMushroomSeconds;
                case "MatteMushroom": return MatteMushroomSeconds;
                case "MacroniumQuartz": return MacroniumQuartzSeconds;
                case "OreVein": return OreVeinNever;
                default: return float.NaN;
            }
        }

        /// <summary>표가 이 이름을 아는가.</summary>
        public static bool Knows(string assetName) => !float.IsNaN(SecondsFor(assetName));

        /// <summary>
        /// 이 채집물이 <b>일부러</b> 재생하지 않는가. 표에 없는 이름은 false다 —
        /// 모르는 것을 "의도된 0"이라고 말하면 실수를 설계로 둔갑시킨다.
        /// </summary>
        public static bool NeverOnPurpose(string assetName)
        {
            float s = SecondsFor(assetName);
            return !float.IsNaN(s) && s <= 0f;
        }

        // ══ 경계 ════════════════════════════════════════════════

        /// <summary>
        /// 다 캔 지 <paramref name="respawnSeconds"/>가 지났는가.
        /// <b>경계는 안쪽으로 친다</b> — 정확히 그 시각이면 돌아온 것이다
        /// (<see cref="GlowGroveRule.HasRegrown"/>·<see cref="MushroomLumberRule.HasRegrown"/>과
        /// 같은 관례). 주기가 0 이하면 언제가 되어도 돌아오지 않는다.
        /// </summary>
        public static bool HasRespawned(float depletedAt, float now, float respawnSeconds)
            => respawnSeconds > 0f && now - depletedAt >= respawnSeconds;

        /// <summary>
        /// 돌아오기까지 남은 초. 영영 돌아오지 않는 것은 무한이다 —
        /// 0을 돌려주면 "지금 막 돌아왔다"와 구별되지 않는다.
        /// </summary>
        public static float Remaining(float depletedAt, float now, float respawnSeconds)
            => respawnSeconds <= 0f
                ? float.PositiveInfinity
                : Mathf.Max(0f, respawnSeconds - (now - depletedAt));

        // ══ 눈앞에서는 돌아오지 않는다 ══════════════════════════
        // <b>보는 앞에서 물체가 튀어나오면 규칙이 아니라 사고로 읽힌다.</b> 시간이
        // 다 됐다는 것과 지금 돌아와도 되는가는 다른 물음이고, 뒤엣것에 답하는 것이
        // 여기다. 조건을 못 채우면 <b>기다린다</b> — 시계를 되돌리지는 않는다.
        // 되돌리면 노드 옆에 서 있는 것만으로 그 자리를 영구히 죽일 수 있다.

        /// <summary>
        /// 이보다 멀면 방향과 상관없이 돌아와도 된다(m).
        ///
        /// <b>사람이 들고 다니는 빛의 끝이 기준이다.</b> 티어 1 랜턴이 정면으로
        /// 닿는 거리가 11m이므로(<see cref="LanternRule.ForwardReachForTier"/>)
        /// 그 밖은 애초에 보이지 않는다. 1m를 더 두는 것은 웅덩이 가장자리에서
        /// 차오르는 것이 곁눈에 걸리지 않게 하려는 여유다.
        ///
        /// 상위 티어는 이보다 멀리 본다. 그래도 기준을 티어 1에 두는 이유는,
        /// 12m 밖에서 잔해 하나가 0.6초에 걸쳐 제 크기로 부풀어 오르는 것은
        /// 눈에 띄지 않는 반면, 기준을 티어 3(27m)에 맞추면 <b>어지간해서는
        /// 조건이 채워지지 않아</b> 세계가 실제로 마르기 때문이다.
        /// </summary>
        public static float UnseenDistance => LanternRule.ForwardReachForTier(1) + 1f;

        /// <summary>
        /// 시야로 치는 반각(도). 넘어가면 화면 밖이다.
        ///
        /// 60도 화각 카메라의 수평 화각은 16:9에서 90도 남짓이라 반각 46도다.
        /// 60도는 그보다 넉넉해서, 화면 가장자리에 걸친 것도 "봤다"로 친다.
        /// </summary>
        public const float ViewHalfAngleDegrees = 60f;

        /// <summary>
        /// 이 자리가 지금 사람 눈에 안 잡히는가 — <b>멀거나, 시야 밖이거나.</b>
        /// 위아래도 함께 본다. 발밑을 보고 있으면 눈높이의 것은 화면에 없다.
        /// </summary>
        public static bool IsUnseen(Vector3 eye, Vector3 look, Vector3 point)
        {
            var d = point - eye;
            float dist = d.magnitude;
            if (dist >= UnseenDistance) return true;
            if (dist < 0.0001f) return false;              // 겹쳐 서 있다. 가장 잘 보이는 자리다

            float m = look.magnitude;
            if (m < 0.0001f) return false;                 // 어디를 보는지 모르면 본다고 친다
            return Vector3.Angle(look / m, d / dist) > ViewHalfAngleDegrees;
        }

        // ══ 흔적 ════════════════════════════════════════════════

        /// <summary>
        /// 다 캔 자리에 남는 <b>흔적</b>의 크기 배율.
        ///
        /// <b>흔적이 남아 있으면 돌아온다.</b> 이것이 이 값이 하는 일이다 —
        /// 통째로 사라졌다가 갑자기 다시 나타나면 세계가 가짜로 보이고,
        /// 무엇보다 플레이어가 <b>어디가 되살아나는 자리인지 배울 방법이 없다</b>.
        /// 반대로 영영 돌아오지 않는 것(합금 더미)은 전과 같이 자취 없이 사라진다.
        /// 그래서 남은 자국 하나가 "여기는 다시 찬다"는 말이 된다.
        ///
        /// 가로는 남기고 세로만 눌러 <b>납작하게</b> 만든다. 균일하게 줄이면
        /// 「작은 잔해 더미」·「작은 버섯」으로 읽혀 다 캔 자리인지 아닌지
        /// 구별되지 않는다. 납작한 것은 무엇이 됐든 <b>긁어낸 자리·밑동</b>으로 읽힌다.
        /// </summary>
        public static readonly Vector3 RemnantScale = new Vector3(0.6f, 0.15f, 0.6f);

        /// <summary>흔적에서 제 크기로 부풀어 오르는 시간(초). 눈에 걸려도 사고로 읽히지 않을 만큼 짧다.</summary>
        public const float RegrowTweenSeconds = 0.6f;

        /// <summary>재생 조건을 다시 물어보는 간격(초). 초당 네 번이면 사람 눈에는 즉시다.</summary>
        public const float PollSeconds = 0.25f;

        // ══ 장부 — 세계가 마르는가 ══════════════════════════════
        // 재생 주기를 「몇 초」로 고르면 곧바로 「분당 몇 개」가 정해지고,
        // 그 숫자가 랜턴이 태우는 양을 넘느냐가 이 라운드의 전부다.

        /// <summary>배터리 셀 하나를 뽑는 데 드는 스크랩. 추출 레시피(battery_cell)와 짝이다.</summary>
        public const int ScrapPerBatteryCell = 5;

        /// <summary>
        /// 이 티어의 랜턴이 <b>스크랩으로 환산해</b> 분당 태우는 양.
        /// 티어 1은 4.8개다 — 1.6/초 × 60 ÷ 셀당 100 × 스크랩 5개.
        /// </summary>
        public static float ScrapBurnedPerMinute(int tier)
            => LanternRule.DrainForTier(tier) * 60f / LanternRule.BatteryPerCell * ScrapPerBatteryCell;

        /// <summary>
        /// 이런 채집물 <paramref name="nodeCount"/>곳이 <b>정상 상태</b>에서 내놓는
        /// 분당 산출. 재생하지 않으면 0이다 — 처음 서 있던 몫은 소득이 아니라 재고다.
        /// </summary>
        public static float SupplyPerMinute(float yieldPerNode, int nodeCount, float respawnSeconds)
            => respawnSeconds <= 0f || nodeCount <= 0
                ? 0f
                : yieldPerNode * nodeCount * 60f / respawnSeconds;

        /// <summary>
        /// 시작부터 <paramref name="elapsedSeconds"/>초 사이에 이 채집물이
        /// <b>내놓을 수 있었던 총량</b>. 처음 서 있던 재고 한 벌에 재생한 횟수를 더한다.
        ///
        /// 한 주기가 지날 때마다 한 벌이 통째로 돌아오는 계단이다. 실제로는 노드마다
        /// 캔 시각이 달라 완만하겠지만, <b>바닥을 세는 것이 목적</b>이라 계단으로 센다 —
        /// 계단의 낮은 쪽에서도 순증가가 양수면 어떤 순서로 캐도 양수다.
        /// </summary>
        public static float CumulativeYield(float yieldPerNode, int nodeCount,
                                            float respawnSeconds, float elapsedSeconds)
        {
            float standing = yieldPerNode * Mathf.Max(0, nodeCount);
            if (respawnSeconds <= 0f || elapsedSeconds <= 0f) return standing;
            return standing * (1f + Mathf.Floor(elapsedSeconds / respawnSeconds));
        }

        /// <summary>
        /// 재생만으로 세계가 버티는가 — <b>공급이 소모보다 크다.</b>
        /// 이 하나가 거짓이면 아무리 오래 놀아도 결국 스크랩이 0으로 간다.
        /// </summary>
        public static bool Sustains(float supplyPerMinute, int lanternTier)
            => supplyPerMinute > ScrapBurnedPerMinute(lanternTier);

        /// <summary>
        /// 앉아서 기다리는 것이 답이 되는가. <b>참이면 값이 잘못됐다.</b>
        /// 배터리 한 통이 버티는 시간 안에 노드가 돌아오면, 탐사 대신
        /// "노드 앞에서 충전 한 통 태우기"가 최적해가 된다.
        /// </summary>
        public static bool WaitingBeatsWalking(float respawnSeconds, int lanternTier)
        {
            float drain = LanternRule.DrainForTier(lanternTier);
            if (drain <= 0f || respawnSeconds <= 0f) return false;
            return respawnSeconds <= LanternRule.MaxBattery / drain;
        }
    }
}
