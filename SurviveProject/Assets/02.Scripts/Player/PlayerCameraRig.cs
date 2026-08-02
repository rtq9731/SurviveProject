using UnityEngine;
using Survive.InputSystem;

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
        bool _잠김;
        Vector2 _시점 = Vector2.zero;

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
            if (input != null) input.LookEvent += 시점입력;
        }

        void OnDisable()
        {
            if (input != null) input.LookEvent -= 시점입력;
        }

        void 시점입력(Vector2 v) => _시점 = v;

        public void SetLookLocked(bool locked)
        {
            _잠김 = locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
            if (locked) _시점 = Vector2.zero;
        }

        void LateUpdate()
        {
            if (_잠김) return;

            _yaw += _시점.x * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - _시점.y * mouseSensitivity, minPitch, maxPitch);

            var 몸각도 = transform.localEulerAngles;
            몸각도.y = _yaw;
            transform.localEulerAngles = 몸각도;

            if (cameraTransform != null)
            {
                var 카메라각도 = cameraTransform.localEulerAngles;
                카메라각도.x = _pitch;
                cameraTransform.localEulerAngles = 카메라각도;
            }
        }
    }
}
