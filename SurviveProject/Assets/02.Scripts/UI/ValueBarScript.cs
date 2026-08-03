using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ValueBarScript : MonoBehaviour
{
    [SerializeField] Color fullColor;
    [SerializeField] Color zeroColor;

    [SerializeField] float initValue = 100f;
    [SerializeField] float maxValue = 100f;
    [SerializeField] float minValue = 0f;

    [SerializeField] Image _fillImage = null;
    [SerializeField] Image _fillLookImage = null;

    [SerializeField] TMP_Text textValue = null;

    [SerializeField] float lerpTime = 0.5f;

    float _targetValue = 100f;

    private void Start()
    {
        _targetValue = initValue;

        _fillImage.fillAmount = _targetValue / maxValue;
        _fillLookImage.fillAmount = _targetValue / maxValue;
        textValue.text = Mathf.RoundToInt(_targetValue).ToString();

        RefreshColor();
    }

    public void ResetValue()
    {
        _targetValue = initValue;
        _fillImage.fillAmount = 0;
        _fillLookImage.fillAmount = 0;
        textValue.text = Mathf.RoundToInt(_targetValue).ToString();
        RefreshColor();
    }

    public void RefreshMaxValue(float value)
    {
        maxValue = value;
    }

    public void SetUpdateValue(float value)
    {
        _targetValue += value;
        _targetValue = Mathf.Clamp(_targetValue, minValue, maxValue);
        textValue.text = Mathf.RoundToInt(_targetValue).ToString();
    }

    /// <summary>
    /// 절대값으로 설정한다. SetUpdateValue는 증감분을 받으므로
    /// Vital 같은 절대값 소스를 바인딩할 때 쓰기 불편해 추가했다.
    /// </summary>
    public void SetValueDirect(float value)
    {
        _targetValue = Mathf.Clamp(value, minValue, maxValue);
        textValue.text = Mathf.RoundToInt(_targetValue).ToString();
    }

    /// <summary>현재 목표값. 표시 갱신은 코루틴이 따라간다.</summary>
    public float TargetValue => _targetValue;

    private void Update()
    {
        FollowTarget();
        RefreshColor();
    }

    /// <summary>
    /// 채워진 양이 목표값을 부드럽게 따라간다.
    ///
    /// 전에는 값이 바뀔 때마다 코루틴을 죽이고 다시 시작했다. 산소처럼 매 프레임
    /// 변하는 값에서는 타이머가 늘 0으로 리셋되어 Lerp의 t가 0에 머물렀고,
    /// 결국 바가 제자리에 멈춘 채 숫자만 움직였다.
    /// 시간을 재는 대신 매 프레임 목표 쪽으로 조금씩 당긴다.
    /// </summary>
    private void FollowTarget()
    {
        if (maxValue <= 0f) return;

        float goal = Mathf.Clamp01(_targetValue / maxValue);
        // lerpTime 안에 대부분 따라잡히는 감쇠율
        float k = 1f - Mathf.Exp(-Time.deltaTime * (5f / Mathf.Max(0.05f, lerpTime)));

        if (_fillImage != null)
        {
            _fillImage.fillAmount = Mathf.Abs(_fillImage.fillAmount - goal) < 0.001f
                ? goal
                : Mathf.Lerp(_fillImage.fillAmount, goal, k);
        }

        // 뒤쪽 잔상은 조금 늦게 따라와야 변화가 눈에 보인다
        if (_fillLookImage != null)
        {
            _fillLookImage.fillAmount = Mathf.Abs(_fillLookImage.fillAmount - goal) < 0.001f
                ? goal
                : Mathf.Lerp(_fillLookImage.fillAmount, goal, k * 0.35f);
        }
    }

    private void RefreshColor()
    {
        Color lerpColor = Color.Lerp(zeroColor, fullColor, _fillImage.fillAmount);
        _fillImage.color = lerpColor;
        lerpColor.a = 0.2f;
        _fillLookImage.color = lerpColor;
    }

}
