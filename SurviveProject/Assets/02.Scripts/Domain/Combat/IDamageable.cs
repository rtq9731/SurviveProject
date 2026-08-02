namespace Survive.Combat
{
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(in DamageInfo info);
    }
}
