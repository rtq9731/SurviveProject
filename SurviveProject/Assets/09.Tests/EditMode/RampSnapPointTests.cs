using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Building;

/// <summary>
/// §1.3 회귀 테스트 — 경사로의 유일한 <c>BuildSnapPoint</c>가
/// <c>accepts: 0</c>(BuildPieceKind.None)로 비어 있어 경사로 착지점에서
/// 아무것도 이어 지을 수 없던 결함.
///
/// 조사 결론: 잊힌 자리값이다. 다른 조각의 모든 스냅 포인트는 실제 플래그를
/// 갖고("토대"는 3·12·16, "벽"은 2·12), 경사로 것만 0이었다. 이름도
/// "Snap_Top"이라 붙일 자리로 의도됐음을 시사한다. §3의 미완료 항목에는
/// "경사로로 2층까지 올라가기"가 명시돼 있어 경사로가 막다른 길이 아니라
/// 이어 지어야 하는 구조임을 로드맵이 전제하고 있다. 그래서 막다른 길로
/// 고정하는 대신 착지점이 토대·바닥을 받도록(Platform = Foundation|Floor = 3)
/// 고쳤다 — 토대의 자체 스냅 포인트들과 같은 조합이고, 착지점에 곧장
/// 바닥을 얹을 수 있게 해서 "반드시 토대·바닥을 거쳐야 한다"는 문제를 없앤다.
///
/// <c>BuildSnapPoint</c>는 asmdef 없는 폴더(Assembly-CSharp) 소속이라
/// 이 테스트 어셈블리에서 직접 참조할 수 없다. 리플렉션으로 찾는다.
/// <see cref="BuildPieceKind"/>는 Domain 어셈블리 소속이라 바로 쓴다.
/// </summary>
public class RampSnapPointTests
{
    const string RampPrefabPath = "Assets/05.Prefabs/Buildables/Piece_Ramp.prefab";

    static Type FindType(string fullName)
    {
        var t = Type.GetType(fullName + ", Assembly-CSharp");
        if (t != null) return t;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    [Test]
    public void 경사로_착지점은_토대와_바닥을_받는다()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RampPrefabPath);
        Assert.IsNotNull(prefab, $"{RampPrefabPath}를 찾지 못했다");

        var snapPointType = FindType("Survive.Building.BuildSnapPoint");
        Assert.IsNotNull(snapPointType, "BuildSnapPoint 타입을 찾지 못했다");

        var snapComponent = prefab.GetComponentInChildren(snapPointType, true);
        Assert.IsNotNull(snapComponent, "경사로 프리팹에 BuildSnapPoint가 없다");

        var acceptsField = snapPointType.GetField("accepts", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(acceptsField, "accepts 필드를 찾지 못했다 — 이름이 바뀌었을 수 있다");

        var accepts = (BuildPieceKind)acceptsField.GetValue(snapComponent);

        Assert.AreNotEqual(BuildPieceKind.None, accepts,
            "경사로 착지점이 다시 아무것도 안 받는 채로(None) 되돌아갔다");
        Assert.AreEqual(BuildPieceKind.Platform, accepts,
            "경사로 착지점은 정확히 Foundation|Floor(Platform)만 받아야 한다");

        Assert.IsTrue((accepts & BuildPieceKind.Foundation) != 0, "경사로 착지점이 토대를 못 받는다");
        Assert.IsTrue((accepts & BuildPieceKind.Floor) != 0, "경사로 착지점이 바닥을 못 받는다");
        Assert.IsFalse((accepts & BuildPieceKind.Wall) != 0, "경사로 착지점이 의도치 않게 벽까지 받는다");
        Assert.IsFalse((accepts & BuildPieceKind.Ramp) != 0, "경사로 착지점이 의도치 않게 경사로까지 받는다");
    }
}
