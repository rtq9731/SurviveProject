using System;
using UnityEngine;
using Survive.Items;

namespace Survive.Player
{
    /// <summary>
    /// 손 소켓(jointItemR) 아래 도구 오브젝트를 켜고 끈다.
    /// 도구 모델은 이미 씬에 붙어 있으므로 생성하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerToolHolder : MonoBehaviour
    {
        [Tooltip("Armature/…/Wrist_R/jointItemR")]
        [SerializeField] Transform handSocket;

        public ToolItemSO EquippedTool { get; private set; }
        public event Action<ToolItemSO> ToolChanged;

        void Awake()
        {
            if (handSocket != null) 전부끄기();
        }

        public void Equip(ToolItemSO tool)
        {
            if (handSocket == null)
            {
                Debug.LogError("[PlayerToolHolder] handSocket이 지정되지 않았습니다.", this);
                return;
            }

            전부끄기();
            EquippedTool = tool;

            if (tool != null && !string.IsNullOrEmpty(tool.socketChildName))
            {
                var 자식 = handSocket.Find(tool.socketChildName);
                if (자식 != null) 자식.gameObject.SetActive(true);
                else Debug.LogWarning(
                    $"[PlayerToolHolder] 손 소켓에 '{tool.socketChildName}' 자식이 없습니다.", this);
            }

            ToolChanged?.Invoke(EquippedTool);
        }

        public void Unequip() => Equip(null);

        void 전부끄기()
        {
            for (int i = 0; i < handSocket.childCount; i++)
                handSocket.GetChild(i).gameObject.SetActive(false);
        }
    }
}
