using UnityEngine;
using Survive.Core;
using Survive.Items;

namespace Survive.World
{
    /// <summary>
    /// 조명탄 총이 <b>쏘는 쪽</b>. 규칙은 전부 <see cref="FlareRule"/>에 있고
    /// 여기서 하는 일은 물리에 묻는 것뿐이다 — 어디에 박히는지 고르는 계산은
    /// <see cref="FlareRule.ImpactPoint"/>가 한다.
    ///
    /// <b>왜 컴포넌트가 아닌가.</b> 붙일 자리가 플레이어 프리팹인데 프리팹은
    /// 이 라운드가 손댈 수 없고, 손댈 수 있다 해도 직렬화 필드가 하나 생기는
    /// 순간 규칙의 사본이 거기 앉는다. 부르는 쪽은 이미 도구를 들고 있는
    /// <c>Survive.Combat.MeleeSwing</c> 하나뿐이라 상태를 둘 자리도 없다.
    ///
    /// <b>「총」이므로 날아가서 박힌다.</b> 발밑에 놓는 물건이면
    /// 「낫이 있는 자리를 비워 놓고 들어간다」(기획서 §5.2)가 성립하지 않는다 —
    /// 먼저 비우려면 내가 아직 없는 자리를 밝힐 수 있어야 한다.
    /// </summary>
    public static class FlareGun
    {
        /// <summary>이 도구가 조명탄 총인가.</summary>
        public static bool Holding(ItemDataSO tool) => tool != null && tool.id == FlareRule.ItemId;

        /// <summary>
        /// 쏜다. 배터리가 모자라면 아무 일도 일어나지 않는다.
        ///
        /// <b>배터리는 랜턴과 같은 통에서 먹는다.</b> 그래서 매 순간의 물음이
        /// "빛을 지키는 데 쓸 것인가 쫓아내는 데 쓸 것인가"가 된다 —
        /// 자원을 따로 두면 그 물음이 사라진다.
        /// </summary>
        /// <param name="muzzle">총구. 대개 카메라다.</param>
        /// <param name="burn">터진 조명탄. 못 쏘면 null.</param>
        public static bool TryFire(Transform muzzle, out FlareBurn burn)
        {
            burn = null;
            if (muzzle == null) return false;

            var lantern = Lantern();
            // 랜턴이 없으면 배터리도 없다. 조명탄만 들고 나가서 쏘는 길을 열면
            // 「같은 통에서 먹는다」가 거짓이 된다.
            if (lantern == null) return false;
            if (!FlareRule.CanFire(lantern.Battery)) return false;

            lantern.Drain(FlareRule.BatteryCost);
            burn = FlareBurn.Ignite(ImpactPoint(muzzle.position, muzzle.forward));
            return true;
        }

        /// <summary>
        /// 총구에서 쏜 조명탄이 박히는 자리. <b>재는 일만 여기서 하고</b>
        /// 고르는 것은 <see cref="FlareRule.ImpactPoint"/>가 한다.
        /// </summary>
        public static Vector3 ImpactPoint(Vector3 muzzle, Vector3 aim)
        {
            bool hitAhead = Physics.Raycast(muzzle, aim, out RaycastHit ahead,
                                            FlareRule.MaxThrowDistance,
                                            Physics.DefaultRaycastLayers,
                                            QueryTriggerInteraction.Ignore);

            // 못 맞혔으면 날아간 끝에서 발밑을 찾는다. 허공에 뜬 채로 타면
            // 「그 자리를 밝힌다」가 지면과 어긋나고, 밀려나는 개체가 원의
            // 아래쪽을 그냥 지나간다.
            bool foundGround = false;
            Vector3 groundPoint = Vector3.zero;

            if (!hitAhead)
            {
                Vector3 far = FlareRule.FarEnd(muzzle, aim);
                foundGround = Physics.Raycast(far + Vector3.up * FlareRule.MaxThrowDistance,
                                              Vector3.down, out RaycastHit down,
                                              FlareRule.MaxThrowDistance * 2f,
                                              Physics.DefaultRaycastLayers,
                                              QueryTriggerInteraction.Ignore);
                if (foundGround) groundPoint = down.point;
            }

            return FlareRule.ImpactPoint(muzzle, aim,
                                         hitAhead, ahead.point, ahead.normal,
                                         foundGround, groundPoint);
        }

        static LanternController Lantern() =>
            GameServices.TryGet<LanternController>(out var lamp) ? lamp : null;
    }
}
