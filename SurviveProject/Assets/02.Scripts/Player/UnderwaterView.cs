using UnityEngine;
using DG.Tweening;
using Survive.Domain.Art;
using Survive.World;

namespace Survive.Player
{
    /// <summary>
    /// 물속에서 시야가 흐려진다.
    ///
    /// 머리가 물에 잠겼는데 화면이 뭍과 똑같으면 잠긴 줄을 모른다.
    /// 산소가 닳는 이유도 설명되지 않는다.
    ///
    /// 포스트 프로세싱 볼륨을 쓰지 않는 이유: 이 프로젝트에 볼륨 프로파일이 없고,
    /// 하나 만들면 URP 설정까지 손대야 한다. 안개는 씬 설정 하나로 끝나고
    /// 물속/뭍의 차이를 만드는 데 충분하다.
    ///
    /// <b>색과 밀도는 여기 없다.</b> <see cref="UnderwaterFog"/>가 랜턴 반경에서
    /// 역산한다 — 물속에서도 <b>반경 안은 보이고 반경 밖은 새까맣다</b>가
    /// 성립해야 하는데, 그 관계를 여기 직렬화 필드로 두면 씬의 사본이 규칙을
    /// 덮는다. 실제로 그랬다: 씬에 밀도 0.028과 자홍 <c>FogCliffs</c>가 박혀 있어
    /// 물속이 <b>77m까지 보이면서 수면 위보다 밝은</b> 상태였다. 화톳불 연료·
    /// 랜턴에서 세 번째로 겪는 같은 일이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UnderwaterView : MonoBehaviour
    {
        [SerializeField] PlayerSwimming swimming;
        [SerializeField] Camera eye;

        // ── 물속 ────────────────────────────────────────────────
        // 직렬화 필드가 아니다. 위 요약 참고.
        static Color UnderwaterFogColor => UnderwaterFog.Color;
        static float UnderwaterFogDensity => UnderwaterFog.Density;

        [Tooltip("잠기고 걷히는 데 걸리는 시간")]
        [SerializeField] float blendSeconds = 0.35f;

        bool _wasSubmerged;

        // 뭍의 원래 설정. 돌아갈 때 그대로 되돌린다.
        bool _landFog;
        Color _landFogColor;
        float _landFogDensity;
        FogMode _landFogMode;
        CameraClearFlags _landClear;
        Color _landBackground;

        void Awake()
        {
            if (swimming == null) swimming = GetComponentInParent<PlayerSwimming>();
            if (eye == null) eye = Camera.main;

            _landFog = RenderSettings.fog;
            _landFogColor = RenderSettings.fogColor;
            _landFogDensity = RenderSettings.fogDensity;
            _landFogMode = RenderSettings.fogMode;

            if (eye != null)
            {
                _landClear = eye.clearFlags;
                _landBackground = eye.backgroundColor;
            }
        }

        void OnDisable() => Restore();

        void Update()
        {
            if (swimming == null) return;

            bool submerged = swimming.IsHeadSubmerged;
            if (submerged == _wasSubmerged) return;

            _wasSubmerged = submerged;
            if (submerged) Enter();
            else Restore();
        }

        void Enter()
        {
            var color = UnderwaterFogColor;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;

            // 0이 아니라 뭍의 밀도에서 이어 올린다. 0에서 시작하면 잠긴 첫
            // 프레임에 안개가 통째로 걷혀 물속이 순간 환해진다 — 어둠 축과
            // 정확히 반대다. 이어 올리면 "닫힌다"만 남는다.
            //
            // 지금 깔려 있는 값을 읽는다. Awake에 잡아 둔 씬 기본값이 아니라 —
            // DepthFogService가 매 프레임 높이에 맞춰 갈아 끼우므로 그쪽이 실제 값이다.
            RenderSettings.fogDensity = Mathf.Min(RenderSettings.fogDensity, UnderwaterFogDensity);
            DOTween.To(() => RenderSettings.fogDensity,
                       v => RenderSettings.fogDensity = v,
                       UnderwaterFogDensity, blendSeconds)
                   .SetId(this);

            if (eye != null)
            {
                eye.clearFlags = CameraClearFlags.SolidColor;
                eye.backgroundColor = color;
            }
        }

        void Restore()
        {
            DOTween.Kill(this);

            RenderSettings.fog = _landFog;
            RenderSettings.fogColor = _landFogColor;
            RenderSettings.fogDensity = _landFogDensity;
            RenderSettings.fogMode = _landFogMode;

            if (eye != null)
            {
                eye.clearFlags = _landClear;
                eye.backgroundColor = _landBackground;
            }
        }
    }
}
