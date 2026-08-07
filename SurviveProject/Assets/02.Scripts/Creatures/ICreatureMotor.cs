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

        /// <summary>
        /// <b>이 몸이 발밑에서 얼마나 떠서 다니는가(m).</b> 걷는 몸은 0이다.
        ///
        /// <b>왜 이동 수단이 이것을 말해야 하는가.</b> 땅에 놓인 것에 닿는 판정
        /// (<see cref="CreatureDecision.IsWithinReach"/>)은 높이차를 얼마까지
        /// 눈감아 줄지를 알아야 한다. 눈감아 주지 않으면 나는 개체는 순항 고도
        /// 때문에 <b>어느 자리에서도</b> 먹지 못하고, 무한정 눈감아 주면 거대 버섯
        /// 갓 위를 지나는 개체가 <b>9m 아래의 풀을 뜯는다</b>(둘 다 실측).
        /// 그 경계를 아는 것은 판단하는 쪽이 아니라 <b>떠 있는 쪽</b>이다.
        /// </summary>
        float CruiseHeight { get; }

        /// <summary>그쪽으로 간다. 멈춰 있었다면 멈춤을 푼다.</summary>
        void MoveTowards(Vector3 target);

        /// <summary>선다.</summary>
        void Stop();
    }
}
