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

        public PostFxState(bool lanternOn, bool inLitZone, float macroniumProximity,
                           float reaperProximity, float hurtPulse, float gamma)
        {
            LanternOn = lanternOn;
            InLitZone = inLitZone;
            MacroniumProximity = Mathf.Clamp01(macroniumProximity);
            ReaperProximity = Mathf.Clamp01(reaperProximity);
            HurtPulse = Mathf.Clamp01(hurtPulse);
            Gamma = Mathf.Clamp01(gamma);
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

        public PostFxLook(float vignette, float filmGrain, float chromaticAberration,
                          float postExposure, Color colorFilter)
        {
            Vignette = vignette;
            FilmGrain = filmGrain;
            ChromaticAberration = chromaticAberration;
            PostExposure = postExposure;
            ColorFilter = colorFilter;
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

            float grain = GrainBase + GrainReaperBonus * s.ReaperProximity;

            // 둘 다 걸릴 수 있지만 더하지 않는다. 겹쳐서 화면이 무너지느니
            // 강한 쪽 하나만 보이는 편이 읽기 쉽다.
            float chromatic = Mathf.Max(ChromaticMacronium * s.MacroniumProximity,
                                        ChromaticHurt * s.HurtPulse);

            var filter = Color.Lerp(Color.white, ArtPalette.MacroniumHighlight,
                                    MacroniumTintMax * s.MacroniumProximity);

            return new PostFxLook(vignette, grain, chromatic,
                                  GammaGrade.Exposure(s.Gamma), filter);
        }
    }
}
