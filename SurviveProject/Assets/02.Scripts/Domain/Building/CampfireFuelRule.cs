using UnityEngine;
using Survive.Harvesting;

namespace Survive.Building
{
    /// <summary>
    /// 화톳불 연료의 규칙.
    ///
    /// <b>스크랩은 연료가 아니다.</b> 스크랩은 에너지를 <b>담고 있는</b> 매체이고,
    /// 불은 그 안에 갇힌 것을 꺼내는 손이다(배터리 추출). 태우는 것과 뽑아내는 것을
    /// 같은 물질에 겹쳐 두면 "왜 스크랩을 넣으면 어떤 때는 불이 커지고 어떤 때는
    /// 배터리가 나오는가"를 설명할 수 없다. 불에 들어가는 것은 목재뿐이다.
    ///
    /// <b>왜 자릿수를 그대로 두는가.</b> 물질만 바뀌었을 뿐 불을 지키는 리듬은
    /// 그대로여야 한다 — 스크랩 2개당 90초였으니 목재 2개당 90초다. 여기서
    /// 효율까지 함께 손대면, 불이 자주 꺼지는 것이 물질 탓인지 수치 탓인지
    /// 아무도 가릴 수 없게 된다.
    /// </summary>
    public static class CampfireFuelRule
    {
        /// <summary>불에 넣을 수 있는 유일한 것.</summary>
        public const string FuelItemId = MushroomLumberRule.WoodItemId;

        /// <summary>목재 하나가 주는 연료(초). 스크랩 시절과 같다.</summary>
        public const float SecondsPerLog = 45f;

        /// <summary>한 번에 넣는 목재 수. 스크랩 시절과 같다 — 한 번 넣으면 90초.</summary>
        public const int LogsPerRefuel = 2;

        /// <summary>가득 찼을 때의 연료(초).</summary>
        public const float MaxFuelSeconds = 180f;

        /// <summary>
        /// 이번에 실제로 넣을 목재 수. 가진 만큼만 넣는다 —
        /// 하나밖에 없다고 거절하면 어두운 데서 손에 든 것을 못 쓴다.
        /// </summary>
        public static int LogsToTake(int held, int logsPerRefuel)
        {
            if (held <= 0 || logsPerRefuel <= 0) return 0;
            return Mathf.Min(logsPerRefuel, held);
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
