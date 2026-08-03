using UnityEngine;

namespace Survive.Building
{
    /// <summary>
    /// 세워진 모듈 조각. 무엇으로 세워졌는지 표시한다.
    ///
    /// <see cref="BuiltStructure"/>와 나눈 이유: 그쪽은 "플레이어가 세운 것"이고
    /// 이쪽은 "격자에 물린 조각"이다. 같은 자리에 같은 종류가 두 번 들어가는 것을
    /// 막으려면 종류를 알아야 하는데, 화톳불이나 보관함에는 그런 개념이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ModularPiece : MonoBehaviour
    {
        [SerializeField] BuildPieceKind kind = BuildPieceKind.None;

        public BuildPieceKind Kind => kind;

        public void Setup(BuildPieceKind k) => kind = k;
    }
}
