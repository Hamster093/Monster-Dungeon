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

    public static void Initialize()
    {
        if (Instance != null) return;
        Instance = new DialogueHistoryModel();
    }

    /// <summary>新增一条对话记录</summary>
    public void Add(DialogueLine line)
    {
        if (line == null) return;
        _lines.Add(line);
    }

    /// <summary>清空历史</summary>
    public void Clear()
    {
        _lines.Clear();
    }
}