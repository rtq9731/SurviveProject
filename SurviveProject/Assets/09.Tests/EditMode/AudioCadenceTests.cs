using NUnit.Framework;
using Survive.Domain.Audio;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 리듬으로 말하는 소리들 — 발소리와, 어둠 속에서 다가오는 것의 기척.
    ///
    /// 둘 다 "언제 낼 것인가"가 곧 내용이다. 발소리는 간격으로 속도를 말하고,
    /// 기척은 간격으로 거리를 말한다. 그래서 클립이 무엇이든 이 규칙만은
    /// 먼저 맞아야 한다.
    /// </summary>
    public class AudioCadenceTests
    {
        // ── 발소리 ───────────────────────────────────────────────

        [Test]
        public void 서_있으면_발소리가_나지_않는다()
        {
            Assert.IsFalse(FootstepCadence.ShouldStep(true, 0f, 10f));
            Assert.IsFalse(FootstepCadence.IsMoving(0f));
        }

        [Test]
        public void 공중에서는_발소리가_나지_않는다()
        {
            // 점프 중에 발소리가 나면 발이 어디 있는지가 화면과 어긋난다.
            Assert.IsFalse(FootstepCadence.ShouldStep(false, 7f, 10f));
        }

        [Test]
        public void 빨리_갈수록_간격이_짧아진다()
        {
            float previous = float.MaxValue;
            for (float speed = FootstepCadence.MinAudibleSpeed; speed <= 9f; speed += 0.25f)
            {
                float stride = FootstepCadence.StrideSeconds(speed);
                Assert.LessOrEqual(stride, previous + 1e-6f, $"speed={speed}에서 되레 느려졌다");
                previous = stride;
            }
        }

        [Test]
        public void 간격은_정한_두_값_사이에_머문다()
        {
            for (float speed = 0f; speed <= 20f; speed += 0.5f)
            {
                float stride = FootstepCadence.StrideSeconds(speed);
                Assert.GreaterOrEqual(stride, FootstepCadence.RunStrideSeconds - 1e-5f);
                Assert.LessOrEqual(stride, FootstepCadence.WalkStrideSeconds + 1e-5f);
            }
        }

        [Test]
        public void 걷기와_뛰기가_갈린다()
        {
            Assert.IsFalse(FootstepCadence.IsRunning(5f), "걷기 속도가 뛰기로 셈해졌다");
            Assert.IsTrue(FootstepCadence.IsRunning(7f), "달리기 속도가 걷기로 셈해졌다");
        }

        [Test]
        public void 간격이_차야_한_걸음이_나간다()
        {
            const float speed = 5f;
            float stride = FootstepCadence.StrideSeconds(speed);

            Assert.IsFalse(FootstepCadence.ShouldStep(true, speed, stride * 0.5f));
            Assert.IsTrue(FootstepCadence.ShouldStep(true, speed, stride));
        }

        // ── 기척 ─────────────────────────────────────────────────

        [Test]
        public void 범위_밖의_것은_들리지_않는다()
        {
            Assert.IsFalse(ApproachAudio.IsAudible(40f, 30f));
            Assert.AreEqual(0f, ApproachAudio.Loudness(40f, 30f), 1e-5f);

            // 소리를 낼 범위가 없으면 아무 거리에서도 들리지 않는다.
            Assert.IsFalse(ApproachAudio.IsAudible(1f, 0f));
            Assert.AreEqual(0f, ApproachAudio.Loudness(1f, 0f), 1e-5f);
        }

        [Test]
        public void 가까워질수록_자주_그리고_크게_들린다()
        {
            const float range = 30f;
            float previousInterval = float.MaxValue;
            float previousLoudness = -1f;

            for (float d = range - 0.5f; d >= 0f; d -= 0.5f)
            {
                float interval = ApproachAudio.IntervalSeconds(d, range);
                float loudness = ApproachAudio.Loudness(d, range);

                Assert.LessOrEqual(interval, previousInterval + 1e-5f, $"d={d}에서 되레 뜸해졌다");
                Assert.GreaterOrEqual(loudness, previousLoudness - 1e-5f, $"d={d}에서 되레 작아졌다");

                previousInterval = interval;
                previousLoudness = loudness;
            }
        }

        [Test]
        public void 간격은_정한_두_값_사이에_머문다_기척()
        {
            const float range = 25f;
            for (float d = 0f; d < range; d += 0.5f)
            {
                float interval = ApproachAudio.IntervalSeconds(d, range);
                Assert.GreaterOrEqual(interval, ApproachAudio.NearIntervalSeconds - 1e-5f);
                Assert.LessOrEqual(interval, ApproachAudio.FarIntervalSeconds + 1e-5f);
            }
        }

        [Test]
        public void 코앞에서는_더_커지지_않는다()
        {
            const float range = 30f;
            Assert.AreEqual(1f, ApproachAudio.Closeness(ApproachAudio.PointBlank, range), 1e-5f);
            Assert.AreEqual(1f, ApproachAudio.Closeness(0f, range), 1e-5f);
            Assert.AreEqual(ApproachAudio.NearIntervalSeconds,
                            ApproachAudio.IntervalSeconds(0f, range), 1e-5f);
        }

        [Test]
        public void 들리기_시작하는_언저리에도_기척은_남는다()
        {
            // 0에서 시작하면 들리기 시작하는 순간이 뚝 켜지는 것으로 들린다.
            const float range = 30f;
            float edge = ApproachAudio.Loudness(range - 0.01f, range);

            Assert.Greater(edge, 0f);
            Assert.AreEqual(ApproachAudio.FloorLoudness, edge, 0.02f);
        }
    }
}
