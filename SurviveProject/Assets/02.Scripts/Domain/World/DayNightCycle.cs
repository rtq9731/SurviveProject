using UnityEngine;
using Survive.Domain.Art;

namespace Survive.World
{
    /// <summary>하루의 네 국면. 경계는 <see cref="DayNightCycle"/>의 네 상수가 정한다.</summary>
    public enum DayPhase
    {
        /// <summary>해가 지평선 아래. 세계 전체가 어둡다.</summary>
        Night,

        /// <summary>해뜰녘. 어둠에서 빛으로 넘어가는 구간이다.</summary>
        Dawn,

        /// <summary>낮. 늙은 태양이 주광색 형광등 정도로 떠 있다.</summary>
        Day,

        /// <summary>해질녘. 빛에서 어둠으로 넘어가는 구간이다.</summary>
        Dusk,
    }

    /// <summary>
    /// <b>하루를 초로 세는 자.</b> 시각 하나를 받아 하늘이 어떤 상태인지 답한다.
    ///
    /// <b>왜 순수한가.</b> 화면을 실제로 어둡게 하는 일은 <c>DayNightService</c>가 한다.
    /// 여기 있는 것은 <b>무엇이 얼마나 밝아야 하는가</b>뿐이고, 그래서 Unity 없이
    /// 전수로 확인할 수 있다 — 해질녘 경계에서 값이 튀지 않는가, 시각을 되감아도
    /// 같은 답이 나오는가, 하루를 백 번 건너뛰어도 같은가. 이 셋은 화면을 띄워서는
    /// 확인할 수 없는 것들이다.
    ///
    /// <b>이 세계의 낮이 왜 어둑한가.</b> 태양이 늙었고 그 위에 두꺼운 대기의 산란이
    /// 겹친다(<c>Plan/세계관.md</c> §2). 원인 둘이 같은 방향으로 작동하므로 낮은
    /// 주광색 형광등 정도이고, 그래서 <b>낮에도 랜턴이 쓸모 있다.</b> 낮이 환하면
    /// 랜턴이 밤 전용 물건이 되어 「어둠은 비용」이 하루의 절반에서만 작동한다.
    ///
    /// <b>밤은 어두워지는 것이 아니라 보라색이 짙어지는 것이다.</b> 지평선의
    /// 매크로늄이 자홍으로 빛나므로 완전한 암흑은 아니다 — 깊이가 곧 자홍의
    /// 농도라는 규칙(기획서 §1.5)이 시간 축에서 한 번 더 쓰인다.
    ///
    /// <b>밝은 구역과는 아무 상관이 없다.</b> 밤이 온다는 것은 세계 전체가 어두워진다는
    /// 뜻이지 화톳불이 꺼진다는 뜻이 아니다. 그래서 이 파일은
    /// <see cref="LitZoneRegistry"/>를 부르지 않고, 저쪽도 이 파일을 모른다.
    /// 둘이 얽히는 순간 「밤이 되면 거점이 사라진다」가 조용히 들어온다.
    /// </summary>
    public static class DayNightCycle
    {
        // ── 튜닝값 ──────────────────────────────────────────────
        //
        // 아래 다섯은 전부 <b>사람이 정할 값</b>이다. 여기 적힌 것은 재기 위한
        // 임시값이고, 실측을 붙여 올린 뒤 기획서 §5.14의 튜닝 표에서 확정한다.
        // 코드가 정할 수 있는 것은 「곡선의 모양」이지 「하루가 몇 분인가」가 아니다.

        /// <summary>하루의 길이(초). <b>임시값</b> — 원정 한 번의 길이가 정해지면 따라 움직인다.</summary>
        public const float DayLengthSeconds = 1200f;

        /// <summary>해뜰녘이 시작되는 시각. 그 전은 밤이다.</summary>
        public const float DawnStart = 0.22f;

        /// <summary>해뜰녘이 끝나고 낮이 되는 시각.</summary>
        public const float DawnEnd = 0.28f;

        /// <summary>낮이 끝나고 해질녘이 시작되는 시각.</summary>
        public const float DuskStart = 0.72f;

