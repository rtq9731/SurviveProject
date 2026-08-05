using NUnit.Framework;
using Survive.Domain.Art;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 감마 조절 화면의 규칙 (백로그 42).
    ///
    /// 가장 중요한 단언은 <see cref="기본값에서는_아무_일도_일어나지_않는다"/>다.
    /// 검증(E2E·스크린샷 비교)이 사람의 화면 설정에 흔들리면 같은 빌드가
    /// 기계마다 다른 결과를 낸다.
    /// </summary>
    public class GammaGradeTests
    {
        [Test]
        public void 기본값에서는_아무_일도_일어나지_않는다()
        {
            Assert.AreEqual(0f, GammaGrade.Exposure(GammaGrade.Neutral), 1e-5f);
            Assert.IsTrue(GammaGrade.IsNeutral(GammaGrade.Neutral));
        }

        [Test]
        public void 슬라이더는_단조롭게_밝아진다()
        {
            float previous = float.MinValue;
            for (float v = 0f; v <= 1.0001f; v += 0.05f)
            {
                float ev = GammaGrade.Exposure(v);
                Assert.Greater(ev, previous - 1e-6f, $"v={v}에서 되레 어두워졌다");
                previous = ev;
            }
        }

        [Test]
        public void 양_끝은_정해진_한계를_넘지_않는다()
        {
            Assert.AreEqual(GammaGrade.MinExposure, GammaGrade.Exposure(0f), 1e-5f);
            Assert.AreEqual(GammaGrade.MaxExposure, GammaGrade.Exposure(1f), 1e-5f);
        }

        [Test]
        public void 범위_밖의_값은_잘린다()
        {
            Assert.AreEqual(GammaGrade.MinExposure, GammaGrade.Exposure(-5f), 1e-5f);
            Assert.AreEqual(GammaGrade.MaxExposure, GammaGrade.Exposure(9f), 1e-5f);
        }

        [Test]
        public void 극단값에서_암부_밝기가_실제로_달라진다()
        {
            // 게이트: "슬라이더 극단값에서 암부 픽셀 값 변화 실측"의 순수부.
            float dark = GammaGrade.Apply(GammaGrade.PatchLuminance, GammaGrade.Exposure(0f));
            float mid = GammaGrade.Apply(GammaGrade.PatchLuminance, GammaGrade.Exposure(GammaGrade.Neutral));
            float bright = GammaGrade.Apply(GammaGrade.PatchLuminance, GammaGrade.Exposure(1f));

            Assert.AreEqual(GammaGrade.PatchLuminance, mid, 1e-6f, "기본값이 무늬 밝기를 바꾼다");
            Assert.Less(dark, mid * 0.7f, "가장 어두운 쪽에서 눈에 띄게 어두워지지 않는다");
            Assert.Greater(bright, mid * 2f, "가장 밝은 쪽에서 눈에 띄게 밝아지지 않는다");
        }

        [Test]
        public void 조절_무늬는_잘_맞춘_화면에서만_겨우_보이는_밝기다()
        {
            // 너무 밝으면 아무 화면에서나 보여서 조절이 되지 않고,
            // 너무 어두우면 아무 화면에서도 안 보인다.
            Assert.Greater(GammaGrade.PatchLuminance, 0.005f);
            Assert.Less(GammaGrade.PatchLuminance, 0.05f);
        }
    }
}
