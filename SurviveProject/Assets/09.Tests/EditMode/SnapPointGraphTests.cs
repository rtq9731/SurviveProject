using System;
using NUnit.Framework;
using UnityEngine;
using Survive.Building;

/// <summary>
/// 붙일 자리 그래프(<see cref="SnapPointGraph"/>)의 경계.
///
/// 씬에서 쓰는 <c>SnapGraph</c>는 정적이라 테스트끼리 오염되지만,
/// 실제 탐색 로직은 인스턴스 클래스로 내려와 있어 테스트마다 새 그래프를
/// 만들면 그만이다. 그래도 정적 격리를 명시적으로 보이려고
/// <c>SetUp</c>에서 <c>Clear()</c>를 부른다.
///
/// 자리 대역은 <see cref="ISnapPoint"/>만 구현한다 — MonoBehaviour도
/// 트랜스폼도 필요 없고, 그게 이 리팩터링의 요점이다.
/// </summary>
public class SnapPointGraphTests
{
    class FakePoint : ISnapPoint
    {
        public bool Alive = true;
        public BuildPieceKind Accepts = BuildPieceKind.Foundation;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;

        public bool IsAlive => Alive;
        public bool Takes(BuildPieceKind kind) => kind != BuildPieceKind.None && (Accepts & kind) != 0;
        public Vector3 SnapPosition => Position;
        public Quaternion SnapRotation => Rotation;
    }

    SnapPointGraph _graph;

    [SetUp]
    public void SetUp()
    {
        _graph = new SnapPointGraph();
        _graph.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        _graph.Clear();
        _graph = null;
    }

    static FakePoint At(float x, float y = 0f, float z = 0f) =>
        new FakePoint { Position = new Vector3(x, y, z) };

    // ── 등록·해제 ───────────────────────────────────────────

    [Test]
    public void 새_그래프는_비어_있다()
    {
        Assert.AreEqual(0, _graph.Count);
    }

    [Test]
    public void 등록하면_수가_는다()
    {
        _graph.Register(At(1f));
        _graph.Register(At(2f));
        Assert.AreEqual(2, _graph.Count);
    }

    [Test]
    public void 같은_자리를_두_번_등록해도_한_번만_들어간다()
    {
        var p = At(1f);
        _graph.Register(p);
        _graph.Register(p);
        Assert.AreEqual(1, _graph.Count);
    }

    [Test]
    public void null_등록은_무시된다()
    {
        _graph.Register(null);
        Assert.AreEqual(0, _graph.Count);
    }

    [Test]
    public void 이미_죽은_자리는_등록되지_않는다()
    {
        _graph.Register(new FakePoint { Alive = false });
        Assert.AreEqual(0, _graph.Count);
    }

    [Test]
    public void 해제하면_수가_준다()
    {
        var p = At(1f);
        _graph.Register(p);
        _graph.Unregister(p);
        Assert.AreEqual(0, _graph.Count);
    }

    [Test]
    public void 등록하지_않은_것을_해제해도_아무_일도_없다()
    {
        _graph.Register(At(1f));
        _graph.Unregister(At(5f));
        _graph.Unregister(null);
        Assert.AreEqual(1, _graph.Count);
    }

    [Test]
    public void Clear는_모두_비운다()
    {
        _graph.Register(At(1f));
        _graph.Register(At(2f));
        _graph.Clear();
        Assert.AreEqual(0, _graph.Count);
    }

    // ── 탐색 ────────────────────────────────────────────────

