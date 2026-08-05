using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Audio;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 소리를 고르는 규칙과, <b>소리가 없을 때 무슨 일이 일어나는가</b>.
    ///
    /// 두 번째가 이 프로젝트에서는 첫 번째보다 중요하다. 지금 이 저장소에는
    /// 소리 파일이 하나도 없고, 그 상태에서도 게임이 지금과 똑같이 돌아야 한다.
    /// "비어 있으면 조용히 아무것도 하지 않는다"는 것은 눈으로 확인할 수 없다 —
    /// 여기서 못을 박아 둔다.
    /// </summary>
    public class AudioCueTests
    {
        // ── 클립 고르기 ──────────────────────────────────────────

        [Test]
        public void 고를_것이_없으면_아무것도_고르지_않는다()
        {
            Assert.AreEqual(ClipRoulette.None, ClipRoulette.Next(0, ClipRoulette.None, 0.5f));
            Assert.AreEqual(ClipRoulette.None, ClipRoulette.Next(-3, 2, 0.9f));
        }

        [Test]
        public void 하나뿐이면_언제나_그것을_고른다()
        {
            for (float r = 0f; r <= 1f; r += 0.1f)
                Assert.AreEqual(0, ClipRoulette.Next(1, 0, r));
        }

        [Test]
        public void 직전에_쓴_것은_다시_고르지_않는다()
        {
            // 발소리가 같은 파일로 두 번 연달아 나면 귀가 즉시 녹음임을 알아챈다.
            for (int count = 2; count <= 6; count++)
            {
                for (int previous = 0; previous < count; previous++)
                {
                    for (float r = 0f; r < 1f; r += 0.017f)
                    {
                        int pick = ClipRoulette.Next(count, previous, r);
                        Assert.AreNotEqual(previous, pick,
                            $"count={count} previous={previous} r={r}에서 같은 것이 또 나왔다");
                        Assert.GreaterOrEqual(pick, 0);
                        Assert.Less(pick, count);
                    }
                }
            }
        }

        [Test]
        public void 직전_것이_없으면_전부가_후보다()
        {
            var seen = new bool[4];
            for (float r = 0f; r < 1f; r += 0.01f)
                seen[ClipRoulette.Next(4, ClipRoulette.None, r)] = true;

            for (int i = 0; i < seen.Length; i++)
                Assert.IsTrue(seen[i], $"{i}번은 한 번도 뽑히지 않았다");
        }

        [Test]
        public void 범위를_벗어난_난수도_배열_밖으로_나가지_않는다()
        {
            foreach (float r in new[] { -5f, -0.001f, 1f, 1.5f, 99f })
            {
                int pick = ClipRoulette.Next(5, 2, r);
                Assert.GreaterOrEqual(pick, 0);
                Assert.Less(pick, 5);
                Assert.AreNotEqual(2, pick);
            }
        }

        // ── 빈 cue ───────────────────────────────────────────────

        [Test]
        public void 클립이_없는_소리는_아무것도_내지_않는다()
        {
            var cue = ScriptableObject.CreateInstance<AudioCueSO>();
            try
            {
                Assert.IsFalse(cue.HasClips);
                Assert.AreEqual(0, cue.ValidClipCount);
                Assert.IsNull(cue.PickClip(0.5f));
            }
            finally { Object.DestroyImmediate(cue); }
        }

        [Test]
        public void 배열에_빈_칸만_있어도_없는_것으로_본다()
        {
            // 인스펙터에서 크기만 늘려 놓고 아직 안 채운 상태.
            var cue = ScriptableObject.CreateInstance<AudioCueSO>();
            try
            {
                cue.clips = new AudioClip[3];
                Assert.IsFalse(cue.HasClips);
                Assert.IsNull(cue.PickClip(0.9f));
            }
            finally { Object.DestroyImmediate(cue); }
        }

        [Test]
        public void 나중에_꽂은_클립도_바로_잡힌다()
        {
            // 추려 둔 배열을 캐시했다가 실측에서 걸린 자리다 — 캐시는 OnEnable에서
            // 채워지는데 그때는 clips가 비어 있어서, 나중에 넣어도 영영 "소리 없음"이었다.
            var cue = ScriptableObject.CreateInstance<AudioCueSO>();
            var a = AudioClip.Create("a", 64, 1, 44100, false);
            var b = AudioClip.Create("b", 64, 1, 44100, false);
            try
            {
                Assert.IsFalse(cue.HasClips);

                cue.clips = new[] { a, null, b };
                Assert.IsTrue(cue.HasClips);
                Assert.AreEqual(2, cue.ValidClipCount, "빈 칸이 유효 클립으로 세어졌다");

                Assert.AreSame(a, cue.ClipAt(0));
                Assert.AreSame(b, cue.ClipAt(1), "빈 칸을 건너뛰지 않았다");
                Assert.IsNull(cue.ClipAt(2));

                Assert.IsNotNull(cue.PickClip(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(cue);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void UI_갈래는_세계에_자리가_없다()
        {
            var cue = ScriptableObject.CreateInstance<AudioCueSO>();
            try
            {
                cue.spatial = true;
                cue.bus = AudioBus.UI;
                Assert.AreEqual(0f, cue.SpatialBlend, 1e-5f, "UI 소리가 3D로 났다");

                cue.bus = AudioBus.Sfx;
                Assert.AreEqual(1f, cue.SpatialBlend, 1e-5f);
            }
            finally { Object.DestroyImmediate(cue); }
        }

        [Test]
        public void 볼륨과_음정은_정한_범위_안에서만_흔들린다()
        {
            var cue = ScriptableObject.CreateInstance<AudioCueSO>();
            try
            {
                cue.volumeMin = 0.4f; cue.volumeMax = 0.8f;
                cue.pitchMin = 0.9f; cue.pitchMax = 1.2f;

                for (float r = 0f; r <= 1f; r += 0.05f)
                {
                    Assert.GreaterOrEqual(cue.PickVolume(r), 0.4f - 1e-5f);
                    Assert.LessOrEqual(cue.PickVolume(r), 0.8f + 1e-5f);
                    Assert.GreaterOrEqual(cue.PickPitch(r), 0.9f - 1e-5f);
                    Assert.LessOrEqual(cue.PickPitch(r), 1.2f + 1e-5f);
                }
            }
            finally { Object.DestroyImmediate(cue); }
        }

        [Test]
        public void 꽂힌_것이_있으면_그것이_표를_이긴다()
        {
            var mine = ScriptableObject.CreateInstance<AudioCueSO>();
            var book = ScriptableObject.CreateInstance<AudioCueSO>();
            try
            {
                Assert.AreSame(mine, AudioCueBookSO.Or(mine, book));
                Assert.AreSame(book, AudioCueBookSO.Or(null, book));
                Assert.IsNull(AudioCueBookSO.Or(null, null));
            }
            finally
            {
                Object.DestroyImmediate(mine);
                Object.DestroyImmediate(book);
            }
        }
    }
}
