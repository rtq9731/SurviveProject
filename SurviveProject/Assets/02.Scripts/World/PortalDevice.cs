using System.Collections;
using System.Text;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Core;
using Survive.Items;
using Survive.Interaction;
using Survive.Localization;
using Survive.Player;

namespace Survive.World
{
    /// <summary>
    /// 강 양 끝의 외계 구조물. 요구 부품을 납품하면 다음 구역으로 내려간다.
    /// </summary>
    public class PortalDevice : MonoBehaviour, IInteractable
    {
        [Tooltip("비우면 번역 표의 World/portal_default를 쓴다")]
        [SerializeField] string displayName = "";
        [SerializeField] ItemStack[] requiredItems = new ItemStack[0];
        [SerializeField] SceneReferenceSO destination;

        [Header("피드백")]
        [Tooltip("기동 시 재생. 카메라 임펄스·발광·저음")]
        [SerializeField] MMF_Player activateFeedback;

        [SerializeField] float activateSeconds = 2.5f;

        bool _activated;

        /// <summary>목적지 없이 기동이 끝났을 때. 구간의 마지막 포탈이라는 뜻이다.</summary>
        public static event System.Action<PortalDevice> ChapterEnded;

        public bool IsActivated => _activated;

        /// <summary>
        /// 인스펙터에 적힌 이름이 이긴다. 강 이쪽과 저쪽의 장치를 다르게 부를 수
        /// 있어야 하기 때문이다. 비어 있을 때만 표의 기본 이름을 쓴다.
        /// </summary>
        string Name => string.IsNullOrEmpty(displayName)
            ? Loc.T("World", "portal_default")
            : displayName;

        public string InteractionPrompt
        {
            get
            {
                if (_activated) return "";
                if (requiredItems == null || requiredItems.Length == 0)
                    return Loc.F("Prompt", "portal_activate", Name);

                // 되풀이 목록이라 항목 틀과 구분자를 표에서 꺼내 이어 붙인다
                // (통짜 문장 규칙의 유일한 예외). 코드가 적어 넣는 말은 하나도 없다.
                var sb = new StringBuilder();
                string separator = Loc.T("UI", "list_separator");
                bool isFirst = true;
                foreach (var need in requiredItems)
                {
                    if (need?.item == null) continue;
                    if (!isFirst) sb.Append(separator);
                    sb.Append(Loc.F("Prompt", "portal_need_entry",
                                    DataText.Name(need.item), HeldCount(need.item.id), need.count));
                    isFirst = false;
                }

                // 요구 목록이 통째로 비어 있으면 빈 목록을 문장에 꽂지 않는다
                if (isFirst) return Loc.F("Prompt", "portal_activate", Name);

                return Loc.F("Prompt", "portal_activate_needs", Name, sb.ToString());
            }
        }

        int HeldCount(string id)
        {
            var inv = _lastPlayer?.Inventory?.Inventory;
            return inv != null ? inv.CountOf(id) : 0;
        }

        PlayerContext _lastPlayer;

        public bool CanInteract(PlayerContext player)
        {
            _lastPlayer = player;
            if (_activated || player?.Inventory?.Inventory == null) return false;

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
            if (_activated) return;
            _activated = true;

            if (requiredItems != null)
            {
                foreach (var need in requiredItems)
                {
                    if (need?.item == null) continue;
                    player.Inventory.Remove(need.item.id, need.count);
                }
            }

            // 진행도에 알린다. FlagOnInteract가 붙어 있었는데 아무도 호출하지 않아
            // 포탈을 기동해도 목표가 완료되지 않았다.
            GetComponent<Survive.Progression.FlagOnInteract>()?.Raise();

            activateFeedback?.PlayFeedbacks();
            StartCoroutine(TransitionTo());
        }

        IEnumerator TransitionTo()
        {
            yield return new WaitForSeconds(activateSeconds);

            // 목적지를 비워 두는 것은 오류가 아니다. 다음 구역이 아직 없는 구간의
            // 마지막 포탈이 그렇다 — 기동은 성립하고 챕터는 여기서 끝난다.
            // 경고를 띄우면 진짜 배선 실수와 구별이 안 된다.
            if (destination == null || string.IsNullOrEmpty(destination.sceneName))
            {
                Debug.Log("[PortalDevice] 다음 구역이 아직 없어 챕터를 여기서 마칩니다.", this);
                ChapterEnded?.Invoke(this);
                yield break;
            }

            if (GameServices.TryGet<SceneFlowService>(out var flow))
                yield return flow.LoadScene(destination, 1f);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(destination.sceneName);
        }
    }
}
