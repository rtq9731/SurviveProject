using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    [SerializeField] Text textValue = null;

    [SerializeField] float lerpTime = 0.5f;

    Coroutine cor = null;

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

        if (cor != null)
            StopCoroutine(cor);

        cor = StartCoroutine(UpdateImage());
    }

    private void Update()
    {
        RefreshColor();
    }

    private IEnumerator UpdateImage()
    {
        float timer = 0f;

        while (lerpTime >= timer)
        {
            _fillImage.fillAmount = Mathf.Lerp(_fillImage.fillAmount, _targetValue / maxValue, timer / lerpTime);
            timer += Time.deltaTime;
            yield return null;
        }

        timer = 0f;

        while (lerpTime >= timer)
        {
            _fillLookImage.fillAmount = Mathf.Lerp(_fillLookImage.fillAmount, _targetValue / maxValue, timer / lerpTime);
            timer += Time.deltaTime;
            yield return null;
        }
        
        textValue.text = Mathf.RoundToInt(_targetValue).ToString();

        yield return null;
    }

    private void RefreshColor()
    {
        Color lerpColor = Color.Lerp(zeroColor, fullColor, _fillImage.fillAmount);
        _fillImage.color = lerpColor;
        lerpColor.a = 0.2f;
        _fillLookImage.color = lerpColor;
    }

}
