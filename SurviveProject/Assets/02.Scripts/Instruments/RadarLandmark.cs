using UnityEngine;

namespace Survive.Instruments
{
    /// <summary>
    /// 레이더 앞에 서는 것 하나. 섬이든 공동이든 낫이든 <b>이것 하나를 붙인다</b>.
    ///
    /// <b>왜 종류별 컴포넌트를 만들지 않는가.</b> "섬 표식"과 "낫 표식"을 나누면
    /// 레이더가 낫을 못 잡는 이유가 물성이 아니라 <b>표식을 안 붙였기 때문</b>이 된다.
    /// 그러면 언젠가 누군가 낫에 섬 표식을 붙이고, 그때 레이더는 아무 오류 없이
    /// 낫을 잡는다. 하나만 두면 그런 길이 없다 — 붙여도 크기와 속도 때문에 떨어진다.
    ///
    /// <b>낫에 이것을 붙이는 것은 이 라운드의 일이 아니다.</b> 낫 코드는 다른 갈래가
    /// 쥐고 있다. 여기서 하는 것은 "붙여도 안 잡힌다"를 규칙과 테스트로 못 박는 것까지다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RadarLandmark : MonoBehaviour, IRadarContactSource
    {
        [Tooltip("번역 표에서 이름을 찾는 열쇠. 화면에 그대로 찍지 않는다")]
        [SerializeField] string contactId;

        [Tooltip("결과 화면에 무엇이라고 적을 것인가. 판정에는 쓰이지 않는다")]
        [SerializeField] RadarContactKind kind = RadarContactKind.Unknown;

        [Tooltip("가장 긴 쪽의 길이(m). 해상도와 견주는 값이다")]
        [Min(0f)] [SerializeField] float sizeMeters = 100f;

        [Tooltip("지표 아래로 들어간 깊이(m). 물 위나 지표면이면 0")]
        [Min(0f)] [SerializeField] float depthMeters;

        [Tooltip("움직이는 것이면 켠다. 켜면 실제 이동에서 빠르기를 잰다")]
        [SerializeField] bool tracksMotion;

        [Tooltip("움직이지 않는 것으로 둘 때 쓰는 빠르기(m/s)")]
        [Min(0f)] [SerializeField] float fixedSpeedMps;

        Vector3 _lastPosition;
        float _speed;

        public string ContactId
        {
            get => contactId;
            set => contactId = value;
        }

        public RadarContactKind ContactKind
        {
            get => kind;
            set => kind = value;
        }

        public Vector3 ContactPosition => transform.position;

        public float ContactSizeMeters
        {
            get => sizeMeters;
            set => sizeMeters = Mathf.Max(0f, value);
        }

        public float ContactDepthMeters
        {
            get => depthMeters;
            set => depthMeters = Mathf.Max(0f, value);
        }

        public float ContactSpeedMps => tracksMotion ? _speed : fixedSpeedMps;

        /// <summary>붙박이로 둘 때의 빠르기. 검증 하네스가 세운다.</summary>
        public float FixedSpeedMps
        {
            get => fixedSpeedMps;
            set => fixedSpeedMps = Mathf.Max(0f, value);
        }

        void OnEnable()
        {
            _lastPosition = transform.position;
            _speed = 0f;
            RadarContactRegistry.Register(this);
        }

        void OnDisable() => RadarContactRegistry.Unregister(this);

        void Update()
        {
            if (!tracksMotion) return;
            if (Time.deltaTime <= 0f) return;

            var now = transform.position;
            _speed = Vector3.Distance(now, _lastPosition) / Time.deltaTime;
            _lastPosition = now;
        }
    }
}
