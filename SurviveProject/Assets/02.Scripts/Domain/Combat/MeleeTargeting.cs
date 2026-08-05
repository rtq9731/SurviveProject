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
            SharesRoot(self, candidate);

        /// <summary>
        /// 두 물건이 한 몸에 속하는가. 계층의 루트가 같으면 한 몸으로 본다.
        /// 몸 하나가 콜라이더 여럿으로 되어 있어도(머리·몸통·다리) 루트는 하나다.
        /// </summary>
        public static bool SharesRoot(Transform a, Transform b) =>
            a != null && b != null && a.root == b.root;

        /// <summary>
        /// 시전 지점과 대상 사이에서 걸린 이 물건이 <b>타격을 가로막는</b> 것인가.
        ///
        /// 벽 하나를 사이에 두고 서서 방 안의 것을 때리던 것을 막는다.
        /// 가로막았다고 볼 수 없는 것이 넷 있다:
        /// <list type="bullet">
        /// <item>세워 올린 벽이 아닌 것 — 아래 <paramref name="hitIsWall"/> 설명 참고.</item>
        /// <item>대상 자신의 표면 — 레이는 결국 대상에 가서 닿는다. 그게 곧 명중이다.</item>
        /// <item>휘두른 본인의 몸 — 판정 원점이 머릿속이라 자기 콜라이더가 늘 먼저 걸린다.</item>
        /// <item>트리거 — 풀숲·채집 범위 같은 것은 통과해 지나가는 것이지 벽이 아니다.</item>
        /// </list>
        /// </summary>
        /// <param name="swinger">휘두른 쪽의 계층 어딘가(보통 카메라나 몸)</param>
        /// <param name="target">때리려는 대상의 계층 어딘가</param>
        /// <param name="hit">시선 위에서 걸린 물건</param>
        /// <param name="hitIsTrigger">걸린 물건이 트리거 콜라이더인가</param>
        /// <param name="hitIsWall">
        /// 걸린 물건이 세워 올린 건축물인가. <b>지형과 소품은 가로막는 것으로 세지 않는다.</b>
        /// 실측 결과 이 레벨은 광맥·잔해를 지형과 거대 버섯 메시 <i>안에</i> 반쯤 파묻어 두었고
        /// (부술 수 있는 22개 중 8~12개), 지형까지 벽으로 세면 그것들이 통째로 캘 수 없게 된다.
        /// 막으려던 것은 "세운 벽 너머를 때리는 일"이었지 채굴이 아니다.
        /// </param>
        public static bool IsOccluder(Transform swinger, Transform target, Transform hit,
                                      bool hitIsTrigger, bool hitIsWall)
        {
            if (hit == null) return false;
            if (!hitIsWall) return false;
            if (hitIsTrigger) return false;
            if (SharesRoot(hit, target)) return false;
            if (SharesRoot(hit, swinger)) return false;
            return true;
        }

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
