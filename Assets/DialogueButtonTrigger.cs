using UnityEngine;

public class DialogueButtonTrigger : MonoBehaviour
{
    [SerializeField] private string _dialogueKey = "npc_liuming";

    public void OnClickStartDialogue()
    {
        UIManager.Instance.Open<TalkPanel>().Dialogue.StartDialogue(_dialogueKey);
    }
}