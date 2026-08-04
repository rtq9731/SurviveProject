using System;
using NUnit.Framework;
using UnityEngine;
using Survive.Core;

/// <summary>
/// 저장본 왕복. 저장이 "파일이 써졌다"로 끝나면 의미가 없다 —
/// 쓴 것을 그대로 되읽을 수 있어야 이어하기가 된다.
///
/// 파일·경로는 여기서 다루지 않는다. 그 부분은 <c>SaveService</c>에 남아
/// PlayMode의 E2ESave 시나리오가 본다. 여기는 순수 변환만 본다.
/// </summary>
public class SaveSerializerTests
{
    [Serializable]
    class Payload
    {
        public int count;
        public string label;
    }

    static SaveSnapshot Sample()
    {
        var s = new SaveSnapshot { sceneName = "MainScene" };
        s.Add("player.inventory", "Survive.Items.InventoryState", "{\"scrap\":7}");
        s.Add("chapter", "Survive.Progression.ChapterState", "{\"index\":2}");
        return s;
    }

    static SaveSnapshot RoundTrip(SaveSnapshot s)
    {
        Assert.IsTrue(SaveSerializer.TryDeserialize(SaveSerializer.Serialize(s),
                                                    out var back, out var error),
                      $"왕복에 실패했다: {error}");
        return back;
    }

    // ── 왕복 ────────────────────────────────────────────────

    [Test]
    public void 왕복하면_씬_이름이_보존된다()
    {
        Assert.AreEqual("MainScene", RoundTrip(Sample()).sceneName);
    }

    [Test]
    public void 왕복하면_항목_수가_보존된다()
    {
        Assert.AreEqual(2, RoundTrip(Sample()).Count);
    }

    [Test]
    public void 왕복하면_항목의_키_타입_내용이_그대로다()
    {
        var back = RoundTrip(Sample());

        var inv = back.Find("player.inventory");
        Assert.IsNotNull(inv, "인벤토리 항목이 사라졌다");
        Assert.AreEqual("Survive.Items.InventoryState", inv.type);
        Assert.AreEqual("{\"scrap\":7}", inv.json);

        var chapter = back.Find("chapter");
        Assert.IsNotNull(chapter, "챕터 항목이 사라졌다");
        Assert.AreEqual("{\"index\":2}", chapter.json);
    }

    [Test]
    public void 왕복하면_항목_순서가_유지된다()
    {
        var back = RoundTrip(Sample());
        Assert.AreEqual("player.inventory", back.entries[0].key);
        Assert.AreEqual("chapter", back.entries[1].key);
    }

    [Test]
    public void 실제_상태_객체도_왕복에서_값이_보존된다()
    {
        // SaveService가 하는 것과 같은 두 겹 직렬화(상태 → JSON 문자열 → 저장본)
        var state = new Payload { count = 42, label = "부서진 잠수정" };

        var snap = new SaveSnapshot { sceneName = "MainScene" };
        snap.Add("probe", typeof(Payload).AssemblyQualifiedName, JsonUtility.ToJson(state));

        var entry = RoundTrip(snap).Find("probe");
        Assert.IsNotNull(entry);

        var restored = JsonUtility.FromJson<Payload>(entry.json);
        Assert.AreEqual(42, restored.count);
        Assert.AreEqual("부서진 잠수정", restored.label);
    }

    [Test]
    public void 항목이_없는_저장본도_왕복된다()
    {
        var back = RoundTrip(new SaveSnapshot { sceneName = "Empty" });
        Assert.AreEqual("Empty", back.sceneName);
        Assert.AreEqual(0, back.Count);
        Assert.IsNotNull(back.entries);
    }

    [Test]
    public void 한글과_따옴표가_섞인_내용도_깨지지_않는다()
    {
        var s = new SaveSnapshot { sceneName = "심해 기지" };
        s.Add("메모", "System.String", "{\"글\":\"따옴표 \\\" 와 줄바꿈\\n 포함\"}");

        var back = RoundTrip(s);
        Assert.AreEqual("심해 기지", back.sceneName);
        Assert.AreEqual("{\"글\":\"따옴표 \\\" 와 줄바꿈\\n 포함\"}", back.Find("메모").json);
    }

    // ── 버전 ────────────────────────────────────────────────

    [Test]
    public void 직렬화하면_현재_버전이_찍힌다()
    {
        var s = Sample();
        SaveSerializer.Serialize(s);
        Assert.AreEqual(SaveSnapshot.CurrentVersion, s.version);
        Assert.IsFalse(RoundTrip(Sample()).IsVersionless);
    }

    [Test]
    public void 버전_없는_저장본도_읽히고_구버전으로_표시된다()
    {
        // 버전 필드가 생기기 전에 남은 저장본
        const string legacy =
            "{\"sceneName\":\"MainScene\",\"entries\":[{\"key\":\"chapter\",\"json\":\"{}\",\"type\":\"X\"}]}";

        Assert.IsTrue(SaveSerializer.TryDeserialize(legacy, out var back, out var error), error);
        Assert.AreEqual(0, back.version);
        Assert.IsTrue(back.IsVersionless, "버전 없는 저장본을 구버전으로 알아보지 못했다");
        Assert.AreEqual(1, back.Count, "구버전이라고 내용까지 버리면 안 된다");
        Assert.AreEqual("MainScene", back.sceneName);
    }

