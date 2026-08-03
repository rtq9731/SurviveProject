using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Survive.Core;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;
using Survive.Vitals;

namespace Survive.UI
{
    /// <summary>
    /// HUD의 각 뷰를 플레이어에 연결한다.
    /// 플레이어는 GameServices에 등록되므로 씬 참조를 두지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HUDController : MonoBehaviour
    {
        [SerializeField] VitalBarView healthBar;
        [SerializeField] VitalBarView oxygenBar;
        [SerializeField] ScrapCounterView scrapCounter;
        [SerializeField] TMP_Text interactionPrompt;

        void Start() => StartCoroutine(BindWhenReady());

        IEnumerator BindWhenReady()
        {
            // 플레이어 컴포넌트들이 OnEnable에서 자신을 등록하므로 한 프레임 기다린다.
            yield return null;

            if (GameServices.TryGet<PlayerVitals>(out var vitals))
            {
                healthBar?.Bind(vitals);
                oxygenBar?.Bind(vitals);
            }
            else Debug.LogWarning("[HUDController] PlayerVitals를 찾지 못했습니다.", this);

            if (GameServices.TryGet<PlayerInventory>(out var inventory))
                scrapCounter?.Bind(inventory);
            else Debug.LogWarning("[HUDController] PlayerInventory를 찾지 못했습니다.", this);

            var interactor = UnityEngine.Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Exclude);
            if (interactor != null)
            {
                interactor.PromptChanged += RefreshPrompt;
                RefreshPrompt(null);
            }
        }

        void RefreshPrompt(string prompt)
        {
            if (interactionPrompt == null) return;
            interactionPrompt.text = prompt ?? "";
            interactionPrompt.gameObject.SetActive(!string.IsNullOrEmpty(prompt));
        }
    }
}
