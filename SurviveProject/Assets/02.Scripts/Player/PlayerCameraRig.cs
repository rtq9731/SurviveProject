using UnityEngine;
using Survive.Input;

namespace Survive.Player
{
    [DisallowMultipleComponent]
    public class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] Transform cameraTransform;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float minPitch = -89f;
        [SerializeField] float maxPitch = 89f;

        float _yaw;
        float _pitch;
        bool _locked;
        Vector2 _lookInput = Vector2.zero;

        public Transform CameraTransform => cameraTransform;

        void Awake()
        {
            _yaw = transform.localEulerAngles.y;
            if (cameraTransform != null) _pitch = cameraTransform.localEulerAngles.x;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnEnable()
        {
            if (input != null) input.LookEvent += OnLookInput;
        }

        void OnDisable()
        {
            if (input != null) input.LookEvent -= OnLookInput;
        }

        void OnLookInput(Vector2 v) => _lookInput = v;

        public void SetLookLocked(bool locked)
        {
            _locked = locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
            if (locked) _lookInput = Vector2.zero;
        }

        /// <summary>
        /// 시점을 직접 지정한다.
        ///
        /// transform.rotation을 밖에서 돌려도 LateUpdate가 _yaw로 덮어쓰기 때문에
        /// 반드시 이 경로로 바꿔야 한다. 컷신과 E2E 검증이 쓴다.
        /// </summary>
        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        /// <summary>지정한 월드 좌표를 바라본다.</summary>
        public void LookAt(Vector3 worldPosition)
        {
            Vector3 from = cameraTransform != null ? cameraTransform.position : transform.position;
            Vector3 d = worldPosition - from;
            if (d.sqrMagnitude < 0.0001f) return;

            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            SetLook(yaw, pitch);
        }

        void LateUpdate()
        {
            if (_locked) return;

            _yaw += _lookInput.x * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - _lookInput.y * mouseSensitivity, minPitch, maxPitch);

            var bodyAngles = transform.localEulerAngles;
            bodyAngles.y = _yaw;
            transform.localEulerAngles = bodyAngles;

            if (cameraTransform != null)
            {
                var camAngles = cameraTransform.localEulerAngles;
                camAngles.x = _pitch;
                cameraTransform.localEulerAngles = camAngles;
            }
        }
    }
}
