/****************************************************
    文件：DialogueHistoryModel.cs
    功能：对话历史数据
*****************************************************/

using System.Collections.Generic;
using UnityEngine;

public class DialogueHistoryModel
{
    public static DialogueHistoryModel Instance { get; private set; }

    private readonly List<DialogueLine> _lines = new List<DialogueLine>();

    public IReadOnlyList<DialogueLine> Lines => _lines;
    public int Count => _lines.Count;

    public static void Initialize()
    {
        if (Instance != null) return;
        Instance = new DialogueHistoryModel();
    }

    /// <summary>新增一条对话记录（调用方负责避免同一句重复写入）</summary>
    public void Add(DialogueLine line)
    {
        if (line == null) return;
        _lines.Add(line);
    }
    /// <summary>追加一条“玩家选择”记录（伪 DialogueLine）</summary>
    public void AddChoice(string buttonText)
    {
        if (string.IsNullOrEmpty(buttonText)) return;
        _lines.Add(new DialogueLine
        {
            speakerName = "你",
            content = $"选择'{buttonText}'",
            iconName = string.Empty
        });
    }

    public void Clear()
    {
        _lines.Clear();
    }
}