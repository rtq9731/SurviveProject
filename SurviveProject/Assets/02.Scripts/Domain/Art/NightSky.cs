using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// <b>머리 위에 무엇이 있는가.</b> 지평선이 뿌옇게 자홍으로 번지는 것은
    /// <see cref="DepthFog"/>가 이미 말했고, 여기서 더하는 것은 <b>그 위에 쏟아지는
    /// 별</b> 하나다 (세계관 §2 · 상세기획서 §7.4).
    ///
    /// <b>왜 별이 규칙이 되는가.</b> 「대기가 탁한 것이 아니라 두꺼운 것」이라는
    /// 한 문장이 이 게임의 안개 전체를 떠받치고 있는데, <b>그 문장을 화면에서
    /// 반증할 수 있는 유일한 자리가 천정</b>이다. 먼지가 낀 대기라면 위도 흐려야
    /// 하고 별은 안 보인다. 별이 보인다는 것이 곧 「맑은 공기가 그저 많다」의
    /// 시각적 증거다.
    ///
    /// <b>별은 광원이 아니다.</b> 빛을 내는 물건 넷(§7.3)에 다섯째를 더하는 것이
    /// 아니라 <b>배경에 찍히는 점</b>이다. 그래서
    /// <list type="bullet">
    /// <item>Light 컴포넌트가 하나도 늘지 않는다 — 아무것도 밝히지 않는다</item>
    /// <item>발광(Emission) 머티리얼이 아니다 — 스카이박스 셰이더가 배경색에
    ///       더할 뿐이라 아트 규칙 검사기의 광원 4색 판정에 걸릴 값이 없다</item>
    /// <item><see cref="StarPeak"/>이 1을 넘지 않는다 — HDR 구간 밖이므로
    ///       블룸이 번지지 않는다. 번지면 그 순간 「빛나는 물건」으로 읽힌다</item>
    /// </list>
    ///
    /// <b>새 색을 만들지 않는다.</b> 별빛은 <see cref="ArtPalette.LightShaft"/>
    /// 지표광 회백이다 — 지상으로 나온 지금 그 칸의 뜻은 <b>햇빛</b>이고
    /// (<c>DayNightCycle.SunColor</c>), 별은 멀리 있는 해다. 다섯째 색을 만들면
    /// 「네 색 안에 들어간다」가 처음으로 깨지는데, 그것을 깨뜨릴 만큼
    /// 별이 특별한 물건이 아니다.
    ///
    /// <b>여기 있는 수는 전부 임시값이다.</b> 별의 수와 밝기는 화면을 보고
    /// 사람이 정할 값이고, 코드가 정할 수 있는 것은 <b>무엇에 매달려 움직이는가</b>다.
    /// </summary>
    public static class NightSky
    {
        // ── 색 ──────────────────────────────────────────────────

        /// <summary>별빛. <b>새 hex가 아니다</b> — 팔레트의 지표광 회백 그대로다.</summary>
        public static Color StarColor => ArtPalette.LightShaft;

        /// <summary>
        /// 가장 밝은 별의 세기(0~1). <b>임시값.</b>
        ///
        /// <b>1을 넘지 않는 것에는 뜻이 있다.</b> 블룸은 HDR 세기 1 위에서만
        /// 번지게 잡혀 있고(§7.5 ③), 별이 번지는 순간 그것은 배경의 점이 아니라
        /// <b>빛나는 물건</b>으로 읽힌다. 광원 넷에 다섯째가 끼는 자리가 거기다.
        /// </summary>
        public const float StarPeak = 0.35f;

        /// <summary>가장 어두운 별이 가장 밝은 별의 몇 배인가. <b>임시값</b> — 전부 같은 밝기면 인쇄한 점처럼 보인다.</summary>
        public const float StarDimmest = 0.22f;

        /// <summary>가장 밝은 별의 휘도. 하늘 휘도와 견주는 자다.</summary>
        public static float StarPeakLuminance => ArtPalette.Luminance(StarColor) * StarPeak;

        // ── 얼마나 촘촘한가 ──────────────────────────────────────
        //
        // 별자리는 큐브맵 여섯 면 위의 격자로 뿌린다. 방향 벡터를 가장 큰 축으로
        // 나눠 면 안의 좌표(-1~1)를 얻고, 그 위에 칸을 깐 뒤 칸마다 별을 하나
        // 둘지 말지 정한다. <b>텍스처가 없다</b> — 별 한 장을 그려 넣으면 그것은
        // 아무도 못 읽는 바이너리가 되고, 밀도를 바꿀 때마다 다시 그려야 한다.

        /// <summary>큐브 한 면의 절반을 몇 칸으로 자르는가. <b>임시값</b>.</summary>
        public const float StarCells = 64f;

        /// <summary>한 칸에 별이 있을 확률. <b>임시값</b>.</summary>
        public const float StarChance = 0.055f;

        /// <summary>별 하나의 반지름(칸 단위). <b>임시값</b>.</summary>
        public const float StarRadius = 0.14f;

        /// <summary>
        /// 하늘 전체의 별 수(어림). 칸 수 × 확률이다.
        ///
        /// 큐브 한 면은 <see cref="StarCells"/>의 두 배씩 두 방향으로 잘리고
        /// 면이 여섯이다. <b>사람이 「몇 개인가」로 이야기할 수 있게 하는 것이
        /// 이 값의 존재 이유</b>다 — 확률과 칸 수 둘을 동시에 머릿속에서 곱하게
        /// 두면 밀도를 조절할 때 감이 서지 않는다.
        /// </summary>
        public static int ApproximateStarCount =>
            Mathf.RoundToInt(6f * (2f * StarCells) * (2f * StarCells) * StarChance);

        /// <summary>별 하나가 하늘에서 차지하는 각(도). 화면에서 몇 픽셀인지 가늠하는 자다.</summary>
        public static float StarAngularDiameterDegrees => 2f * StarRadius * (90f / (2f * StarCells));

        // ── 낮에는 사라진다 ──────────────────────────────────────

        /// <summary>
        /// 별이 완전히 씻겨 사라지는 햇빛의 양. <b>임시값</b>.
        ///
        /// <b>1이 아니라 그보다 훨씬 작다.</b> 별은 해가 다 뜬 뒤에 사라지는 것이
        /// 아니라 <b>박명 중간에</b> 사라진다 — 하늘이 조금만 밝아도 별보다 밝아지기
        /// 때문이다. 1로 두면 대낮에도 별이 희미하게 남아 「낮이 시들시들하다」가
        /// 「낮이 밤 같다」가 된다.
        /// </summary>
        public const float StarWashoutDaylight = 0.35f;

        /// <summary>
        /// 이 햇빛의 양에서 별이 얼마나 남아 보이는가(0~1).
        ///
        /// <b>주인은 밤낮 시계 하나다.</b> 별에 따로 시계를 달지 않는다 —
        /// <c>DayNightCycle.Daylight</c>가 낸 값을 받아서 쓴다. 지평선의 안개색이
        /// 같은 값을 보고 움직이므로(<see cref="DepthFog.HorizonColor"/>) 하늘의
        /// 두 절반이 언제나 같은 시각을 말한다.
        ///
        /// <b>선형이 아니라 smoothstep인 이유</b>는 밝기 곡선과 같다 — 꺾이면
        /// 사람 눈이 그것을 「번쩍」으로 읽는다.
        /// </summary>
        public static float StarVisibility(float daylight)
        {
            float d = Mathf.Clamp01(daylight);
            // 문턱을 0으로 내려도 0으로 나누지 않는다. 그때는 햇빛이 조금이라도
            // 있으면 별이 없는 것이 되고, 그것이 「문턱 0」의 뜻이다.
            float washout = Mathf.Max(StarWashoutDaylight, 1e-4f);

            return 1f - SmoothStep01(d / washout);
        }

        // ── 대기가 삼키는 몫 ─────────────────────────────────────

        /// <summary>
        /// 이 시선 고도(사인값)에서 대기가 배경을 덮는 정도(0~1).
        ///
        /// <b>스카이박스가 읽는 것은 각도가 아니라 시선 벡터의 y다.</b> 각도를
        /// 쓰면 셰이더가 화소마다 asin을 불러야 하고, 그 값은 다시 sin으로
        /// 되돌려질 뿐이다. 그래서 <see cref="DepthFog.SkyCoverage"/>를
        /// <b>사인으로 색인</b>해 둔다 — 규칙은 저쪽 하나에 그대로 있고
        /// 여기서는 자를 바꿔 끼울 뿐이다.
        /// </summary>
        public static float CoverageAtSin(float sinElevation)
        {
            float s = Mathf.Clamp(sinElevation, -1f, 1f);
            return DepthFog.SkyCoverage(Mathf.Asin(s) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// 시선 고도별 대기 두께를 담을 표의 칸 수.
        ///
        /// <b>표는 시각과 무관하다.</b> 하늘색은 <c>지평선색 × 덮는 정도</c>로
        /// 정확히 갈라지고(<see cref="DepthFog.SkyColor"/> 참조) 뒤엣것에는
        /// 시각이 들어가지 않는다. 그래서 이 표는 <b>실행 중 한 번만</b> 만들면
        /// 되고, 매 프레임 바뀌는 것은 색 하나뿐이다 — 스카이박스를 세우는
        /// 가장 값싼 방법이 이것이다.
        /// </summary>
        public const int CoverageSteps = 128;

        /// <summary>
        /// 그 표. 칸 <c>i</c>는 사인 <c>(i + 0.5) / CoverageSteps</c>의 값이다.
        ///
        /// <b>반 칸 밀어 두는 것에는 뜻이 있다.</b> 셰이더는 이 표를 텍스처로 읽고
        /// 텍스처의 표본은 <b>칸의 한가운데</b>에 있다. 끝을 0과 1에 맞춰 구우면
        /// 화소가 읽는 값이 반 칸씩 어긋나고, 그 어긋남은 지평선 쪽에서 가장 큰
        /// 기울기를 만나 눈에 띄는 띠가 된다.
        ///
        /// <b>표를 만드는 것은 여기(Domain)이고 텍스처로 굽는 것은 저쪽이다.</b>
        /// 숫자가 맞는지는 씬 없이 확인할 수 있어야 한다.
        /// </summary>
        public static float[] CoverageTable()
        {
            var table = new float[CoverageSteps];
            for (int i = 0; i < CoverageSteps; i++)
                table[i] = CoverageAtSin((i + 0.5f) / CoverageSteps);
            return table;
        }

        // ── 도구 ────────────────────────────────────────────────

        static float SmoothStep01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
