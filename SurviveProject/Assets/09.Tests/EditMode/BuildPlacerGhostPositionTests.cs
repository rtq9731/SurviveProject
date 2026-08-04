using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Survive.Core;
using Survive.Building;

/// <summary>
/// §1.1 회귀 테스트 — 모듈 조각을 놓을 수 없을 때 고스트가 원점(0,0,0)으로
/// 순간이동하던 결함.
///
/// <c>BuildPlacer</c>는 <c>Assets/02.Scripts/Building/</c>에 있고 이 폴더는
/// 어떤 asmdef에도 속하지 않아 기본 Assembly-CSharp으로 컴파일된다.
/// 이 테스트 어셈블리(Survive.Tests.EditMode)는 asmdef 기반이라
/// Assembly-CSharp을 참조할 수 없다(유니티가 원천적으로 막는다 — 컴파일
/// 순서가 반대라서 순환 참조가 된다). 그래서 <c>BuildPlacer</c> 타입은
/// 컴파일 타임에 직접 쓸 수 없고, 리플렉션으로 찾아 다룬다.
///
/// <see cref="PlacementResult"/>와 <see cref="BuildableSO"/>는
/// <c>Domain/Building</c> 아래에 있어 Survive.Domain 어셈블리 소속이라
/// 평범하게 참조할 수 있다.
/// </summary>
public class BuildPlacerGhostPositionTests
{
    GameObject _placerGO;
    GameObject _rayGO;
    BuildableSO _wallLike;

    [SetUp]
    public void 초기화()
    {
        GameServices.Clear();

        _wallLike = ScriptableObject.CreateInstance<BuildableSO>();
        _wallLike.pieceKind = BuildPieceKind.Wall;   // 모듈 조각이어야 EvaluateModular를 탄다
        _wallLike.requiresSnap = true;                // 토대가 아니므로 붙일 자리가 없으면 바로 NoAnchor
    }

    [TearDown]
    public void 정리()
    {
        if (_placerGO != null) UnityEngine.Object.DestroyImmediate(_placerGO);
        if (_rayGO != null) UnityEngine.Object.DestroyImmediate(_rayGO);
        if (_wallLike != null) UnityEngine.Object.DestroyImmediate(_wallLike);
        GameServices.Clear();
    }

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
    public void 붙일_자리가_없으면_고스트가_원점이_아니라_조준점에_남는다()
    {
        var placerType = FindType("Survive.Building.BuildPlacer");
        Assert.IsNotNull(placerType, "BuildPlacer 타입을 찾지 못했다");

        _placerGO = new GameObject("TestBuildPlacer");
        var placer = _placerGO.AddComponent(placerType);

        // 조준 기준점을 원점이 아닌 곳에 둔다 — 버그가 살아 있었다면
        // 결과가 우연히 (0,0,0)과 겹쳐서 테스트가 무력해지는 일을 막는다.
        _rayGO = new GameObject("TestRayOrigin");
        _rayGO.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

        SetPrivateField(placerType, placer, "rayOrigin", _rayGO.transform);
        // 지형이 전혀 없어도(레이어 마스크를 다 꺼서) 결정적으로 재현되게 한다.
        SetPrivateField(placerType, placer, "surfaceMask", (LayerMask)0);

        float maxDistance = (float)GetPrivateField(placerType, placer, "maxDistance");

        placerType.GetMethod("Select", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(placer, new object[] { _wallLike });

        var evaluate = placerType.GetMethod("Evaluate", BindingFlags.Public | BindingFlags.Instance);
        var args = new object[] { null, null };
        var resultObj = evaluate.Invoke(placer, args);
        var result = (PlacementResult)resultObj;
        var position = (Vector3)args[0];

        Assert.AreEqual(PlacementResult.NoAnchor, result,
            "이 시나리오는 붙일 자리가 없는 벽이어야 한다 — 전제가 깨졌다");

        // 조준점 = rayOrigin.position + forward * maxDistance (레이가 아무것도 못 맞혔을 때)
        var expectedAim = _rayGO.transform.position + _rayGO.transform.forward * maxDistance;

        Assert.AreNotEqual(Vector3.zero, position,
            "고스트가 원점으로 순간이동했다 — §1.1 회귀");
        Assert.Less(Vector3.Distance(expectedAim, position), 0.01f,
            $"고스트가 조준점({expectedAim})이 아니라 {position}에 있다");
    }

    static void SetPrivateField(Type type, object instance, string name, object value)
    {
        var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"필드 '{name}'을 찾지 못했다 — 이름이 바뀌었을 수 있다");
        field.SetValue(instance, value);
    }

    static object GetPrivateField(Type type, object instance, string name)
    {
        var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"필드 '{name}'을 찾지 못했다 — 이름이 바뀌었을 수 있다");
        return field.GetValue(instance);
    }
}
