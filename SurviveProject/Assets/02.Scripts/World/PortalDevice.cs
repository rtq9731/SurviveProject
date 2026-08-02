using System.Collections;
using System.Text;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Core;
using Survive.Items;
using Survive.Interaction;
using Survive.Player;

namespace Survive.World
{
    /// <summary>
    /// 강 양 끝의 외계 구조물. 요구 부품을 납품하면 다음 구역으로 내려간다.
    /// </summary>
    public class PortalDevice : MonoBehaviour, IInteractable
    {
        [SerializeField] string displayName = "외계 구조물";
        [SerializeField] ItemStack[] requiredItems = new ItemStack[0];
        [SerializeField] SceneReferenceSO destination;

        [Header("피드백")]
        [Tooltip("기동 시 재생. 카메라 임펄스·발광·저음")]
        [SerializeField] MMF_Player activateFeedback;

        [SerializeField] float activateSeconds = 2.5f;

        bool _기동됨;

        public string InteractionPrompt
        {
            get
            {
                if (_기동됨) return "";
                if (requiredItems == null || requiredItems.Length == 0)
                    return $"[E] {displayName} 기동";

                var sb = new StringBuilder();
                sb.Append($"[E] {displayName} 기동 — ");
                bool 첫번째 = true;
                foreach (var need in requiredItems)
                {
                    if (need?.item == null) continue;
                    if (!첫번째) sb.Append(", ");
                    sb.Append($"{need.item.displayName} {보유량(need.item.id)}/{need.count}");
                    첫번째 = false;
                }
                return sb.ToString();
            }
        }

        int 보유량(string id)
        {
            var inv = _마지막플레이어?.Inventory?.Inventory;
            return inv != null ? inv.CountOf(id) : 0;
        }

        PlayerContext _마지막플레이어;

        public bool CanInteract(PlayerContext player)
        {
            _마지막플레이어 = player;
            if (_기동됨 || player?.Inventory?.Inventory == null) return false;

            if (requiredItems != null)
            {
                foreach (var need in requiredItems)
                {
                    if (need?.item == null) continue;
                    if (!player.Inventory.Inventory.Has(need.item.id, need.count)) return false;
                }
            }
            return true;
        }

        public void Interact(PlayerContext player)
        {
            if (_기동됨) return;
            _기동됨 = true;

            if (requiredItems != null)
            {
                foreach (var need in requiredItems)
                {
                    if (need?.item == null) continue;
                    player.Inventory.Remove(need.item.id, need.count);
                }
            }

            activateFeedback?.PlayFeedbacks();
            StartCoroutine(전환());
        }

        IEnumerator 전환()
        {
            yield return new WaitForSeconds(activateSeconds);

            if (destination == null || string.IsNullOrEmpty(destination.sceneName))
            {
                Debug.LogWarning("[PortalDevice] destination이 비어 있어 씬을 전환하지 않습니다.", this);
                yield break;
            }

            if (GameServices.TryGet<SceneFlowService>(out var flow))
                yield return flow.LoadScene(destination, 1f);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(destination.sceneName);
        }
    }
}
