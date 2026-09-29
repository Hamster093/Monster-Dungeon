using UnityEngine;

public class DialogueButtonTrigger : MonoBehaviour
{
    [Header("对话控制器")]
    [SerializeField] private DialogueController _dialogueController;

    [Header("要播放的对话 ID (对应 Addressables 里的名字)")]
    [SerializeField] private string _dialogueKey = "npc_liuming";

    /// <summary>
    /// 绑定到 UI Button 的 OnClick 事件上
    /// </summary>
    public void OnClickStartDialogue()
    {
        if (_dialogueController == null)
        {
            Debug.LogError("[DialogueButtonTrigger] DialogueController 未赋值！");
            return;
        }

        // 可选：防止对话正在进行时重复点击触发
        // if (_dialogueController.IsDialogueRunning) return; 

        _dialogueController.StartDialogue(_dialogueKey);
    }
}