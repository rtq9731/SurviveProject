using System.Collections;
using UnityEngine;
using Survive.Core;
using Survive.UI;
using Survive.Vitals;

namespace Survive.World
{
    /// <summary>
    /// 밟으면 다음 씬으로 넘어가는 트리거. 프롤로그의 동굴 입구가 이것이다.
    ///
    /// 자막이 재생 중이면 끝날 때까지 기다린다 — 대사가 잘린 채로 화면이
    /// 넘어가면 무슨 말이었는지 알 수 없다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SceneTransitionTrigger : MonoBehaviour
    {
        [SerializeField] SceneReferenceSO destination;
        [SerializeField] float fadeSeconds = 1.0f;

        [Tooltip("자막이 끝날 때까지 최대 몇 초까지 기다릴지")]
        [SerializeField] float maxWaitForSubtitle = 20f;

        bool _fired;

        /// <summary>전환이 시작됐는지. E2E가 확인한다.</summary>
        public bool Triggered => _fired;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_fired) return;
            if (other.GetComponentInParent<PlayerVitals>() == null) return;

            _fired = true;
            StartCoroutine(Go());
        }

        IEnumerator Go()
        {
            if (destination == null || string.IsNullOrEmpty(destination.sceneName))
            {
                Debug.LogError("[SceneTransitionTrigger] 대상 씬이 비어 있습니다.", this);
                yield break;
            }

            // 대사가 끝나기를 기다린다
            var director = Object.FindAnyObjectByType<Survive.Narrative.SequenceDirector>(
                FindObjectsInactive.Exclude);
            float waited = 0f;
            while (director != null && director.IsPlaying && waited < maxWaitForSubtitle)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            var fader = Object.FindAnyObjectByType<ScreenFader>(FindObjectsInactive.Exclude);
            var flow = fader != null ? fader.BuildFlow() : new SceneFlowService();

            yield return flow.LoadScene(destination, fadeSeconds);
        }
    }
}
