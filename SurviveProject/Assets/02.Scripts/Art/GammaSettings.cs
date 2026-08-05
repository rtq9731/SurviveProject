using System;
using UnityEngine;
using Survive.Domain.Art;

namespace Survive.Art
{
    /// <summary>
    /// 사람이 맞춘 화면 밝기를 기억한다 (백로그 42).
    ///
    /// 값은 하나뿐이고 규칙은 <see cref="GammaGrade"/>에 있다. 여기가 하는 일은
    /// PlayerPrefs에 넣고 빼는 것과, "아직 한 번도 맞춘 적이 없다"를 구별하는 것뿐이다.
    ///
    /// <b>검증은 언제나 기본값으로 돈다.</b> E2E와 스크린샷 비교가 사람의 설정에
    /// 흔들리면 같은 빌드가 기계마다 다른 결과를 낸다. <see cref="ForceNeutral"/>이
    /// 켜져 있으면 저장된 값이 무엇이든 <see cref="GammaGrade.Neutral"/>을 돌려준다.
    /// </summary>
    public static class GammaSettings
    {
        public const string ValueKey = "survive.gamma.value";
        public const string CalibratedKey = "survive.gamma.calibrated";

        static float _value = GammaGrade.Neutral;
        static bool _loaded;

        /// <summary>바뀔 때마다 알린다. 후처리 서비스가 이것을 듣는다.</summary>
        public static event Action<float> Changed;

        /// <summary>
        /// 검증용 강제 기본값. 테스트 하네스가 켜고, 끄는 것도 하네스의 몫이다.
        /// </summary>
        public static bool ForceNeutral { get; set; }

        /// <summary>사람이 맞춘 값(0~1).</summary>
        public static float Value
        {
            get
            {
                if (ForceNeutral) return GammaGrade.Neutral;
                Load();
                return _value;
            }
            set
            {
                Load();
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(v, _value)) return;

                _value = v;
                PlayerPrefs.SetFloat(ValueKey, v);
                Changed?.Invoke(ForceNeutral ? GammaGrade.Neutral : v);
            }
        }

        /// <summary>한 번이라도 조절 화면을 끝냈는가. 첫 실행 판정에 쓴다.</summary>
        public static bool Calibrated
        {
            get => PlayerPrefs.GetInt(CalibratedKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(CalibratedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>지금 값에 해당하는 노출 보정(EV).</summary>
        public static float Exposure => GammaGrade.Exposure(Value);

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _value = Mathf.Clamp01(PlayerPrefs.GetFloat(ValueKey, GammaGrade.Neutral));
        }

        /// <summary>기억한 것을 지운다. 테스트가 첫 실행 상태를 만들 때 쓴다.</summary>
        public static void Reset()
        {
            _loaded = true;
            _value = GammaGrade.Neutral;
            PlayerPrefs.DeleteKey(ValueKey);
            PlayerPrefs.DeleteKey(CalibratedKey);
            Changed?.Invoke(GammaGrade.Neutral);
        }
    }
}
