using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Survive.Core;

namespace Survive.UI
{
    /// <summary>
    /// 화면 암전. 씬이 바뀌는 순간을 가려 준다.
    ///
    /// 씬 로드는 한 프레임을 통째로 멈춰 세우기 때문에, 가리지 않으면
    /// 화면이 뚝 끊긴 것처럼 보인다. 프롤로그에서 챕터 1로 넘어가는 지점이
    /// 이 게임에서 가장 먼저 만나는 전환이라 특히 눈에 띈다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image image;

        [Tooltip("씬을 시작할 때 검은 화면에서 밝아진다")]
        [SerializeField] bool fadeInOnStart = true;

        [SerializeField] float startFadeSeconds = 0.8f;

        Tween _tween;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (image == null) image = GetComponent<Image>();

            // 페이드 중에도 클릭이 통과해야 한다. 전체 화면을 덮기 때문이다.
            if (image != null) image.raycastTarget = false;
            if (group != null) group.blocksRaycasts = false;
        }

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<ScreenFader>();
        void OnDestroy() => _tween?.Kill();

        IEnumerator Start()
        {
            if (!fadeInOnStart) yield break;
            if (group != null) group.alpha = 1f;
            yield return FadeIn(startFadeSeconds);
        }

        public IEnumerator FadeOut(float seconds) => FadeTo(1f, seconds);
        public IEnumerator FadeIn(float seconds) => FadeTo(0f, seconds);

        IEnumerator FadeTo(float alpha, float seconds)
        {
            if (group == null) yield break;

            _tween?.Kill();
            // 씬 전환 중에는 timeScale을 믿을 수 없다. 실제 시간으로 돌린다.
            _tween = group.DOFade(alpha, Mathf.Max(0.01f, seconds)).SetUpdate(true);

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            group.alpha = alpha;
        }

        /// <summary>SceneFlowService에 넘길 콜백을 만든다.</summary>
        public SceneFlowService BuildFlow() => new SceneFlowService(FadeOut, FadeIn);
    }
}
