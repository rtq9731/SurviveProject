using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Survive.Core;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;
using Survive.Vitals;
using Survive.Building;

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

        [Tooltip("건설 미리보기가 왜 막혔는지 보여줄 줄. interactionPrompt와 같은 방식으로 쓴다")]
        [SerializeField] TMP_Text buildPlacementPrompt;

        BuildPlacer _placer;

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

            if (GameServices.TryGet<BuildPlacer>(out var placer))
            {
                _placer = placer;
                _placer.ResultChanged += RefreshBuildPrompt;
                _placer.SelectionChanged += OnBuildSelectionChanged;
                RefreshBuildPrompt(_placer.LastResult);
            }
            else Debug.LogWarning("[HUDController] BuildPlacer를 찾지 못했습니다.", this);
        }

        void OnDestroy()
        {
            if (_placer != null)
            {
                _placer.ResultChanged -= RefreshBuildPrompt;
                _placer.SelectionChanged -= OnBuildSelectionChanged;
            }
        }

        void RefreshPrompt(string prompt)
        {
            if (interactionPrompt == null) return;
            interactionPrompt.text = prompt ?? "";
            interactionPrompt.gameObject.SetActive(!string.IsNullOrEmpty(prompt));
        }

        /// <summary>배치 판정이 바뀔 때마다 사유를 띄운다. 건설 모드가 아니면 지운다.</summary>
        void RefreshBuildPrompt(PlacementResult result)
        {
            if (buildPlacementPrompt == null) return;

            // 건설 모드를 나가면 판정 값은 그대로 남아 있을 수 있으니
            // 여기서 다시 IsActive를 확인해야 메뉴를 닫아도 문구가 안 남는다.
            string text = (_placer != null && _placer.IsActive)
                ? PlacementCheckText.Describe(result)
                : "";

            buildPlacementPrompt.text = text;
            buildPlacementPrompt.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>건설 모드를 빠져나가면(선택 해제) 사유 문구도 즉시 지운다.</summary>
        void OnBuildSelectionChanged(BuildableSO selected)
        {
            if (selected == null && buildPlacementPrompt != null)
            {
                buildPlacementPrompt.text = "";
                buildPlacementPrompt.gameObject.SetActive(false);
            }
        }
    }
}
