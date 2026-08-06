using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// 몸을 실제로 옮기는 것. <see cref="CreatureBrain"/>이 아는 이동의 전부다.
    ///
    /// <b>왜 인터페이스로 뽑았는가.</b> 예전에는 두뇌가 "NavMeshAgent가 있으면 그것,
    /// 없으면 FlyerMotor"라고 두 갈래로 적혀 있었다. 세 번째 이동 방식
    /// (<see cref="HoverDrifter"/>)이 붙으면서 그 분기가 세 갈래가 되는데, 이동 수단을
    /// 늘릴 때마다 판단하는 쪽을 고쳐야 한다면 그건 두뇌가 몸을 너무 많이 아는 것이다.
    ///
    /// NavMeshAgent는 Unity의 것이라 이 인터페이스를 붙일 수 없다. 그래서 두뇌는
    /// <b>에이전트가 있으면 에이전트, 없으면 이 인터페이스</b>까지만 안다.
    /// </summary>
    public interface ICreatureMotor
    {
        /// <summary>정의에서 받은 최고 속도(m/s).</summary>
        float Speed { get; set; }

        /// <summary>그쪽으로 간다. 멈춰 있었다면 멈춤을 푼다.</summary>
        void MoveTowards(Vector3 target);

        /// <summary>선다.</summary>
        void Stop();
    }
}
