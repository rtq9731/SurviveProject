using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 후처리가 지금 무엇을 보고 판단해야 하는가. 전부 0~1로 정규화해서 받는다 —
    /// Unity 타입(Light·Volume·Collider)이 하나도 들어오지 않아야 이 판단을
    /// 씬 없이 시험할 수 있다.
    /// </summary>
    public readonly struct PostFxState
    {
        /// <summary>랜턴이 켜져 있는가.</summary>
        public readonly bool LanternOn;

        /// <summary>화톳불·군락 등 어떤 광원이든 그 안에 서 있는가.</summary>
        public readonly bool InLitZone;

        /// <summary>매크로늄 액면과의 근접도. 1이면 액면 위.</summary>
        public readonly float MacroniumProximity;

        /// <summary>낫(선공 포식자)이 얼마나 붙었는가. 1이면 코앞.</summary>
        public readonly float ReaperProximity;

        /// <summary>맞은 직후의 잔향. 1에서 시작해 짧게 0으로 떨어진다.</summary>
        public readonly float HurtPulse;

        /// <summary>감마 조절 화면에서 사람이 정한 값(0~1, 0.5가 기본).</summary>
        public readonly float Gamma;

        /// <summary>
        /// 때린 것이 어느 쪽에 있었는가(도). 0이 정면, 오른쪽이 양수, 등 뒤가 ±180.
        /// 판단은 <see cref="Survive.Combat.HurtBearing"/>이 한다.
        ///
        /// <b>기본값이 0(정면)인 것은 뜻이 있다.</b> 추락·질식처럼 방향이 없는 피해는
        /// 좌우로 쏠리지 않아야 한다 — 없는 방향을 가리키면 신호가 거짓말이 된다.
        /// </summary>
        public readonly float HurtBearingDegrees;

        public PostFxState(bool lanternOn, bool inLitZone, float macroniumProximity,
                           float reaperProximity, float hurtPulse, float gamma,
                           float hurtBearingDegrees = 0f)
        {
            LanternOn = lanternOn;
            InLitZone = inLitZone;
            MacroniumProximity = Mathf.Clamp01(macroniumProximity);
            ReaperProximity = Mathf.Clamp01(reaperProximity);
            HurtPulse = Mathf.Clamp01(hurtPulse);
            Gamma = Mathf.Clamp01(gamma);
            HurtBearingDegrees = Mathf.Clamp(hurtBearingDegrees, -180f, 180f);
        }

        public static PostFxState Default => new PostFxState(false, false, 0f, 0f, 0f, GammaGrade.Neutral);
    }

    /// <summary>후처리 볼륨에 그대로 꽂히는 값들.</summary>
    public readonly struct PostFxLook
    {
        public readonly float Vignette;
        public readonly float FilmGrain;
        public readonly float ChromaticAberration;

        /// <summary>노출 보정(EV). 감마 조절 외에는 건드리지 않는다.</summary>
        public readonly float PostExposure;

        /// <summary>색 필터. 액면 근처에서만 자홍 쪽으로 아주 조금 기운다.</summary>
        public readonly Color ColorFilter;

        /// <summary>
        /// 비네트 중심을 화면 한가운데에서 얼마나 옮길 것인가(화면 비율).
        /// 평소에는 (0,0)이고, 맞은 직후에만 잠깐 쏠린다.
        ///
        /// <b>부호에 주의.</b> 비네트는 중심에서 <b>먼</b> 곳을 어둡게 하므로,
        /// 오른쪽을 어둡게 하려면 중심을 <b>왼쪽</b>으로 옮겨야 한다.
        /// 그 뒤집기는 <see cref="PostFxGrade.Evaluate"/> 안에서 이미 끝나 있다 —
        /// 여기 값은 볼륨에 그대로 더하면 되는 값이다.
        /// </summary>
        public readonly Vector2 VignetteCenterOffset;

        public PostFxLook(float vignette, float filmGrain, float chromaticAberration,
                          float postExposure, Color colorFilter,
                          Vector2 vignetteCenterOffset = default)
        {
            Vignette = vignette;
            FilmGrain = filmGrain;
            ChromaticAberration = chromaticAberration;
            PostExposure = postExposure;
            ColorFilter = colorFilter;
            VignetteCenterOffset = vignetteCenterOffset;
        }
    }

    /// <summary>
    /// 상태 → 후처리 값. 이 게임의 후처리 규칙 전부가 여기 한 함수에 있다.
    ///
    /// <b>대원칙: 어둠은 이미 있다.</b> 환경광이 0이라 광원이 없는 곳은 진짜로
    /// 검다. 후처리가 할 일의 절반은 그 검정을 <i>들어올리지 않는 것</i>이다.
    /// 그래서 여기서 나오는 값들은 전부 상한이 낮고, 검정을 밝히는 방향의
    /// 노브(<see cref="PostFxLook.PostExposure"/>)는 사람이 감마 화면에서
    /// 직접 정한 것 말고는 움직이지 않는다.
    ///
    /// <b>쓰지 않는 것.</b> DoF·모션블러는 이 게임에 없다(상세기획서 §7.4).
    /// 좁은 통로를 손전등 하나로 더듬는 게임에서 초점이 흐려지면 그것은
    /// 연출이 아니라 고장으로 읽힌다. (컷신 전용 DoF는 나중에 별도 볼륨으로
    /// 얹을 여지만 남긴다 — 상시 볼륨에는 절대 넣지 않는다.)
    /// 색수차도 <b>상시로는 쓰지 않는다</b> — 액면과 피격 순간에만 잠깐 걸린다.
    /// </summary>
    public static class PostFxGrade
    {
        // ── 비네트 ────────────────────────────────────────────
        // 상시 보조 수준으로만 쓴다. 처음에는 랜턴이 꺼지면 크게 조이려 했는데,
        // 어차피 화면이 검은 게임에서 비네트까지 조이면 답답하기만 하고
        // "빛이 없다"는 정보는 이미 화면 전체가 말하고 있다.
        public const float VignetteBase = 0.26f;
        public const float VignetteDarkBonus = 0.06f;

        // ── 피격 방향 신호 ────────────────────────────────────
        // 랜턴이 앞으로 밀린 뒤로 사람은 대개 <b>안 보이는 데서</b> 맞는다.
        // 방향을 말해 주지 않으면 그것은 난이도가 아니라 억울함이 된다(기획서 §9).
        //
        // 신호는 <b>가리는 쪽으로만</b> 낸다. 이 게임에서 어둠은 지켜야 할 것이므로
        // 무엇을 밝혀서 알리는 길은 애초에 없고, 비네트를 한쪽으로 쏠리게 하는 것은
        // 검은 화면에서 유일하게 공짜인 표현이다.

        /// <summary>정면에서 맞았을 때 더해지는 비네트. 이미 봤으니 작게.</summary>
        public const float VignetteHurtFront = 0.08f;

        /// <summary>등 뒤에서 맞았을 때까지 더해지는 몫. 못 본 것일수록 크게 말한다.</summary>
        public const float VignetteHurtRear = 0.20f;

        /// <summary>비네트 중심이 옆으로 쏠리는 폭(화면 비율). 크게 두면 화면이 기운다.</summary>
        public const float VignetteHurtShift = 0.22f;

        // ── 필름 그레인 ───────────────────────────────────────
        // 저폴리 면이 넓게 깔릴 때 생기는 밴딩을 부순다. 세게 넣으면
        // 검정이 회색으로 들리므로 아주 약하게.
        public const float GrainBase = 0.11f;
        public const float GrainReaperBonus = 0.09f;

        // ── 색수차 ────────────────────────────────────────────
        public const float ChromaticMacronium = 0.16f;
        public const float ChromaticHurt = 0.28f;

        // ── 액면 색 기울기 ────────────────────────────────────
        public const float MacroniumTintMax = 0.14f;

        /// <summary>지금 상태에서 볼륨에 넣을 값들.</summary>
        public static PostFxLook Evaluate(in PostFxState s)
        {
            // 랜턴도 꺼져 있고 밝은 구역도 아니면, 아주 조금만 더 조인다.
            bool dark = !s.LanternOn && !s.InLitZone;
            float vignette = VignetteBase + (dark ? VignetteDarkBonus : 0f);

            // 맞은 방향. 뒤일수록 세게 조이고, 짧게 도는 쪽 가장자리를 어둡게 한다.
            float behind = Survive.Combat.HurtBearing.Behindness(s.HurtBearingDegrees);
            vignette += s.HurtPulse * Mathf.Lerp(VignetteHurtFront, VignetteHurtRear, behind);

            // 비네트는 중심에서 먼 쪽을 어둡게 하므로 부호를 뒤집어 옮긴다 —
            // 오른쪽에서 맞았으면 중심이 왼쪽으로 가야 오른쪽이 어두워진다.
            float side = Survive.Combat.HurtBearing.ScreenSide(s.HurtBearingDegrees);
            var center = new Vector2(-side * VignetteHurtShift * s.HurtPulse, 0f);

            float grain = GrainBase + GrainReaperBonus * s.ReaperProximity;

            // 둘 다 걸릴 수 있지만 더하지 않는다. 겹쳐서 화면이 무너지느니
            // 강한 쪽 하나만 보이는 편이 읽기 쉽다.
            float chromatic = Mathf.Max(ChromaticMacronium * s.MacroniumProximity,
                                        ChromaticHurt * s.HurtPulse);

            var filter = Color.Lerp(Color.white, ArtPalette.MacroniumHighlight,
                                    MacroniumTintMax * s.MacroniumProximity);

            return new PostFxLook(vignette, grain, chromatic,
                                  GammaGrade.Exposure(s.Gamma), filter, center);
        }
    }
}
