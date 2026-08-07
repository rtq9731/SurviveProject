using UnityEngine;

namespace Survive.Progression
{
    /// <summary>
    /// <b>코어를 훔치기 전에 한 번 울리는 경고</b> (기획서 §4.5, 챕터 1 스펙 §8-4).
    ///
    /// <b>왜 경고가 있어야 하는가.</b> 코어를 집으면 발령이 오르고 낫이 다섯으로 는다
    /// (<c>NestSite</c>). 그것을 모르고 집으면 습격은 불운이고, 알고 집으면
    /// <b>감수한 결정</b>이다. 기획서가 "경고가 있어야 불운이 아니라 감수한 결정이
    /// 된다"고 적은 자리가 바로 여기다 — 난이도를 사람이 고르게 하는 장치이므로,
    /// 고를 재료를 먼저 주지 않으면 그 선택지 자체가 성립하지 않는다.
    ///
    /// <b>왜 한 번뿐인가.</b> 되풀이해 말할 채널이 아직 없다 —
    /// <c>UnlockService.Announce</c>는 원장이 잠그는 한 번짜리다. 그런데 사전 경고는
    /// 원래 한 번짜리다. 두 번째부터는 사람이 이미 아는 것이고, 알면서 다시 가는
    /// 사람에게 같은 말을 되풀이하는 것은 경고가 아니라 잔소리다. 채널의 성질과
    /// 이 경고의 성질이 맞아떨어지는 자리라, 없는 채널을 지어낼 이유가 없다.
    ///
    /// <b>판단은 전부 여기 있다.</b> 재는 일만 <c>CoreTheftWarner</c>가 한다.
    /// 순수 정적이라 Unity 실행 없이 시험한다.
    /// </summary>
    public static class CoreTheftWarning
    {
        /// <summary>Resources 아래에서 경고 대사를 찾는 이름.</summary>
        public const string ResourceName = "Warn_CoreTheft";

        /// <summary>
        /// 원장에 남는 열쇠. <b>1회성의 주인이 이것이다.</b>
        ///
        /// 컴포넌트의 bool로 세면 저장본을 불러온 뒤에 같은 자리에서 또 말한다 —
        /// <c>LocationDiscoveryTrigger</c>가 이미 같은 이유로 원장을 쓴다.
        /// 접두사를 발견 열쇠(<c>discovery:</c>)와 다르게 두는 것은, 이것이 무언가를
        /// 여는 기록이 아니라 <b>말한 적 있다</b>는 기록이기 때문이다.
        /// </summary>
        public const string Key = "warn:core_theft";

        /// <summary>
        /// 경고가 울리는 거리(m). 둥지 반경(<c>NestRule.HomeRadius</c> 3m)의 다섯 배다.
        ///
        /// <b>왜 넉넉한가.</b> 코어에 손이 닿는 자리에서 울리는 것은 경고가 아니라
        /// 통보다. 되돌아설 여유가 있어야 결정이 되고, 낫의 지각 반경(정의 기본값 8m)
        /// 바깥에서 울려야 "듣고 나서 고를 수 있었다"가 참이 된다.
        /// </summary>
        public const float WarnRadius = 15f;

        /// <summary>
        /// 지금이 경고할 때인가.
        ///
        /// <b>코어가 아직 둥지에 있을 때만이다.</b> 이미 나와 있으면 훔칠 것이 없고,
        /// 손에 들고 되돌려 놓으러 가는 길에 "가져가면 반응한다"고 말하는 것은
        /// 틀린 말이다. 사람이 어느 쪽에서 다가왔는지는 보지 않는다 — 거리 하나다.
        /// </summary>
        public static bool IsMoment(bool coreAtHome, float playerToNest) =>
            coreAtHome && playerToNest >= 0f && playerToNest <= WarnRadius;

        /// <summary>
        /// 사람과 둥지의 거리. <b>수평으로만 잰다</b> —
        /// 둥지 판정(<c>NestRule.AtHome</c>)과 같은 자를 쓴다. 이 세계는 갓과 바위가
        /// 머리 위를 덮고 있어 높이 차가 쉽게 몇 미터씩 나는데, 그것을 거리에 더하면
        /// 갓 위로 올라선 것만으로 경고가 늦어진다. 높이는 다가감의 척도가 아니다.
        /// </summary>
        public static float PlaneDistance(Vector3 player, Vector3 nest)
        {
            float dx = player.x - nest.x;
            float dz = player.z - nest.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// 경고를 한 번 쓴다. <b>처음이면 true</b>, 이미 울렸으면 false.
        ///
        /// 원장이 없으면 울리지 않는다. 셀 수 없는 1회성은 1회성이 아니고,
        /// 조용히 매번 울리는 것이 조용히 안 울리는 것보다 나쁘다.
        /// </summary>
        public static bool TryClaim(UnlockLedger ledger) => ledger != null && ledger.Unlock(Key);
    }
}
