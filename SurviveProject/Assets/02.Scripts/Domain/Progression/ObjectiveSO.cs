using UnityEngine;
using Survive.Items;

namespace Survive.Progression
{
    /// <summary>
    /// 목표가 진행도를 계산할 때 필요한 게임 상태.
    /// 인터페이스로 두어 도메인이 MonoBehaviour를 모르게 한다.
    /// </summary>
    public interface IObjectiveContext
    {
        Inventory PlayerInventory { get; }
        int GetFlag(string key);
    }

    /// <summary>
    /// 목표의 기반. 구현체는 각자 자기 이름의 파일에 둔다 —
    /// ScriptableObject 클래스를 한 파일에 여러 개 넣으면 Unity가
    /// 에셋의 m_Script 참조를 연결하지 못한다.
    /// </summary>
    public abstract class ObjectiveSO : ScriptableObject
    {
        public string id;
        [TextArea] public string displayText;

        /// <summary>0~1 진행도.</summary>
        public abstract float Evaluate(IObjectiveContext ctx);

        public bool IsComplete(IObjectiveContext ctx) => Evaluate(ctx) >= 1f;
    }
}
