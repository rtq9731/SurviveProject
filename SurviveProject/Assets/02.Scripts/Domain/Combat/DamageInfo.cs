using UnityEngine;

namespace Survive.Combat
{
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Source;
        public readonly Vector3 HitPoint;
        public readonly Vector3 HitNormal;

        public DamageInfo(float amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal)
        {
            Amount = amount;
            Source = source;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
        }
    }
}