        /// <summary>해질녘이 끝나고 밤이 되는 시각.</summary>
        public const float DuskEnd = 0.78f;

        // ── 밝기 ────────────────────────────────────────────────

        /// <summary>
        /// 한낮의 태양 세기. <b>형광등 한 줄 정도</b>다 — 이 값을 올리면
        /// 랜턴이 밤 전용 물건이 되므로, 올릴 때는 랜턴 경제를 함께 본다.
        /// </summary>
        public const float DaySunIntensity = 0.75f;

        /// <summary>
        /// 한낮의 환경광 배율. 씬의 환경광이 Flat 모드라
        /// <c>ambientIntensity</c>로는 못 낮춘다 — 색의 명도로만 조절된다
        /// (<c>SceneArtSettingsTests</c>에 실측이 적혀 있다). 그래서 배율이다.
        /// </summary>
        public const float DayAmbientScale = 0.13f;

        /// <summary>
        /// 한밤의 환경광 배율. <b>0이 아니다</b> — 지평선의 매크로늄이 자홍으로
        /// 빛나므로 밤에도 형태가 아주 희미하게는 읽힌다. 다만 이 값으로 무엇을
        /// 알아볼 수는 없어야 한다. 그것이 랜턴이 절실한 이유다.
        /// </summary>
        public const float NightAmbientScale = 0.030f;

        /// <summary>한낮에 태양이 오르는 각도. 90도는 정수리이므로 그보다 낮게 둔다.</summary>
        public const float NoonPitchDegrees = 62f;

        // ── 시계 ────────────────────────────────────────────────

        /// <summary>
        /// 흐른 초를 하루 안의 시각(0~1)으로 접는다. <b>0이 자정이다.</b>
        ///
        /// <b>음수와 거대한 값을 둘 다 받는다.</b> 시각을 되감는 일(디버그·검증)과
        /// 하루를 통째로 건너뛰는 일(잠자기)이 둘 다 생길 것이고, 그때
        /// <c>%</c> 하나로 끝내면 음수 쪽에서 -0.3 같은 값이 나와 국면 판정이
        /// 통째로 어긋난다. C#의 나머지는 피제수의 부호를 따라가기 때문이다.
        ///
        /// <c>double</c>로 받는 것은 실행 시간이 길어질수록 <c>float</c>의 해상도가
        /// 초 단위 아래로 떨어지기 때문이다 — 한 시간만 돌아도 시각이 계단처럼 움직인다.
        /// </summary>
        public static float Wrap(double seconds)
        {
            if (DayLengthSeconds <= 0f) return 0f;

            double t = seconds / DayLengthSeconds;
            t -= System.Math.Floor(t);

            // 아주 작은 음수가 반올림으로 1.0에 붙는 경우를 잘라 낸다.
            // 1.0은 0.0과 같은 시각이므로 언제나 0 쪽으로 접는다.
            float f = (float)t;
            return f >= 1f || f < 0f ? 0f : f;
        }

        /// <summary>시각을 초로 되돌린다. 세이브가 시각만 들고 있어도 되게 하는 창구다.</summary>
        public static double SecondsAt(float timeOfDay) =>
            Mathf.Repeat(timeOfDay, 1f) * (double)DayLengthSeconds;

        // ── 곡선 ────────────────────────────────────────────────

        /// <summary>
        /// 이 시각의 <b>햇빛의 양</b>. 밤 0, 낮 1, 그 사이는 부드럽게 오간다.
        ///
        /// <b>선형이 아니라 smoothstep인 이유.</b> 선형으로 이으면 해질녘의
        /// 시작과 끝에서 밝기의 기울기가 꺾여 화면이 툭 튄다. 사람 눈은 값보다
        /// 값의 변화에 민감해서 그 꺾임을 「번쩍」으로 읽는다.
        /// </summary>
        public static float Daylight(float timeOfDay)
        {
            float t = Mathf.Repeat(timeOfDay, 1f);

            if (t < DawnStart) return 0f;
            if (t < DawnEnd) return SmoothStep01((t - DawnStart) / (DawnEnd - DawnStart));
            if (t < DuskStart) return 1f;
            if (t < DuskEnd) return 1f - SmoothStep01((t - DuskStart) / (DuskEnd - DuskStart));
            return 0f;
        }

