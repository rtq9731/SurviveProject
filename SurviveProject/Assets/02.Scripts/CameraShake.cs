using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// Cinemachine 3.x에서 네임스페이스가 Cinemachine -> Unity.Cinemachine 으로 바뀌었다.
// CinemachineVirtualCamera는 Unity.Cinemachine 아래에 [Obsolete] 호환 클래스로 남아 있어
// 씬의 CM vcam1이 그대로 동작한다. 신규 카메라 연출은 CinemachineCamera + Impulse를 쓴다.
using Unity.Cinemachine;

public class CameraShake : MonoBehaviour
{
    CinemachineVirtualCamera _vCam = null;
    Coroutine _shakeCor = null;

    bool _loop = true;
    bool _isShaking = false;

    private void Awake()
    {
        _vCam = GetComponent<CinemachineVirtualCamera>();
    }

    public void StartShake(EasingFunction.Ease easing, bool isLoop, float loopSec = 1f, float shakePower = 1f)
    {
        _loop = isLoop;

        if (_shakeCor != null)
            StopCoroutine(_shakeCor);

        _shakeCor = StartCoroutine(Shake(easing, loopSec, shakePower));
    }

    public bool IsShaking()
    {
        return _isShaking;
    }

    public void StopShake()
    {
        _loop = false;
    }

    private IEnumerator Shake(EasingFunction.Ease easing, float loopSec = 1f, float shakePower = 1f)
    {
        EasingFunction.Function easingFunc = EasingFunction.GetEasingFunction(easing);
        _isShaking = true;
        float timer = 0f;
        Vector3 curAngle = Vector3.zero;
        float curValue = 0f;

        while(_loop)
        {
            while (timer <= loopSec / 2)
            {
                timer += Time.deltaTime;
                curValue = easingFunc(0, loopSec / 2, timer) * shakePower;
                curAngle = _vCam.transform.localRotation.eulerAngles;
                curAngle.x += curValue;
                _vCam.transform.localRotation = Quaternion.Euler(curAngle);
                yield return null;
            }

            while (timer >= 0)
            {
                timer -= Time.deltaTime;
                curValue = easingFunc(0, loopSec / 2, timer) * shakePower;
                curAngle = _vCam.transform.localRotation.eulerAngles;
                curAngle.x += curValue;
                _vCam.transform.localRotation = Quaternion.Euler(curAngle);
                yield return null;
            }
        }
        _isShaking = false;

        yield return null;
    }
}
