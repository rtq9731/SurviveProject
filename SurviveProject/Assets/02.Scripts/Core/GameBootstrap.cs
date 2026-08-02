using UnityEngine;
using Survive.Core;
using Survive.InputSystem;

namespace Survive.Core
{
    /// <summary>
    /// 플레이 가능한 씬의 진입점. 서비스를 등록하고 입력을 켠다.
    /// 실행 순서를 앞당겨 다른 컴포넌트의 Awake보다 먼저 돈다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;

        void Awake()
        {
            GameServices.Clear();

            if (input != null)
            {
                GameServices.Register(input);
                input.EnableGameplayInput();
            }
            else
            {
                Debug.LogError("[GameBootstrap] InputReader가 지정되지 않았습니다.", this);
            }
        }

        void OnDestroy() => GameServices.Clear();
    }
}
