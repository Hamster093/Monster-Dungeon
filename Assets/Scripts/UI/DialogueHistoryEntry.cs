/****************************************************
    文件：DialogueHistoryEntry.cs
    功能：历史面板中的一条对话条目
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

public class DialogueHistoryEntry : MonoBehaviour
{
    [SerializeField] private Text _speakerNameText;
    //[SerializeField] private Image _iconImage;
    [SerializeField] private Text _contentText;

    public void Bind(DialogueLine line)
    {
        if (line == null) return;

        _speakerNameText.text = string.IsNullOrEmpty(line.speakerName)
            ? "旁白"
            : line.speakerName;

        _contentText.text = line.content;

        //// 头像
        //if (_iconImage != null)
        //{
        //    if (string.IsNullOrEmpty(line.iconName))
        //    {
        //        _iconImage.enabled = false;
        //    }
        //    else
        //    {
        //        var sp = Resources.Load<Sprite>("Icons/" + line.iconName);
        //        _iconImage.sprite = sp;
        //        _iconImage.enabled = sp != null;
        //    }
        //}
    }
}