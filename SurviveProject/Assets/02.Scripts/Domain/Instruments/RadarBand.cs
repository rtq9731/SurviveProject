using System;
using UnityEngine;

namespace Survive.Instruments
{
    /// <summary>
    /// 레이더가 쓰는 전자파의 성질. <b>이 클래스의 요점은 값이 하나뿐이라는 것이다.</b>
    ///
    /// 기획이 정한 원리는 한 줄이다 — <b>낮은 주파수의 전자파</b>. 느리고 반응성이 낮다.
    /// 그래서 여기에 사람이 적는 물리량은 <see cref="wavelengthMeters"/> 하나이고,
    /// 레이더가 무엇을 잡고 무엇을 못 잡는지는 전부 그 하나에서 계산으로 따라 나온다.
    ///
    /// <b>왜 이렇게 만드는가.</b> "낫은 안 잡힌다"를 규칙 표에 손으로 적어 두면 그것은
    /// 예외다. 예외는 반드시 둘째 예외를 부르고("그럼 큰 낫은?", "느린 낫은?"), 결국
    /// 무엇이 잡히는지 아무도 설명할 수 없게 된다. 파장 하나에서 파생시키면 그런 질문에
    /// 규칙이 스스로 답한다.
    ///
    /// 파장 λ에서 나오는 것 셋:
    /// <list type="bullet">
    /// <item><b>해상도</b> — 파장보다 작은 것은 한 칸 안에서 뭉개진다. 그래서
    ///       발밑 균열도 낫도 사라진다. 어둠이 지형을 감춘다는 축이 여기서 지켜진다.</item>
    /// <item><b>추적 한계 속도</b> — 한 장을 얻는 데 <see cref="sweepSeconds"/>가 걸리므로,
    ///       그 사이에 한 칸(=해상도)을 벗어나는 것은 번져서 남지 않는다.</item>
    /// <item><b>투과 깊이</b> — 파장이 길수록 매질을 깊이 지난다. 그래서 바다 아래
    ///       깊은 층과 공동이 잡힌다.</item>
    /// </list>
    /// 넷째는 파장이 아니라 <b>적분</b>에서 나온다 — 저주파의 되돌아오는 신호는 약해서
    /// 여러 장을 겹쳐 쌓아야 형상이 선다. 쌓는 동안 안테나가 제자리에 있어야 위상이
    /// 맞으므로, 움직이면 쌓아 둔 것이 못 쓰게 된다(<see cref="CoherenceRadiusMeters"/>).
    /// 그것이 "서서 기다리는 값"의 정체다.
    /// </summary>
    [Serializable]
    public class RadarBand
    {
        // ── 사람이 정하는 값 ─────────────────────────────────────

        /// <summary>
        /// 파장(m). <b>이 장치의 유일한 물리 입력이다.</b> 8m는 대략 37MHz대 —
        /// 실제 지표투과 레이더가 깊이를 얻으려고 내려가는 그 대역이고,
        /// 그 대역이 해상도를 잃는 것도 실제와 같다.
        /// </summary>
        [Tooltip("파장(m). 클수록 깊이 들어가고 대신 작은 것을 못 본다")]
        [Min(0.01f)] public float wavelengthMeters = 8f;

        /// <summary>
        /// 한 장을 얻는 데 걸리는 시간(초). 되돌아오는 신호가 약해 한 번 훑는 데
        /// 오래 걸린다 — "반응성이 낮다"의 수치판이다.
        /// </summary>
        [Tooltip("한 장을 얻는 데 걸리는 시간(초)")]
        [Min(0.01f)] public float sweepSeconds = 8f;

        /// <summary>닿는 거리(m). 다른 섬까지 닿아야 §14 4단계가 성립한다.</summary>
        [Tooltip("닿는 거리(m)")]
        [Min(1f)] public float rangeMeters = 4000f;

        // ── 파장에서 따라 나오는 값 ──────────────────────────────

        /// <summary>
        /// 해상도 한 칸의 크기(m)가 파장의 몇 배인가.
        ///
        /// 1인 것은 이 장치가 파장만 한 안테나를 갖추지 못한 손제작품이기 때문이다.
        /// 제대로 만든 배열이라면 λ/2까지 내려가지만, 스크랩으로 감은 것에는
        /// 그만한 이득이 없다.
        /// </summary>
        public const float ResolutionPerWavelength = 1f;

        /// <summary>
        /// 위상이 맞다고 볼 수 있는 반경이 파장의 몇 분의 일인가.
        /// λ/8은 결맞음 적분의 통상 기준이다 — 그 이상 어긋나면 겹쳐 쌓은 것이
        /// 서로를 지운다.
        /// </summary>
        public const float CoherenceWavelengthFraction = 8f;

        /// <summary>투과 깊이가 파장의 몇 배인가. 저주파일수록 깊이 든다.</summary>
        public const float PenetrationPerWavelength = 40f;

        /// <summary>
        /// 이보다 작은 것은 한 칸 안에서 뭉개져 형상이 서지 않는다.
        /// <b>낫과 발밑 균열이 여기서 함께 떨어진다</b> — 종류를 보고 거르는 것이 아니라
        /// 크기가 모자라서 떨어지는 것이다.
        /// </summary>
        public float ResolutionMeters => wavelengthMeters * ResolutionPerWavelength;

        /// <summary>
        /// 한 장을 얻는 동안 한 칸을 벗어나는 속도. 이보다 빠른 것은 번져서 남지 않는다.
        /// </summary>
        public float MaxTrackableSpeedMps =>
            sweepSeconds <= 0f ? float.PositiveInfinity : ResolutionMeters / sweepSeconds;

        /// <summary>이 깊이까지는 매질을 지나 되돌아온다.</summary>
        public float PenetrationDepthMeters => wavelengthMeters * PenetrationPerWavelength;

        /// <summary>
        /// 관측하는 동안 장치가 벗어나면 안 되는 반경(m). 걸음 한 번이면 넘는다 —
        /// 그래서 "서서 기다린다"가 규칙이 아니라 결과가 된다.
        /// </summary>
        public float CoherenceRadiusMeters =>
            wavelengthMeters / Mathf.Max(0.0001f, CoherenceWavelengthFraction);
    }
}
