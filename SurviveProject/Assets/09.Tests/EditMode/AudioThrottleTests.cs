using NUnit.Framework;
using Survive.Domain.Audio;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 겹침 막이 — 소리를 들어서는 확인할 수 없는 것들.
    ///
    /// 이 프로젝트에는 아직 소리 파일이 하나도 없고, 앞으로 꽂더라도 사람도 나도
    /// 그 소리를 듣고 "괜찮다"를 판정할 수 없다. 판정할 수 있는 것은 <b>수</b>다 —
    /// 쉰 번 불렀을 때 몇 번이 나갔는가. 그 규칙 전부가 여기에서 검증된다.
    /// </summary>
    public class AudioThrottleTests
    {
        const int Cue = 101;
        const int Other = 202;

        [Test]
        public void 한_프레임에_쉰_번_불러도_상한을_넘지_않는다()
        {
            var t = new AudioThrottle(24);
            const float now = 3f;      // 같은 프레임이므로 시간이 흐르지 않는다

            int played = 0;
            for (int i = 0; i < 50; i++)
                if (t.TryClaim(Cue, now, 0f, 0.4f, perCueLimit: 3)) played++;

            Assert.AreEqual(3, played, "같은 프레임에 같은 소리가 상한보다 많이 나갔다");
            Assert.AreEqual(3, t.ActiveVoicesOf(Cue, now));
        }

        [Test]
        public void 최소_간격_안의_재요청은_무시된다()
        {
            var t = new AudioThrottle(24);

            Assert.IsTrue(t.TryClaim(Cue, 0f, 0.25f, 0.1f, 8));
            Assert.IsFalse(t.TryClaim(Cue, 0.1f, 0.25f, 0.1f, 8), "간격 안인데 나갔다");
            Assert.IsFalse(t.TryClaim(Cue, 0.24f, 0.25f, 0.1f, 8), "경계 직전인데 나갔다");
            Assert.IsTrue(t.TryClaim(Cue, 0.25f, 0.25f, 0.1f, 8), "간격이 지났는데 막혔다");
        }

        [Test]
        public void 최소_간격이_0이면_검사하지_않는다()
        {
            var t = new AudioThrottle(24);

            Assert.IsTrue(t.TryClaim(Cue, 1f, 0f, 0.1f, 8));
            Assert.IsTrue(t.TryClaim(Cue, 1f, 0f, 0.1f, 8));
        }

        [Test]
        public void 다른_소리는_서로의_간격에_걸리지_않는다()
        {
            var t = new AudioThrottle(24);

            Assert.IsTrue(t.TryClaim(Cue, 0f, 1f, 0.1f, 8));
            Assert.IsFalse(t.TryClaim(Cue, 0.1f, 1f, 0.1f, 8));
            Assert.IsTrue(t.TryClaim(Other, 0.1f, 1f, 0.1f, 8), "남의 간격에 걸렸다");
        }

        [Test]
        public void 소리가_끝나면_자리가_돌아온다()
        {
            var t = new AudioThrottle(24);

            for (int i = 0; i < 3; i++) t.TryClaim(Cue, 0f, 0f, 0.5f, 3);
            Assert.AreEqual(3, t.ActiveVoicesOf(Cue, 0f));

            // 0.5초짜리 셋이 다 끝난 뒤
            Assert.AreEqual(0, t.ActiveVoicesOf(Cue, 0.6f));
            Assert.IsTrue(t.TryClaim(Cue, 0.6f, 0f, 0.5f, 3));
        }

        [Test]
        public void 전체_상한은_소리_종류를_가리지_않는다()
        {
            var t = new AudioThrottle(4);
            const float now = 0f;

            Assert.IsTrue(t.TryClaim(1, now, 0f, 1f, 0));
            Assert.IsTrue(t.TryClaim(2, now, 0f, 1f, 0));
            Assert.IsTrue(t.TryClaim(3, now, 0f, 1f, 0));
            Assert.IsTrue(t.TryClaim(4, now, 0f, 1f, 0));
            Assert.IsFalse(t.TryClaim(5, now, 0f, 1f, 0), "풀이 찼는데 다섯 번째가 나갔다");
            Assert.AreEqual(4, t.ActiveVoices(now));
        }

        [Test]
        public void 소리별_상한이_0이면_전체_상한만_본다()
        {
            var t = new AudioThrottle(8);
            const float now = 0f;

            int played = 0;
            for (int i = 0; i < 30; i++)
                if (t.TryClaim(Cue, now, 0f, 1f, perCueLimit: 0)) played++;

            Assert.AreEqual(8, played);
        }

        [Test]
        public void 루프는_멈출_때까지_자리를_붙든다()
        {
            var t = new AudioThrottle(2);

            Assert.IsTrue(t.TryClaim(Cue, 0f, 0f, AudioThrottle.Endless, 1));
            Assert.AreEqual(1, t.ActiveVoices(1000f), "루프가 시간이 지났다고 사라졌다");

            t.Release(Cue);
            Assert.AreEqual(0, t.ActiveVoices(1000f), "멈췄는데 자리가 남았다");
        }

        [Test]
        public void 없는_소리를_멈춰도_남의_자리를_지우지_않는다()
        {
            var t = new AudioThrottle(4);
            t.TryClaim(Cue, 0f, 0f, AudioThrottle.Endless, 1);

            t.Release(Other);
            Assert.AreEqual(1, t.ActiveVoices(0f));
        }

        [Test]
        public void 되돌리면_간격_기록까지_사라진다()
        {
            var t = new AudioThrottle(4);

            Assert.IsTrue(t.TryClaim(Cue, 100f, 5f, 0.1f, 4));
            t.Reset();

            // 씬을 다시 올리면 시각이 0으로 돌아간다. 옛 기록이 남아 있으면
            // "아직 간격 안"으로 읽혀 한동안 소리가 죽는다.
            Assert.IsTrue(t.TryClaim(Cue, 0f, 5f, 0.1f, 4));
            Assert.AreEqual(1, t.ActiveVoices(0f));
        }

        [Test]
        public void 상한은_적어도_하나다()
        {
            var t = new AudioThrottle(0);
            Assert.AreEqual(1, t.MaxVoices);
            Assert.IsTrue(t.TryClaim(Cue, 0f, 0f, 1f, 0));
            Assert.IsFalse(t.TryClaim(Other, 0f, 0f, 1f, 0));
        }

        [Test]
        public void 막힌_요청은_간격_기록을_밀지_않는다()
        {
            var t = new AudioThrottle(24);

            Assert.IsTrue(t.TryClaim(Cue, 0f, 1f, 0.1f, 8));

            // 0.5초에 막힌 요청이 기록을 밀었다면 1.0초에도 막혀야 한다.
            Assert.IsFalse(t.TryClaim(Cue, 0.5f, 1f, 0.1f, 8));
            Assert.IsTrue(t.TryClaim(Cue, 1f, 1f, 0.1f, 8), "막힌 요청이 간격을 밀었다");
        }
    }
}
