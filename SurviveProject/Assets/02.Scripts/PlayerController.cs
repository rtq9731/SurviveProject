using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] Transform camTrm = null;
    [Header("Control Settings")]
    [SerializeField] float _mouseSensitivity = 100.0f;
    [SerializeField] float _playerSpeed = 5.0f;
    [SerializeField] float _runningSpeed = 7.0f;
    [SerializeField] float _gravityScale = 1f;
    [SerializeField] float _shakePower = 5f;

    float _horizontalAngle = 0f;
    float _verticalAngle = 0f;

    [SerializeField] CameraShake camShake = null;

    CharacterController _characterController = null;
    Animator _charAnim = null;

    int hashDirHorizontal = 0;
    int hashDirVertical = 0;
    int hashIsMove = 0;
    int hashSpeed = 0;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _charAnim = GetComponent<Animator>();
        hashDirHorizontal = Animator.StringToHash("HorizontalMove");
        hashDirVertical = Animator.StringToHash("VerticalMove");
        hashSpeed = Animator.StringToHash("Speed");
        hashIsMove = Animator.StringToHash("isMove");
    }

    private void Update()
    {
        UpdateCam();
        CalcPlayerMove();
    }

    public void CalcPlayerMove()
    {
        Vector3 move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxisRaw("Vertical"));

        if (move.sqrMagnitude > 1.0f)
        {
            move.Normalize();
        }

        if (move.sqrMagnitude < 0.01f)
        {
            if (camShake.IsShaking())
                camShake.StopShake();
        }
        else
        {
            if (!camShake.IsShaking())
            {
                camShake.StartShake(EasingFunction.Ease.Linear, true, 1f, _shakePower);
            }
        }

        _charAnim.SetFloat(hashSpeed, (Input.GetButton("Run") ? _runningSpeed : _playerSpeed) / _playerSpeed);

        move *= (Input.GetButton("Run") ? _runningSpeed : _playerSpeed) * Time.deltaTime;

        move = new Vector3(move.x, -9.8f * _gravityScale * Time.deltaTime, move.z);

        _charAnim.SetFloat(hashDirHorizontal, move.normalized.x);
        _charAnim.SetFloat(hashDirVertical, move.normalized.z);

        _charAnim.SetBool(hashIsMove, new Vector2(Input.GetAxis("Horizontal"), Input.GetAxisRaw("Vertical")).magnitude >= 0.1f);

        move = transform.TransformDirection(move);
        _characterController.Move(move);
    }

    public void UpdateCam()
    {

        if (_horizontalAngle > 360) _horizontalAngle -= 360.0f;
        if (_horizontalAngle < 0) _horizontalAngle += 360.0f;

        float turnPlayer = Input.GetAxis("Mouse X") * (_mouseSensitivity / 5f);
        _horizontalAngle += turnPlayer;

        Vector3 currentAngles = transform.localEulerAngles;
        currentAngles.y = _horizontalAngle;
        transform.localEulerAngles = currentAngles;

        var turnCam = -Input.GetAxis("Mouse Y");
        turnCam *= (_mouseSensitivity / 5f);
        _verticalAngle = Mathf.Clamp(turnCam + _verticalAngle, -89.0f, 89.0f);
        currentAngles = camTrm.localEulerAngles;
        currentAngles.x = _verticalAngle;
        camTrm.localEulerAngles = currentAngles;
    }
}