    [Test]
    public void 모르는_필드가_섞여_있어도_읽힌다()
    {
        const string future =
            "{\"version\":99,\"sceneName\":\"A\",\"playtimeSeconds\":123,\"entries\":[]}";

        Assert.IsTrue(SaveSerializer.TryDeserialize(future, out var back, out var error), error);
        Assert.AreEqual(99, back.version);
        Assert.AreEqual("A", back.sceneName);
    }

    // ── 망가진 입력 ─────────────────────────────────────────

    [Test]
    public void 빈_문자열은_실패로_돌아온다()
    {
        Assert.IsFalse(SaveSerializer.TryDeserialize("", out var back, out var error));
        Assert.IsNull(back);
        Assert.IsFalse(string.IsNullOrEmpty(error), "실패 사유가 비어 있다");
    }

    [Test]
    public void null_입력은_실패로_돌아온다()
    {
        Assert.IsFalse(SaveSerializer.TryDeserialize(null, out var back, out var error));
        Assert.IsNull(back);
        Assert.IsFalse(string.IsNullOrEmpty(error));
    }

    [Test]
    public void 공백만_있는_파일은_실패로_돌아온다()
    {
        Assert.IsFalse(SaveSerializer.TryDeserialize("   \n\t ", out _, out var error));
        Assert.IsFalse(string.IsNullOrEmpty(error));
    }

    [Test]
    public void 망가진_JSON은_예외를_던지지_않고_실패로_돌아온다()
    {
        Assert.DoesNotThrow(() =>
        {
            Assert.IsFalse(SaveSerializer.TryDeserialize("{ 이건 JSON이 아니다", out var back, out var error));
            Assert.IsNull(back);
            Assert.IsFalse(string.IsNullOrEmpty(error));
        });
    }

    [Test]
    public void entries가_없는_JSON은_빈_목록으로_읽힌다()
    {
        Assert.IsTrue(SaveSerializer.TryDeserialize("{\"sceneName\":\"A\"}", out var back, out var error), error);
        Assert.IsNotNull(back.entries, "entries가 null이면 불러오기가 그 자리에서 죽는다");
        Assert.AreEqual(0, back.Count);
    }

    [Test]
    public void 키가_빈_항목은_읽을_때_버려진다()
    {
        // 어떤 저장 대상과도 이어질 수 없는 칸이다
        const string text =
            "{\"sceneName\":\"A\",\"entries\":[" +
            "{\"key\":\"\",\"json\":\"{}\",\"type\":\"X\"}," +
            "{\"key\":\"chapter\",\"json\":\"{}\",\"type\":\"X\"}]}";

        Assert.IsTrue(SaveSerializer.TryDeserialize(text, out var back, out var error), error);
        Assert.AreEqual(1, back.Count);
        Assert.AreEqual("chapter", back.entries[0].key);
    }

    [Test]
    public void null_저장본을_직렬화하면_빈_문자열이다()
    {
        Assert.AreEqual(string.Empty, SaveSerializer.Serialize(null));
    }

    [Test]
    public void entries가_null인_저장본도_직렬화된다()
    {
        var s = new SaveSnapshot { sceneName = "A", entries = null };
        Assert.DoesNotThrow(() => SaveSerializer.Serialize(s));
        Assert.IsNotNull(s.entries);
    }

    // ── 키 조회 ─────────────────────────────────────────────

    [Test]
    public void 알_수_없는_키는_null을_돌려준다()
    {
        Assert.IsNull(Sample().Find("여기_없는_키"));
    }

    [Test]
    public void null이나_빈_키로_찾으면_null이다()
    {
        Assert.IsNull(Sample().Find(null));
        Assert.IsNull(Sample().Find(""));
    }

    [Test]
    public void 항목이_없는_저장본에서_찾아도_죽지_않는다()
    {
        Assert.IsNull(new SaveSnapshot().Find("chapter"));
        Assert.IsNull(new SaveSnapshot { entries = null }.Find("chapter"));
        Assert.AreEqual(0, new SaveSnapshot { entries = null }.Count);
    }

    [Test]
    public void 같은_키가_둘이면_먼저_넣은_것을_돌려준다()
    {
        var s = new SaveSnapshot();
        s.Add("dup", "A", "{\"n\":1}");
        s.Add("dup", "B", "{\"n\":2}");

        Assert.AreEqual(2, s.Count, "키가 겹친다고 조용히 지우면 결함이 저장본에서 사라진다");
        Assert.AreEqual("{\"n\":1}", s.Find("dup").json);
    }

    [Test]
    public void Add는_넣은_항목을_그대로_돌려준다()
    {
        var s = new SaveSnapshot();
        var e = s.Add("k", "T", "{}");

        Assert.AreEqual("k", e.key);
        Assert.AreEqual("T", e.type);
        Assert.AreEqual("{}", e.json);
        Assert.AreSame(e, s.Find("k"));
    }

    [Test]
    public void entries가_null인_저장본에도_넣을_수_있다()
    {
        var s = new SaveSnapshot { entries = null };
        s.Add("k", "T", "{}");
        Assert.AreEqual(1, s.Count);
    }
}
