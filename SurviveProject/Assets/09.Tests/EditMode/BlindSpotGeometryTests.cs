using NUnit.Framework;
using UnityEngine;
using Survive.World;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// <b>사각은 오프셋이 아니라 「등 뒤 도달 거리」가 정한다.</b>
    ///
    /// 기획서 §9는 랜턴 오프셋과 낫 공격 거리를 <b>같이 보라</b>고 했고, 회신 ⑧은
    /// "오프셋 3m에 공격 거리 2.2m면 여유가 0.8m"라고 셈했다. 그 셈이 이 파일이
    /// 잡은 것이다 — <b>견줄 것은 오프셋이 아니다.</b> 낫이 어둠에 선 채로 때리려면
    /// 사람 뒤로 가장 가까운 어두운 자리(<b>반경 − 오프셋</b>)까지 공격 거리가 닿아야 한다.
    ///
    /// <b>그 부호가 2026-08-07에 뒤집혔다.</b> 오프셋 3m일 때 등 뒤 도달은 5m였고
    /// 여유는 −2.8m — 어둠에 선 채로 닿는 자리가 한 점도 없었다. 그때는
    /// <see cref="LitZoneRegistry.IsBlindSide"/>가 <b>반평면</b>이라 낫이 그래도 때렸고,
    /// 대신 때리는 시간의 96.4%를 빛 안에 서 있었다. 두 규칙이 서로 다른 사각을
    /// 말하고 있었던 것이다.
    ///
    /// 이제 그 규칙이 <b>원</b>으로 돌아왔고(사용자 판단: "내 약간 뒤까지 커버하는
    /// 원형 범위"), 오프셋도 <b>6.5m</b>로 함께 올라갔다. 등 뒤 도달 1.5m 대 사거리
    /// 2.2m이므로 <b>여유는 +0.7m이고 그런 자리가 있다.</b> 이 파일이 재는 것은
    /// 이제 "왜 없는가"가 아니라 <b>"얼마나 있는가"</b>다.
    /// </summary>
    public class BlindSpotGeometryTests
    {
        const float 낫사거리 = 2.2f;      // 08.Data/Creatures/낫.asset
        const float 티어1반경 = 8f;
        const float 티어1오프셋 = 6.5f;

        // ── 지금 값 ─────────────────────────────────────────────

        [Test]
        public void 지금_값에서는_어둠에_선_채로_때릴_자리가_있다()
        {
            // 2026-08-07에 뒤집힌 자리다. 예전 값(오프셋 3m)에서는 여유가 −2.8m라
            // 그런 자리가 없었고, 아래 옛값_회귀 테스트가 그 셈을 그대로 들고 있다.
            Assert.That(LanternRule.DarkStrikeMargin(티어1반경, 티어1오프셋, 낫사거리),
                        Is.EqualTo(0.7f).Within(0.001f),
                        "여유 = 2.2 − 1.5 = +0.7m");
            Assert.That(LanternRule.DarkStrikeArea(티어1반경, 티어1오프셋, 낫사거리),
                        Is.GreaterThan(0f));
            Assert.That(LanternRule.DarkStrikeAngle(티어1반경, 티어1오프셋, 낫사거리),
                        Is.GreaterThan(0f));
            Assert.That(LanternRule.HasDarkStrikeWindow(티어1반경, 티어1오프셋, 낫사거리), Is.True);
        }

        [Test]
        public void 옛값이_왜_안_됐는지를_셈으로_남긴다()
        {
            // <b>바뀐 이유를 코드가 들고 있어야 한다.</b> 오프셋 3m는 등 뒤 도달이
            // 5m라 사거리 2.2m로는 닿지 않았다 — 그것이 이 라운드가 값을 올린 이유다.
            const float 옛오프셋 = 3f;

            Assert.That(LanternRule.DarkStrikeMargin(티어1반경, 옛오프셋, 낫사거리),
                        Is.EqualTo(-2.8f).Within(0.001f), "여유 = 2.2 − 5.0 = −2.8m");
            Assert.That(LanternRule.DarkStrikeArea(티어1반경, 옛오프셋, 낫사거리),
                        Is.EqualTo(0f).Within(1e-4f));
            Assert.That(LanternRule.HasDarkStrikeWindow(티어1반경, 옛오프셋, 낫사거리), Is.False);
        }

        [Test]
        public void 규칙의_손잡이가_실제로_지금_값이다()
        {
            // 이 등호가 깨지면 위 테스트가 재는 것이 게임이 아니라 상수의 사본이 된다.
            Assert.That(LanternRule.RadiusForTier(1), Is.EqualTo(티어1반경).Within(1e-4f));
            Assert.That(LanternRule.OffsetForTier(1), Is.EqualTo(티어1오프셋).Within(1e-4f));
            Assert.That(LanternRule.BackReachForTier(1), Is.EqualTo(1.5f).Within(1e-4f));
        }

        // ── 여유의 부호가 사각의 있고 없음을 가른다 ─────────────

        [TestCase(8f, 3f, 2.2f, false)]     // 지금 값 — 여유 −2.8
        [TestCase(8f, 3f, 5.0f, false)]     // 문턱 정확히 — 닿기만 할 뿐 넓이가 0이다
        [TestCase(8f, 3f, 5.5f, true)]      // 사거리를 늘려 연 경우
        // 문턱(5.8m)에 정확히 세우지 않는다 — 8f − 5.8f가 2.19999981이라
        // 부동소수점 한 자리로 부호가 뒤집힌다. 문턱 자체는 아래 전용 검사가 본다.
        [TestCase(8f, 5.7f, 2.2f, false)]   // 문턱 바로 아래
        [TestCase(8f, 6.5f, 2.2f, true)]    // 오프셋을 늘려 연 경우
        [TestCase(8f, 0f, 2.2f, false)]     // 밀린 것이 없으면 사각도 없다
        public void 여유가_양수일_때만_사각이_있다(float r, float o, float reach, bool 열림)
        {
            bool 여유양수 = LanternRule.DarkStrikeMargin(r, o, reach) > 0f;
            Assert.That(여유양수, Is.EqualTo(열림), "여유의 부호");
            Assert.That(LanternRule.HasDarkStrikeWindow(r, o, reach), Is.EqualTo(열림));

            float 넓이 = LanternRule.DarkStrikeArea(r, o, reach);
            float 각도 = LanternRule.DarkStrikeAngle(r, o, reach);
            if (열림)
            {
                Assert.That(넓이, Is.GreaterThan(0f), "열렸으면 넓이가 있다");
                Assert.That(각도, Is.GreaterThan(0f), "열렸으면 각도가 있다");
            }
            else
            {
                Assert.That(넓이, Is.EqualTo(0f).Within(1e-3f), "닫혔으면 넓이가 0이다");
                Assert.That(각도, Is.EqualTo(0f).Within(1e-3f), "닫혔으면 각도가 0이다");
            }
        }

        [Test]
        public void 문턱은_두_손잡이의_같은_자리다()
        {
            // 오프셋 쪽 문턱과 사거리 쪽 문턱이 같은 부등식의 양쪽이라는 것.
            float 오프셋문턱 = LanternRule.OffsetThatOpensDark(티어1반경, 낫사거리);
            Assert.That(오프셋문턱, Is.EqualTo(5.8f).Within(1e-4f));

            // 오프셋이 6.5m로 오르면서 이 문턱이 5m에서 1.5m로 내려왔다 —
            // 곧 <b>사거리 2.2m인 낫이 그 문턱을 넘어섰다</b>는 뜻이고,
            // 그것이 이 라운드가 값을 올린 이유 자체다.
            float 사거리문턱 = LanternRule.ReachThatOpensDark(티어1반경, 티어1오프셋);
            Assert.That(사거리문턱, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(낫사거리, Is.GreaterThan(사거리문턱), "낫이 문턱을 넘는다");

            Assert.That(LanternRule.DarkStrikeMargin(티어1반경, 오프셋문턱, 낫사거리),
                        Is.EqualTo(0f).Within(1e-3f), "오프셋 문턱에서 여유가 정확히 0");
            Assert.That(LanternRule.DarkStrikeMargin(티어1반경, 티어1오프셋, 사거리문턱),
                        Is.EqualTo(0f).Within(1e-3f), "사거리 문턱에서 여유가 정확히 0");
        }

        [Test]
        public void 여유가_음수로_내려가면_사각이_사라진다()
        {
            // 문턱 바로 위에서는 있고, 한 뼘 아래로 내리면 없다.
            Assert.That(LanternRule.DarkStrikeArea(티어1반경, 6.0f, 낫사거리),
                        Is.GreaterThan(0f), "오프셋 6.0m에서는 있다");
            Assert.That(LanternRule.DarkStrikeArea(티어1반경, 5.5f, 낫사거리),
                        Is.EqualTo(0f).Within(1e-3f), "오프셋 5.5m로 내리면 사라진다");
        }

        // ── 단조성 ──────────────────────────────────────────────

        [Test]
        public void 오프셋을_밀수록_사각이_단조로_커진다()
        {
            float 앞넓이 = -1f, 앞각도 = -1f;
            for (float o = 0f; o <= 12f; o += 0.25f)
            {
                float 넓이 = LanternRule.DarkStrikeArea(티어1반경, o, 낫사거리);
                float 각도 = LanternRule.DarkStrikeAngle(티어1반경, o, 낫사거리);
                Assert.That(넓이, Is.GreaterThanOrEqualTo(앞넓이 - 1e-3f), $"오프셋 {o}에서 넓이가 줄었다");
                Assert.That(각도, Is.GreaterThanOrEqualTo(앞각도 - 1e-3f), $"오프셋 {o}에서 각도가 줄었다");
                앞넓이 = 넓이; 앞각도 = 각도;
            }
        }

        [Test]
        public void 사거리를_늘릴수록_사각이_단조로_커진다()
        {
            float 앞 = -1f;
            for (float reach = 0f; reach <= 12f; reach += 0.25f)
            {
                float 넓이 = LanternRule.DarkStrikeArea(티어1반경, 티어1오프셋, reach);
                Assert.That(넓이, Is.GreaterThanOrEqualTo(앞 - 1e-3f), $"사거리 {reach}에서 넓이가 줄었다");
                앞 = 넓이;
            }
        }

        [Test]
        public void 빛이_사람에게서_완전히_떠나면_사방이_사각이다()
        {
            // 오프셋이 반경보다 크면 사람 자신이 빛 밖이다. 그때는 방향이 없다.
            Assert.That(LanternRule.DarkStrikeAngle(8f, 12f, 2.2f), Is.EqualTo(360f).Within(1e-3f));
            Assert.That(LanternRule.DarkStrikeArea(8f, 12f, 2.2f),
                        Is.EqualTo(Mathf.PI * 2.2f * 2.2f).Within(1e-3f), "사거리 원 전체가 어둡다");
        }

        [Test]
        public void 반경이_0이면_사거리_원_전체가_사각이다()
        {
            Assert.That(LanternRule.DarkStrikeArea(0f, 0f, 2.2f),
                        Is.EqualTo(Mathf.PI * 2.2f * 2.2f).Within(1e-3f));
        }

        [Test]
        public void 사거리가_0이면_잴_것이_없다()
        {
            Assert.That(LanternRule.DarkStrikeArea(8f, 3f, 0f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(LanternRule.DarkStrikeAngle(8f, 3f, 0f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(LanternRule.HasDarkStrikeWindow(8f, 3f, 0f), Is.False);
        }

        // ── 닫힌 식이 실제 넓이와 맞는가 ────────────────────────

        /// <summary>
        /// <b>공식을 믿지 않고 세어 본다.</b> 5cm 격자로 사거리 원을 훑어
        /// 빛 밖인 칸을 세고, 닫힌 식이 내놓은 넓이와 견준다.
        /// E2E가 세계에서 하는 일과 똑같은 것을 씬 없이 한다.
        /// </summary>
        [TestCase(8f, 6.5f, 2.2f)]
        [TestCase(8f, 8.0f, 2.2f)]
        [TestCase(8f, 3.0f, 6.0f)]
        [TestCase(4f, 3.0f, 2.2f)]
        public void 닫힌_식이_격자로_센_넓이와_맞는다(float r, float o, float reach)
        {
            const float 눈금 = 0.02f;
            var 중심 = new Vector2(o, 0f);
            int 어두운칸 = 0;
            for (float x = -reach; x <= reach; x += 눈금)
                for (float y = -reach; y <= reach; y += 눈금)
                {
                    if (x * x + y * y > reach * reach) continue;
                    if ((new Vector2(x, y) - 중심).magnitude > r) 어두운칸++;
                }

            float 센넓이 = 어두운칸 * 눈금 * 눈금;
            float 식넓이 = LanternRule.DarkStrikeArea(r, o, reach);
            // 격자로 세면 경계 한 줄만큼 어긋난다. 둘레 × 눈금의 절반이 그 크기다.
            Assert.That(식넓이, Is.EqualTo(센넓이).Within(0.15f),
                        $"반경 {r} 오프셋 {o} 사거리 {reach}");
        }

        // ── 티어와의 관계 ───────────────────────────────────────

        [Test]
        public void 티어를_올려도_여유는_그대로다()
        {
            // 등 뒤 도달 거리는 티어와 무관하게 고정이므로(BackReach) 여유도 고정이다.
            // 상위 티어가 "등 뒤를 돈으로 사는 물건"이 아니라는 축이 여기에도 걸린다.
            float 기준 = LanternRule.DarkStrikeMargin(LanternRule.RadiusForTier(1),
                                                      LanternRule.OffsetForTier(1), 낫사거리);
            for (int tier = 1; tier <= LanternRule.MaxTier; tier++)
            {
                float 여유 = LanternRule.DarkStrikeMargin(LanternRule.RadiusForTier(tier),
                                                          LanternRule.OffsetForTier(tier), 낫사거리);
                Assert.That(여유, Is.EqualTo(기준).Within(1e-3f), $"티어 {tier}");
            }
        }

        [Test]
        public void 사각의_깊이와_어둠_속_타격은_여전히_다른_물음이다()
        {
            // 둘 다 양수가 됐지만 <b>같은 값이 아니다.</b> 깊이는 6.5m(내준 자리의
            // 크기)이고 어둠 속 타격 여유는 0.7m(거기 서서 팔이 닿는 만큼)다.
            // 이 둘을 같은 값으로 읽어서 "여유 0.8m"라는 셈이 나왔었다.
            Assert.That(LanternRule.BlindSpotDepthForTier(1), Is.EqualTo(6.5f).Within(1e-4f));
            Assert.That(LanternRule.DarkStrikeMargin(티어1반경, 티어1오프셋, 낫사거리),
                        Is.EqualTo(0.7f).Within(1e-3f));
            Assert.That(LanternRule.BlindSpotDepthForTier(1),
                        Is.Not.EqualTo(LanternRule.DarkStrikeMargin(티어1반경, 티어1오프셋, 낫사거리)));
        }
    }
}
