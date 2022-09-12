using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CameraShake : MonoBehaviour
{
    CinemachineVirtualCamera _vCam = null;

    bool _loop = true;

    private void Awake()
    {
        _vCam = GetComponent<CinemachineVirtualCamera>();
    }

    private void Start()
    {
        StartCoroutine(Shake(EasingFunction.Ease.Linear));
    }

    private IEnumerator Shake(EasingFunction.Ease easing, float loopSec = 1f, float shakePower = 1f)
    {
        EasingFunction.Function easingFunc = EasingFunction.GetEasingFunction(easing);

        float timer = 0f;
        Vector3 curAngle = Vector3.zero;
        float curValue = 0f;

        while(_loop)
        {
            while (timer <= loopSec)
            {
                timer += Time.deltaTime;
                curValue = easingFunc(0, loopSec, timer);
                curAngle = _vCam.transform.localRotation.eulerAngles;
                curAngle.x += curValue;
                _vCam.transform.localRotation = Quaternion.Euler(curAngle);
                yield return null;
            }

            while (timer >= 0)
            {
                timer -= Time.deltaTime;
                curValue = easingFunc(0, loopSec, timer);
                curAngle = _vCam.transform.localRotation.eulerAngles;
                curAngle.x -= curValue;
                _vCam.transform.localRotation = Quaternion.Euler(curAngle);
                yield return null;
            }
        }

        yield return null;
    }
}
