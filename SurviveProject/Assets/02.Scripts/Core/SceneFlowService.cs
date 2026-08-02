using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survive.Core
{
    /// <summary>씬 전환. 페이드는 ScreenFader에 위임한다.</summary>
    public class SceneFlowService
    {
        readonly Func<float, IEnumerator> _fadeOut;
        readonly Func<float, IEnumerator> _fadeIn;

        public SceneFlowService(Func<float, IEnumerator> fadeOut = null, Func<float, IEnumerator> fadeIn = null)
        {
            _fadeOut = fadeOut;
            _fadeIn = fadeIn;
        }

        public event Action<string> SceneLoaded;

        public IEnumerator LoadScene(SceneReferenceSO target, float fadeSeconds)
        {
            if (target == null || string.IsNullOrEmpty(target.sceneName))
            {
                Debug.LogError("[SceneFlowService] 대상 씬이 비어 있습니다.");
                yield break;
            }

            if (_fadeOut != null) yield return _fadeOut(fadeSeconds);

            var op = SceneManager.LoadSceneAsync(target.sceneName);
            while (op != null && !op.isDone) yield return null;

            SceneLoaded?.Invoke(target.sceneName);

            if (_fadeIn != null) yield return _fadeIn(fadeSeconds);
        }
    }
}