        /// <summary>이 시각의 국면. 경계는 <b>시작하는 쪽에 붙는다</b> — 해뜰녘의 첫 순간은 해뜰녘이다.</summary>
        public static DayPhase PhaseAt(float timeOfDay)
        {
            float t = Mathf.Repeat(timeOfDay, 1f);

            if (t < DawnStart) return DayPhase.Night;
            if (t < DawnEnd) return DayPhase.Dawn;
            if (t < DuskStart) return DayPhase.Day;
            if (t < DuskEnd) return DayPhase.Dusk;
            return DayPhase.Night;
        }

        /// <summary>밤인가. 「낫은 밤에 다닌다」가 물어볼 창구다.</summary>
        public static bool IsNight(float timeOfDay) => PhaseAt(timeOfDay) == DayPhase.Night;

        /// <summary>
        /// 태양의 고도(도). 음수면 지평선 아래이고, 그때 태양은 아무것도 밝히지 않는다.
        ///
        /// 밝기와 따로 계산한다. 밝기는 해질녘에 급히 떨어져야 하고 고도는
        /// 하루에 걸쳐 완만히 오르내려야 하는데, 하나로 묶으면 해가 낮에 거의
        /// 움직이지 않다가 해질녘 6분 만에 지평선으로 곤두박질친다.
        /// </summary>
        public static float SunPitchDegrees(float timeOfDay)
        {
            float t = Mathf.Repeat(timeOfDay, 1f);

            // 자정(0)에 가장 낮고 정오(0.5)에 가장 높은 하나의 호.
            float arc = -Mathf.Cos(t * 2f * Mathf.PI);
            return arc * NoonPitchDegrees;
        }

        // ── 색 ──────────────────────────────────────────────────

        /// <summary>
        /// 태양의 색. <b>지표광 회백</b>이다 — 광원 4색의 첫 칸이고,
        /// 지상으로 나온 지금 그것은 곧 햇빛이다(<see cref="ArtPalette.LightShaft"/>).
        /// </summary>
        public static Color SunColor => ArtPalette.LightShaft;

        /// <summary>이 시각의 태양 세기. 지평선 아래면 0이다.</summary>
        public static float SunIntensity(float timeOfDay) => DaySunIntensity * Daylight(timeOfDay);

        /// <summary>
        /// 이 시각의 환경광.
        ///
        /// <b>낮과 밤이 밝기만 다른 것이 아니라 색이 다르다.</b> 낮은 지표광 회백이고
        /// 밤은 자홍이다 — 지평선의 매크로늄이 유일하게 남은 빛이기 때문이다.
        /// 그래서 밤이 오는 것은 화면이 검어지는 일이 아니라 <b>보라색이 짙어지는 일</b>로
        /// 보인다. 색까지 같이 옮기지 않으면 밤이 그냥 「어두운 낮」이 된다.
        /// </summary>
        public static Color AmbientAt(float timeOfDay)
        {
            float d = Daylight(timeOfDay);
            var night = ArtPalette.Macronium * NightAmbientScale;
            var day = ArtPalette.LightShaft * DayAmbientScale;

            var c = Color.Lerp(night, day, d);
            c.a = 1f;
            return c;
        }

        /// <summary>
        /// 화면이 실제로 얼마나 밝아지는지의 <b>대리값</b>. 환경광의 휘도다.
        ///
        /// 화면 휘도 자체는 지형·광원·후처리가 함께 정하므로 여기서 알 수 없다.
        /// 다만 <b>순서</b>는 여기서 정해진다 — 낮이 해질녘보다 밝고 해질녘이
        /// 밤보다 밝다는 것. 그 순서가 화면에서도 지켜지는지는 E2E가 픽셀로 잰다.
        /// </summary>
        public static float AmbientLuminance(float timeOfDay)
        {
            var c = AmbientAt(timeOfDay);
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        static float SmoothStep01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
