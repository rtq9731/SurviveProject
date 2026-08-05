using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Art;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 후처리의 대원칙 — <b>어두운 픽셀을 들어올리지 않는다</b> — 을 값으로 지킨다.
    ///
    /// 이 게임의 어둠은 환경광 0으로 이미 만들어져 있다. 후처리가 할 일의 절반은
    /// 그 검정을 지키는 것이고, 그것을 깨는 방법은 대개 "노출을 조금만 올리자"다.
    /// 여기서 사람이 감마 화면에서 직접 정한 것 말고는 어떤 상태도 노출을
    /// 건드리지 못한다는 것을 못 박는다.
    /// </summary>
    public class PostFxGradeTests
    {
        [Test]
        public void 기본_상태에서는_노출을_건드리지_않는다()
        {
            var look = PostFxGrade.Evaluate(PostFxState.Default);
            Assert.AreEqual(0f, look.PostExposure, 1e-5f,
                "아무 일도 없는데 화면이 밝아진다 — 어둠이 무너진다");
        }

        [Test]
        public void 어떤_상태_조합도_노출을_올리지_못한다()
        {
            foreach (bool lantern in new[] { false, true })
            foreach (bool lit in new[] { false, true })
            foreach (float macronium in new[] { 0f, 1f })
            foreach (float reaper in new[] { 0f, 1f })
            foreach (float hurt in new[] { 0f, 1f })
            {
                var s = new PostFxState(lantern, lit, macronium, reaper, hurt, GammaGrade.Neutral);
                Assert.AreEqual(0f, PostFxGrade.Evaluate(s).PostExposure, 1e-5f,
                    $"lantern={lantern} lit={lit} 액면={macronium} 낫={reaper} 피격={hurt}");
            }
        }

        [Test]
        public void 노출을_움직이는_것은_감마_설정뿐이다()
        {
            var dark = new PostFxState(false, false, 0f, 0f, 0f, 0f);
            var bright = new PostFxState(false, false, 0f, 0f, 0f, 1f);

            Assert.Less(PostFxGrade.Evaluate(dark).PostExposure, 0f);
            Assert.Greater(PostFxGrade.Evaluate(bright).PostExposure, 0f);
        }

        [Test]
        public void 상시_색수차는_없다()
        {
            var look = PostFxGrade.Evaluate(PostFxState.Default);
            Assert.AreEqual(0f, look.ChromaticAberration, 1e-5f,
                "평상시에 색수차가 걸려 있다 — 상시 CA 금지");
        }

        [Test]
        public void 액면에_붙으면_색수차와_붉은_기가_걸린다()
        {
            var near = new PostFxState(false, false, 1f, 0f, 0f, GammaGrade.Neutral);
            var look = PostFxGrade.Evaluate(near);

            Assert.AreEqual(PostFxGrade.ChromaticMacronium, look.ChromaticAberration, 1e-4f);
            Assert.Greater(look.ColorFilter.r, look.ColorFilter.g,
                "액면 근처인데 화면이 자홍 쪽으로 기울지 않았다");
            Assert.Greater(look.ColorFilter.b, look.ColorFilter.g);
        }

        [Test]
        public void 색_필터는_기본_상태에서_흰색이다()
        {
            var look = PostFxGrade.Evaluate(PostFxState.Default);
            Assert.AreEqual(1f, look.ColorFilter.r, 1e-5f);
            Assert.AreEqual(1f, look.ColorFilter.g, 1e-5f);
            Assert.AreEqual(1f, look.ColorFilter.b, 1e-5f);
        }

        [Test]
        public void 피격_순간의_색수차가_액면보다_세다()
        {
            var hit = new PostFxState(false, false, 1f, 0f, 1f, GammaGrade.Neutral);
            Assert.AreEqual(PostFxGrade.ChromaticHurt,
                            PostFxGrade.Evaluate(hit).ChromaticAberration, 1e-4f);
        }

        [Test]
        public void 색수차는_겹쳐도_더해지지_않는다()
        {
            var both = new PostFxState(false, false, 1f, 0f, 1f, GammaGrade.Neutral);
            Assert.LessOrEqual(PostFxGrade.Evaluate(both).ChromaticAberration,
                               Mathf.Max(PostFxGrade.ChromaticMacronium, PostFxGrade.ChromaticHurt) + 1e-4f);
        }

        [Test]
        public void 낫이_붙으면_그레인이_미세하게_오른다()
        {
            var far = PostFxGrade.Evaluate(new PostFxState(false, false, 0f, 0f, 0f, GammaGrade.Neutral));
            var near = PostFxGrade.Evaluate(new PostFxState(false, false, 0f, 1f, 0f, GammaGrade.Neutral));

            Assert.Greater(near.FilmGrain, far.FilmGrain);
            Assert.Less(near.FilmGrain, 0.25f, "그레인이 세면 검정이 회색으로 들린다");
        }

        [Test]
        public void 비네트는_상시_보조_수준을_넘지_않는다()
        {
            foreach (bool lantern in new[] { false, true })
            foreach (bool lit in new[] { false, true })
            {
                var look = PostFxGrade.Evaluate(
                    new PostFxState(lantern, lit, 0f, 0f, 0f, GammaGrade.Neutral));

                Assert.GreaterOrEqual(look.Vignette, PostFxGrade.VignetteBase - 1e-4f);
                Assert.LessOrEqual(look.Vignette,
                    PostFxGrade.VignetteBase + PostFxGrade.VignetteDarkBonus + 1e-4f,
                    "비네트가 답답한 수준까지 조인다");
            }
        }

        [Test]
        public void 랜턴을_켜면_비네트가_풀린다()
        {
            var off = PostFxGrade.Evaluate(new PostFxState(false, false, 0f, 0f, 0f, GammaGrade.Neutral));
            var on = PostFxGrade.Evaluate(new PostFxState(true, false, 0f, 0f, 0f, GammaGrade.Neutral));

            Assert.Greater(off.Vignette, on.Vignette);
        }

        [Test]
        public void 밝은_구역_안이면_랜턴이_꺼져도_풀린다()
        {
            var inZone = PostFxGrade.Evaluate(new PostFxState(false, true, 0f, 0f, 0f, GammaGrade.Neutral));
            var outside = PostFxGrade.Evaluate(new PostFxState(false, false, 0f, 0f, 0f, GammaGrade.Neutral));

            Assert.Less(inZone.Vignette, outside.Vignette);
        }

        [Test]
        public void 입력은_0에서_1로_잘린다()
        {
            var s = new PostFxState(false, false, 5f, -3f, 99f, 7f);
            Assert.AreEqual(1f, s.MacroniumProximity, 1e-5f);
            Assert.AreEqual(0f, s.ReaperProximity, 1e-5f);
            Assert.AreEqual(1f, s.HurtPulse, 1e-5f);
            Assert.AreEqual(1f, s.Gamma, 1e-5f);
        }
    }
}
