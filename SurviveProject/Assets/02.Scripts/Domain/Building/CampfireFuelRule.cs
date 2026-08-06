using UnityEngine;
using Survive.Harvesting;

namespace Survive.Building
{
    /// <summary>
    /// 화톳불 연료의 규칙. <b>목재 수치를 돌리는 자리는 여기 하나다.</b>
    ///
    /// <b>스크랩은 연료가 아니다.</b> 스크랩은 에너지를 <b>담고 있는</b> 매체이고,
    /// 불은 그 안에 갇힌 것을 꺼내는 손이다(배터리 추출). 태우는 것과 뽑아내는 것을
    /// 같은 물질에 겹쳐 두면 "왜 스크랩을 넣으면 어떤 때는 불이 커지고 어떤 때는
    /// 배터리가 나오는가"를 설명할 수 없다. 불에 들어가는 것은 목재뿐이다.
    ///
    /// <b>목재의 주 소비처가 여기다</b>(기획서 §5.3). 다리 관문이 폐기되며
    /// "다리 하나 = 버섯나무 아홉 그루"라는 밸런스 축이 빠졌고, 그 자리를
    /// 화톳불 연료가 대신한다. 스크랩은 <b>나가는 값</b>(랜턴·배터리·조명탄),
    /// 목재는 <b>머무는 값</b>(화톳불·전초기지)이다. 불이 꺼지면 밝은 구역이
    /// 사라지므로(<see cref="Survive.World.LitZoneRegistry"/>) 목재 재고가
    /// 곧 안전 재고이고, 벌목량이 곧 세울 수 있는 전진 기지 수가 된다.
    ///
    /// <b>수치는 아직 확정이 아니다</b>(상세기획서 §13 3번 — 랜턴 반경·초당 소모·
    /// 깊이별 산출 세 값이 정해져야 역산된다). 그래서 아래 <b>손잡이 넷</b>만
    /// 돌리면 전부 따라오도록 짜 두었다. 나머지는 전부 파생값이고,
    /// <see cref="Campfire"/>는 직렬화 필드를 갖지 않고 여기를 그대로 읽는다 —
    /// 프리팹에 사본이 있으면 상수를 돌려도 게임이 안 바뀐다.
    /// </summary>
    public static class CampfireFuelRule
    {
        // ══ 튜닝 손잡이 넷 ══════════════════════════════════════
        // 목재 밸런스를 돌릴 때 손대는 곳은 이 넷뿐이다.

        /// <summary>
        /// 목재 하나가 주는 연료(초). <b>축의 단위</b>다.
        ///
        /// 스크랩 시절부터 45초였고 그대로 둔다 — 물질과 자릿수를 한 번에
        /// 바꾸면 불이 자주 꺼지는 것이 무엇 탓인지 가릴 수 없다.
        /// </summary>
        public const float SecondsPerLog = 45f;

        /// <summary>
        /// 불 하나가 품을 수 있는 목재 수. <b>"목재 재고 = 안전 재고"가 성립하는 이유.</b>
        ///
        /// 이것이 작으면 재고가 안전이 되지 못한다 — 아무리 많이 베어 와도
        /// 불에 들어가는 것은 네 개뿐이고 나머지는 배낭에서 잠든다. 그러면
        /// 벌목은 "불 앞에 얼마나 오래 묶여 있을 것인가"를 물을 뿐,
        /// "얼마나 멀리 나갈 것인가"를 묻지 못한다(기획서 §5.3).
        /// 가득 채운 불 하나가 거대 버섯 두 그루쯤이라야 전진 기지를 세는 단위가 된다.
        /// </summary>
        public const int CapacityLogs = 10;

        /// <summary>
        /// 한 번 넣을 때의 상한. 빈 불을 두 번에 채우는 크기다.
        ///
        /// 상한을 아예 없애지 않는 이유는 <b>넣는 것이 판단이어야</b> 하기 때문이다 —
        /// 한 번에 배낭을 통째로 붓게 하면 전진 기지에 나눠 둘 몫이 사라진다.
        /// </summary>
        public const int LogsPerRefuel = 5;

