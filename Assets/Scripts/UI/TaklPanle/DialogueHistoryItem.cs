/****************************************************
    文件：DialogueHistoryItem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/3 21:22:29
	功能：简单对话条类
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

public class DialogueHistoryItem : MonoBehaviour
{
    public Text content;

    private void Awake()
    {
        if (content == null) content = GetComponent<Text>();
    }

    public void SetData(DialogueLine line)
    {
        content.text = line.speakerName + ": " + line.content;
    }
}