    [Test]
    public void 빈_그래프에서는_찾지_못한다()
    {
        bool found = _graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 10f,
                                           out var pos, out var rot);
        Assert.IsFalse(found);
        Assert.AreEqual(Vector3.zero, pos);
        Assert.AreEqual(Quaternion.identity, rot);
    }

    [Test]
    public void 반경_안의_자리를_찾는다()
    {
        _graph.Register(At(1.5f));

        Assert.IsTrue(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 2f,
                                            out var pos, out _));
        Assert.AreEqual(new Vector3(1.5f, 0f, 0f), pos);
    }

    [Test]
    public void 반경_밖의_자리는_찾지_못한다()
    {
        _graph.Register(At(2.5f));

        Assert.IsFalse(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 2f,
                                             out _, out _));
    }

    [Test]
    public void 반경과_거리가_정확히_같으면_잡히지_않는다()
    {
        // 반경은 열린 구간이다. 경계에 걸친 자리를 잡으면 미리보기가
        // 반경 밖으로 한 칸 튀어 나가 보인다.
        _graph.Register(At(2f));

        Assert.IsFalse(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 2f,
                                             out _, out _));
    }

    [Test]
    public void 반경_바로_안쪽은_잡힌다()
    {
        _graph.Register(At(1.99f));

        Assert.IsTrue(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 2f,
                                            out _, out _));
    }

    [Test]
    public void 반경_0이면_같은_자리에_있어도_잡히지_않는다()
    {
        _graph.Register(At(0f));

        Assert.IsFalse(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 0f,
                                             out _, out _));
    }

    [Test]
    public void 음수_반경은_절댓값처럼_동작한다()
    {
        // 반경을 제곱해서 비교하므로 부호가 사라진다. 잡아 두지 않으면
        // 나중에 이 성질에 기대는 코드가 생기거나, 반대로 "음수는 막힌다"고
        // 믿는 코드가 생긴다. 호출부(BuildPlacer)는 항상 양수 반경을 넘긴다.
        _graph.Register(At(0.5f));

        Assert.IsTrue(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, -2f,
                                            out _, out _));
        Assert.IsFalse(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, -0.2f,
                                             out _, out _));
    }

    [Test]
    public void 여러_자리_중_가장_가까운_것을_고른다()
    {
        _graph.Register(At(3f));
        _graph.Register(At(1f));
        _graph.Register(At(2f));

        Assert.IsTrue(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 10f,
                                            out var pos, out _));
        Assert.AreEqual(new Vector3(1f, 0f, 0f), pos);
    }

    [Test]
    public void 받지_않는_종류는_후보에서_빠진다()
    {
        _graph.Register(new FakePoint { Position = new Vector3(1f, 0f, 0f), Accepts = BuildPieceKind.Wall });
        _graph.Register(new FakePoint { Position = new Vector3(3f, 0f, 0f), Accepts = BuildPieceKind.Foundation });

        Assert.IsTrue(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 10f,
                                            out var pos, out _));
        Assert.AreEqual(new Vector3(3f, 0f, 0f), pos,
            "더 가까운 자리가 있어도 종류가 맞지 않으면 건너뛴다");
    }

    [Test]
    public void 아무_자리도_종류를_받지_않으면_찾지_못한다()
    {
        _graph.Register(new FakePoint { Position = Vector3.right, Accepts = BuildPieceKind.Wall });

        Assert.IsFalse(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Ramp, 10f,
                                             out _, out _));
    }

    [Test]
    public void None을_찾으면_아무것도_잡히지_않는다()
    {
        _graph.Register(At(0.5f));

        Assert.IsFalse(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.None, 10f,
                                             out _, out _));
    }

    [Test]
    public void 찾은_자리의_위치와_회전을_그대로_돌려준다()
    {
        var rot = Quaternion.Euler(0f, 37f, 0f);
        _graph.Register(new FakePoint
        {
            Position = new Vector3(1f, 2f, 3f),
            Rotation = rot,
            Accepts = BuildPieceKind.Floor
        });

        Assert.IsTrue(_graph.TryFindNearest(new Vector3(1f, 2f, 3.2f), BuildPieceKind.Floor, 2f,
                                            out var pos, out var got));
        Assert.AreEqual(new Vector3(1f, 2f, 3f), pos);
        Assert.AreEqual(rot.eulerAngles.y, got.eulerAngles.y, 0.001f);
    }

    [Test]
    public void 실패하면_출력값은_기본값이다()
    {
        _graph.Register(new FakePoint
        {
            Position = new Vector3(9f, 9f, 9f),
            Rotation = Quaternion.Euler(0f, 90f, 0f)
        });

        Assert.IsFalse(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 1f,
                                             out var pos, out var rot));
        Assert.AreEqual(Vector3.zero, pos);
        Assert.AreEqual(Quaternion.identity, rot);
    }

    [Test]
    public void 여러_종류를_받는_자리는_그중_하나로_찾힌다()
    {
        _graph.Register(new FakePoint { Position = Vector3.right, Accepts = BuildPieceKind.Platform });

        Assert.IsTrue(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 5f, out _, out _));
        Assert.IsTrue(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Floor, 5f, out _, out _));
    }

    // ── 사라진 자리 정리 ────────────────────────────────────

    [Test]
    public void 죽은_자리는_탐색_중에_목록에서_치워진다()
    {
        var dead = At(1f);
        _graph.Register(dead);
        _graph.Register(At(2f));
        Assert.AreEqual(2, _graph.Count);

        dead.Alive = false;
        _graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 10f, out _, out _);

        Assert.AreEqual(1, _graph.Count, "부서진 조각의 자리가 목록에 남아 있다");
    }

    [Test]
    public void 죽은_자리는_후보로_잡히지_않는다()
    {
        _graph.Register(At(3f));

        // Register가 죽은 자리를 막으니, 살아 있는 채로 넣고 나중에 죽인다
        var later = At(1f);
        _graph.Register(later);
        later.Alive = false;

        Assert.IsTrue(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 10f,
                                            out var pos, out _));
        Assert.AreEqual(new Vector3(3f, 0f, 0f), pos, "죽은 자리가 최근접으로 잡혔다");
    }

    [Test]
    public void 전부_죽으면_탐색이_실패하고_목록이_빈다()
    {
        var a = At(1f);
        var b = At(2f);
        _graph.Register(a);
        _graph.Register(b);
        a.Alive = false;
        b.Alive = false;

        Assert.IsFalse(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 10f,
                                             out _, out _));
        Assert.AreEqual(0, _graph.Count);
    }

    [Test]
    public void 뒤에서부터_지워도_앞_항목을_건너뛰지_않는다()
    {
        // 역순 순회 + RemoveAt는 인덱스가 어긋나기 쉬운 조합이다.
        // 죽은 것과 산 것을 번갈아 넣어 한 번에 전부 걸러지는지 본다.
        for (int i = 0; i < 3; i++)
        {
            var dead = At(10f + i);
            _graph.Register(dead);
            dead.Alive = false;

            _graph.Register(At(1f + i));
        }
        Assert.AreEqual(6, _graph.Count);

        Assert.IsTrue(_graph.TryFindNearest(Vector3.zero, BuildPieceKind.Foundation, 20f,
                                            out var pos, out _));
        Assert.AreEqual(new Vector3(1f, 0f, 0f), pos);
        Assert.AreEqual(3, _graph.Count, "죽은 자리 셋이 한 번에 걸러지지 않았다");
    }

    // ── 씬 쪽 배선 ──────────────────────────────────────────

    [Test]
    public void BuildSnapPoint는_그래프가_보는_얼굴을_구현한다()
    {
        // BuildSnapPoint는 asmdef 없는 폴더(Assembly-CSharp) 소속이라
        // 여기서 직접 참조할 수 없다. 리플렉션으로 배선만 확인한다.
        var t = Type.GetType("Survive.Building.BuildSnapPoint, Assembly-CSharp");
        if (t == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType("Survive.Building.BuildSnapPoint");
                if (t != null) break;
            }
        }
        Assert.IsNotNull(t, "BuildSnapPoint 타입을 찾지 못했다");
        Assert.IsTrue(typeof(ISnapPoint).IsAssignableFrom(t),
            "BuildSnapPoint가 ISnapPoint를 구현하지 않는다 — 스냅 그래프에 아무것도 등록되지 않는다");
    }
}
