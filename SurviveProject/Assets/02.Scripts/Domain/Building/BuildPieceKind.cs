using System;

namespace Survive.Building
{
    /// <summary>
    /// 모듈 조각의 종류.
    ///
    /// 붙일 수 있는 자리를 "이 자리는 무엇을 받는가"로 적기 위해 비트로 둔다.
    /// 바닥 모서리는 벽도 받고 경사로도 받는데, 그걸 종류별 목록으로 적으면
    /// 조각을 하나 더할 때마다 모든 자리를 고쳐야 한다.
    /// </summary>
    [Flags]
    public enum BuildPieceKind
    {
        None = 0,

        /// <summary>토대. 지면에 직접 놓는 유일한 조각이고, 나머지는 여기서 자란다.</summary>
        Foundation = 1 << 0,

        /// <summary>바닥. 같은 층으로 넓히거나 벽 위에 얹어 2층이 된다.</summary>
        Floor = 1 << 1,

        /// <summary>벽.</summary>
        Wall = 1 << 2,

        /// <summary>문간. 벽과 같은 자리에 서지만 지나갈 수 있다.</summary>
        Doorway = 1 << 3,

        /// <summary>경사로. 층과 지면을 잇는다.</summary>
        Ramp = 1 << 4,

        /// <summary>바닥처럼 놓이는 것 전부.</summary>
        Platform = Foundation | Floor,

        /// <summary>벽처럼 서는 것 전부.</summary>
        Upright = Wall | Doorway,
    }
}
