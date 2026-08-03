using UnityEngine;
using DG.Tweening;
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
    /// </summary>
    [DisallowMultipleComponent]
    public class UnderwaterView : MonoBehaviour
    {
        [SerializeField] PlayerSwimming swimming;
        [SerializeField] Camera eye;

        [Header("물속")]
        [SerializeField] Color underwaterFog = new Color(0.10f, 0.26f, 0.33f);

        [Tooltip("작을수록 뿌옇다")]
        [SerializeField] float underwaterFogDensity = 0.028f;

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
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = underwaterFog;

            // 0에서 시작해 짧게 차오르면 "잠겼다"가 몸으로 읽힌다
            RenderSettings.fogDensity = 0f;
            DOTween.To(() => RenderSettings.fogDensity,
                       v => RenderSettings.fogDensity = v,
                       underwaterFogDensity, blendSeconds)
                   .SetId(this);

            if (eye != null)
            {
                eye.clearFlags = CameraClearFlags.SolidColor;
                eye.backgroundColor = underwaterFog;
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
