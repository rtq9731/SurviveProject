using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 발광 버섯 군락의 빛과 재생 규칙.
    ///
    /// 군락은 지금까지 배터리를 채워 주는 고마운 배경이었다. 여기서부터는
    /// <b>플레이어가 소모할 수 있는 것</b>이 된다 — 갓을 따면 그만큼 어두워지고,
    /// 다 따면 꺼진다. 꺼진 군락은 <see cref="LitZoneRegistry"/>에서 빠지므로
    /// 빛을 꺼리는 소비자(낫)가 그 자리로 걸어 들어온다. 자원과 안전이
    /// 같은 저울에 올라가는 지점이 여기다.
    ///
    /// 규칙만 여기 있다. 무엇이 몇 개 남았는지 세는 일과 라이트를 만지는 일은
    /// Survive.World.GlowGrove가 하고, 이 클래스는 Unity 없이 테스트된다 —
    /// <see cref="LitZoneRegistry"/>·<see cref="RespawnRule"/>과 같은 자리다.
    /// </summary>
    public static class GlowGroveRule
    {
        /// <summary>
        /// 딴 갓 하나가 다시 자라기까지 걸리는 시간(초).
        ///
        /// <b>왜 180인가.</b> 세 가지 실측을 견줘 정했다.
        /// <list type="number">
        /// <item>랜턴은 완충 100에서 초당 1씩 닳는다 — <b>한 번 충전에 100초</b>다.
        ///   재생이 그보다 짧으면 "꺼진 군락 옆에서 랜턴 켜고 기다리기"가 정답이 된다.
        ///   180초는 배터리 한 통보다 길어서 기다림이 답이 되지 못한다.</item>
        /// <item>군락 사이는 가깝다 — 1↔2가 15.6m, 2↔3이 7.9m, 걸어서 3초 남짓이다.
        ///   그래서 대가는 "시간을 버티는 것"이 아니라 <b>자리를 옮기는 것</b>이어야 하고,
        ///   셋을 다 비우면 옮길 자리가 없어 진짜로 어둠에 남는다.</item>
        /// <item>기존 재생값과 자릿수를 맞춘다 — 발광 버섯 채집 노드 90초,
        ///   불씨버섯 성장 55초, 챕터 1 스펙 표의 발광 버섯 120초.
        ///   빛(세계의 상태)이 걸린 만큼 자원 재생보다는 무겁게 잡되,
        ///   영구가 아니라 <b>한 번의 탐사 왕복 안에 돌아오는</b> 길이로 둔다.</item>
        /// </list>
        /// </summary>
        public const float RegrowSeconds = 180f;

        /// <summary>
        /// 남은 갓 <paramref name="remaining"/> / 전체 <paramref name="total"/> →
        /// 빛의 세기 배율(0~1).
        ///
        /// 선형이다. 광도의 물리를 흉내내면(제곱근 등) 한 개 땄을 때 눈에 거의 티가
        /// 안 나고, 그러면 "딸수록 어두워진다"를 플레이어가 배우지 못한다.
        /// 세기와 반경에 같은 배율을 곱해 <b>보이는 밝기와 안전 반경이 어긋나지 않게</b> 한다 —
        /// 빛이 남아 보이는데 포식자가 들어오면 규칙을 읽을 수 없다.
        /// </summary>
        public static float Brightness(int remaining, int total)
        {
            if (total <= 0) return 0f;
            return Mathf.Clamp01(remaining / (float)total);
        }

        /// <summary>
        /// 아직 켜져 있는가. 하나라도 남아 있으면 켜져 있다.
        ///
        /// 밝기가 얼마나 낮든 0이 아닌 한 켜진 것으로 친다 — 반쯤 딴 군락이
        /// 조용히 포식자에게 열려 있으면 "다 따야 열린다"는 계약이 깨진다.
        /// </summary>
        public static bool IsLit(int remaining) => remaining > 0;

        /// <summary>
        /// 딴 지 <paramref name="regrowSeconds"/>가 지났는가.
        /// 정확히 그 시각이면 자란 것으로 본다 — 경계는 안쪽으로 친다
        /// (CreatureDecision의 감지·사거리 판정과 같은 관례).
        /// </summary>
        public static bool HasRegrown(float harvestedAt, float now, float regrowSeconds)
            => now - harvestedAt >= regrowSeconds;

        /// <summary>돌아오기까지 남은 초. 이미 자랐으면 0이다. 프롬프트에 그대로 쓴다.</summary>
        public static float RegrowRemaining(float harvestedAt, float now, float regrowSeconds)
            => Mathf.Max(0f, regrowSeconds - (now - harvestedAt));
    }
}
