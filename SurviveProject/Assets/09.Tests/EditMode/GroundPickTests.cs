using System.Collections.Generic;
using NUnit.Framework;
using Survive.World;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// <b>제자리에서 두 번 세워도 사람이 뜨지 않는다.</b>
    ///
    /// E2E 하네스는 사람을 어느 자리에 세울 때 위에서 아래로 광선을 쏴 발밑을 잰다.
    /// 그런데 <b>이미 그 자리에 서 있으면</b> 광선이 지면보다 먼저 자기 몸통을 맞는다.
    /// 그 높이를 지면으로 알고 그 위에 세우면 사람은 자기 키만큼 올라가고,
    /// 같은 호출이 되풀이될수록 계속 올라간다 — 실측 5.2m까지 떴다.
    ///
    /// 여러 라운드가 이 증상을 우회만 하고 지나갔으므로 여기에 못을 박는다.
    /// 재는 것은 물리가 아니라 <b>맞은 것을 고르는 규칙</b>이다.
    /// </summary>
    public class GroundPickTests
    {
        const float 지면 = 50f;
        const float 키 = 1.8f;
        const float 발올림 = 1.0f;      // 몸 중심을 지면 위로 얼마나 올리는가
        const float 쏘는높이 = 30f;

        /// <summary>
        /// 지면 위 <paramref name="선높이"/>에 사람이 서 있을 때, 위에서 쏜 광선이 맞는 것들.
        /// 사람이 없으면 지면 하나만 맞는다.
        /// </summary>
        static List<GroundProbeHit> 광선이_맞는것(float? 선높이)
        {
            var hits = new List<GroundProbeHit>();
            float 시작 = (선높이 ?? 지면) + 쏘는높이;

            if (선높이.HasValue)
            {
                float 정수리 = 선높이.Value + 키 * 0.5f;
                hits.Add(new GroundProbeHit(시작 - 정수리, 정수리, true));
            }
            hits.Add(new GroundProbeHit(시작 - 지면, 지면, false));
            return hits;
        }

        [Test]
        public void 아무도_없으면_지면을_고른다()
        {
            Assert.IsTrue(GroundPick.TryPick(광선이_맞는것(null), out float y));
            Assert.AreEqual(지면, y, 0.001f);
        }

        [Test]
        public void 이미_서_있어도_자기_몸이_아니라_지면을_고른다()
        {
            var hits = 광선이_맞는것(지면 + 발올림);

            Assert.IsTrue(GroundPick.TryPick(hits, out float y));
            Assert.AreEqual(지면, y, 0.001f, "자기 몸통을 지면으로 셌다");
        }

        [Test]
        public void 제자리에서_두_번_세워도_같은_높이다()
        {
            // 첫 번째: 아무도 없는 자리에 세운다
            Assert.IsTrue(GroundPick.TryStandY(광선이_맞는것(null), 발올림, out float 첫번째));

            // 두 번째: 방금 세운 그 사람이 그 자리에 있는 채로 다시 세운다
            Assert.IsTrue(GroundPick.TryStandY(광선이_맞는것(첫번째), 발올림, out float 두번째));

            Assert.AreEqual(첫번째, 두번째, 0.001f,
                            $"제자리에서 다시 세웠더니 {두번째 - 첫번째:F2}m 떠올랐다");

            // 세 번째까지. 뜨는 결함은 한 번에 조금씩 쌓여서 두 번으로는 눈에 안 띌 수 있다.
            Assert.IsTrue(GroundPick.TryStandY(광선이_맞는것(두번째), 발올림, out float 세번째));
            Assert.AreEqual(첫번째, 세번째, 0.001f);
        }

        /// <summary>
        /// <b>음성 확인.</b> 자기 몸을 세는 옛 방식(맞은 것 중 가장 위)이라면
        /// 정말로 떠오르는가. 여기가 통과하지 않으면 위의 검사들은 아무것도 막고 있지 않다.
        /// </summary>
        [Test]
        public void 자기_몸을_세면_부를_때마다_키만큼_떠오른다()
        {
            float 높이 = 지면 + 발올림;

            for (int i = 0; i < 3; i++)
            {
                var hits = 광선이_맞는것(높이);

                // 옛 방식: Physics.Raycast가 돌려주던 「가장 먼저 맞은 것」
                float 가장위 = float.MinValue;
                float 가장가까운 = float.MaxValue;
                foreach (var h in hits)
                    if (h.Distance < 가장가까운) { 가장가까운 = h.Distance; 가장위 = h.Y; }

                높이 = 가장위 + 발올림;
            }

            Assert.Greater(높이 - (지면 + 발올림), 2.5f,
                           "옛 방식인데도 떠오르지 않는다 — 이 검사가 함정을 재현하지 못한다");
        }

        [Test]
        public void 맞은_것이_내_몸뿐이면_고르지_못한다()
        {
            var hits = new List<GroundProbeHit> { new GroundProbeHit(1f, 60f, true) };

            Assert.IsFalse(GroundPick.TryPick(hits, out _),
                           "내 몸밖에 없는데 그것을 지면으로 삼았다");
        }
    }
}
