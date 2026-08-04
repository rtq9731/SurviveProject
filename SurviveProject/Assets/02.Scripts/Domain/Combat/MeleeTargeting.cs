using UnityEngine;

namespace Survive.Combat
{
    /// <summary>
    /// 휘두른 도구가 무엇에 닿는가를 정하는 규칙. 전부 순수 함수다 —
    /// 씬도 물리 질의도 시간도 건드리지 않고, 넣은 값만 보고 답한다.
    ///
    /// <see cref="MeleeSwing"/>은 물리로 후보를 모아 오기만 하고,
    /// 그 후보를 때릴지 말지는 여기에 묻는다.
    /// 원뿔 경계 하나와 "자기 몸인가" 하나가 곧 자해 여부를 가르기 때문에,
    /// 그 판단이 씬을 켜지 않고도 값으로 확인 가능한 자리에 있어야 한다.
    /// </summary>
    public static class MeleeTargeting
    {
        /// <summary>
        /// 전방 판정 각도(도)를 내적 비교용 코사인 한계로 바꾼다.
        /// 90이면 좌우 45도씩이므로 한계는 cos 45도다.
        /// </summary>
        public static float ConeCosLimit(float coneAngleDegrees) =>
            Mathf.Cos(coneAngleDegrees * 0.5f * Mathf.Deg2Rad);

        /// <summary>
        /// 원뿔 <b>안쪽 또는 경계 위</b>면 닿는 것으로 본다.
        ///
        /// <paramref name="originToTarget"/>이 영벡터면 — 대상 중심이 판정 원점과 겹치면 —
        /// 방향을 정할 수 없으므로 닿지 않은 것으로 본다.
        /// </summary>
        public static bool IsWithinCone(Vector3 forward, Vector3 originToTarget, float cosLimit)
        {
            Vector3 dir = originToTarget.normalized;
            return Vector3.Dot(forward, dir) >= cosLimit;
        }

        /// <summary>
        /// 이 물건이 휘두른 본인의 몸에 속하는가. 계층의 루트가 같으면 같은 몸으로 본다.
        ///
        /// 판정 구의 중심이 카메라 — 곧 플레이어의 머릿속 — 이라
        /// 자기 CharacterController는 어떤 스윙에서도 항상 후보로 잡힌다.
        /// 시선을 충분히 내리면 그 몸이 전방 원뿔 안으로 들어와,
        /// 자기 도구로 자기를 때리는 일이 실제로 벌어진다.
        /// </summary>
        public static bool BelongsToSelf(Transform self, Transform candidate) =>
            self != null && candidate != null && candidate.root == self.root;

        /// <summary>
        /// 해석된 대상이 본인의 몸인가. 씬에 붙어 있지 않은 구현체(Component가 아닌 것)는
        /// 계층이 없으므로 남으로 본다.
        /// </summary>
        public static bool IsSelfTarget(Transform self, IDamageable target) =>
            target is Component c && BelongsToSelf(self, c.transform);

        /// <summary>
        /// 이 피해를 자기가 냈는가. 가해자를 모르는 피해(환경·낙하 등)는
        /// 남이 낸 것으로 보아 그대로 들어오게 둔다.
        /// </summary>
        public static bool IsSelfInflicted(Transform self, GameObject source) =>
            source != null && BelongsToSelf(self, source.transform);
    }
}