        /// <summary>
        /// 세우자마자 타고 있는 몫. 한 번 넣은 것이 아니라 <b>불씨</b>다.
        ///
        /// 비율(예: 용량의 절반)로 두면 안 된다 — <see cref="CapacityLogs"/>를
        /// 올릴 때마다 공짜로 주는 목재가 함께 늘어 축이 조용히 무너진다.
        /// </summary>
        public const int StarterLogs = 2;

        // ══ 파생값 ══════════════════════════════════════════════

        /// <summary>불에 넣을 수 있는 유일한 것.</summary>
        public const string FuelItemId = MushroomLumberRule.WoodItemId;

        /// <summary>가득 찼을 때의 연료(초).</summary>
        public const float MaxFuelSeconds = SecondsPerLog * CapacityLogs;

        /// <summary>세우자마자 남아 있는 연료(초).</summary>
        public const float StarterFuelSeconds = SecondsPerLog * StarterLogs;

        // ══ 계산 ════════════════════════════════════════════════

        /// <summary>목재 N개가 태우는 시간(초). 용량을 넘겨 세지 않는다.</summary>
        public static float SecondsFor(int logs, float secondsPerLog, float maxFuel)
        {
            if (logs <= 0) return 0f;
            return Mathf.Min(maxFuel, logs * secondsPerLog);
        }

        /// <summary>
        /// 지금 불에 더 들어갈 수 있는 목재 수.
        ///
        /// <b>내림이다.</b> 넘칠 몫은 세지 않는다 — 올림으로 세면
        /// <see cref="AfterRefuel"/>의 상한에 잘려 배낭의 목재가 그냥 사라진다.
        /// 예전에는 연료 170초짜리 불에 목재 2개(90초)를 넣으면 10초만 오르고
        /// 80초어치가 증발했다. 태우지도 못한 나무를 잃는 것은 규칙이 아니라 버그다.
        /// </summary>
        public static int RoomInLogs(float fuel, float secondsPerLog, float maxFuel)
        {
            if (secondsPerLog <= 0f) return 0;
            float left = maxFuel - fuel;
            if (left <= 0f) return 0;
            return Mathf.Max(0, Mathf.FloorToInt(left / secondsPerLog));
        }

        /// <summary>
        /// 이번에 실제로 넣을 목재 수. 가진 만큼만, 들어갈 만큼만 넣는다 —
        /// 하나밖에 없다고 거절하면 어두운 데서 손에 든 것을 못 쓰고,
        /// 자리보다 많이 받으면 나무가 사라진다.
        /// </summary>
        public static int LogsToTake(int held, int logsPerRefuel, float fuel,
                                     float secondsPerLog, float maxFuel)
        {
            if (held <= 0 || logsPerRefuel <= 0) return 0;
            int room = RoomInLogs(fuel, secondsPerLog, maxFuel);
            return Mathf.Min(Mathf.Min(logsPerRefuel, held), room);
        }

        /// <summary>목재를 넣은 뒤의 연료. 최대치를 넘지 않는다.</summary>
        public static float AfterRefuel(float fuel, int logs, float secondsPerLog, float maxFuel)
        {
            if (logs <= 0) return fuel;
            return Mathf.Min(maxFuel, fuel + secondsPerLog * logs);
        }

        /// <summary>지난 시간만큼 탄 뒤의 연료. 0 아래로는 내려가지 않는다.</summary>
        public static float AfterBurn(float fuel, float deltaSeconds)
            => Mathf.Max(0f, fuel - Mathf.Max(0f, deltaSeconds));

        /// <summary>연료가 남아 있으면 타고 있는 것이다.</summary>
        public static bool IsBurning(float fuel) => fuel > 0f;
    }
}
