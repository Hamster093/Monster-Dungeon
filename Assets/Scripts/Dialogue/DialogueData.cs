/****************************************************
    文件：DialogueData.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/29 18:42:10
	功能：对话数据类
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话选项
/// </summary>
[Serializable]
public class DialogueOption
{
    public string text;        // 选项文本
    public string targetNodeId; // 跳转到的目标节点ID
    [Tooltip("回调标识，运行时由业务注册对应逻辑")]
    public string callbackKey;   // 可为空
}

/// <summary>
/// 对话节点
/// </summary>
[Serializable]
public class DialogueNode
{
    public string id;           // 节点唯一ID
    public string speakerName;  //说话人
    public string iconName;     //图片名称
    public List<string> contents;       // 对话内容

    public string nextNodeId;   // 自动跳转的下一节点ID（没有选项时使用）
    public List<DialogueOption> options; // 玩家选项（如果有，则覆盖 nextNodeId）
}

/// <summary>
/// 对话数据
/// </summary>
[Serializable]
public class DialogueData
{
    public string dialogueId;   // 当前对话文件的ID（比如 "npc_001"）
    public string startNodeId;  // 起始节点
    public List<DialogueNode> nodes;
}

/// <summary
/// >回调返回结果，用于覆盖跳转节点、插入额外对话行
/// </summary>
public class DialogueOptionResult
{
    /// <summary>
    /// 覆盖 option.targetNodeId，为空则用 option 自身的
    /// </summary>
    public string targetNodeId;
    /// <summary>
    /// 要插入到对话流里的附加行（例如检定结果）
    /// </summary>
    public List<DialogueLine> extraLines;
}