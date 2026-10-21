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
    public string tag;
    public int param;
    // Ink 原始选项位置是稳定标识；显示文案和列表序号都不能作为结算去重键。
    public string choiceId;
    public string checkAttribute, checkKind;
    public int requiredItemId; // 明确的剧情道具门槛，显示与提交共用。
    public string displayText; // 仅供界面显示的标注，历史记录仍保存原始 text。

}

/// <summary>
/// 对话节点
/// </summary>
[Serializable]
public class DialogueNode
{
    public string id;           // 节点唯一ID
    //新：多行支持
    public List<DialogueLineData> lines = new List<DialogueLineData>();

    // 旧：兼容字段，lines 为空时才回退到这里
    public string speakerName;  //说话人
    public string iconName;     //图片名称
    public List<string> contents = new List<string>();       // 对话内容

    public string nextNodeId;   // 自动跳转的下一节点ID（没有选项时使用）
    public List<DialogueOption> options = new List<DialogueOption>(); // 玩家选项（如果有，则覆盖 nextNodeId）
}

[Serializable]
public class DialogueLineData
{
    public string sourceId; // 原稿稳定句地址，供差分演出配置。
    public string speaker;
    public string icon;
    public string content;
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
    /// <summary>
    /// 是否等待外部事件完成后再继续对话（例如等待委托接取完成、动画播放完毕等）
    /// </summary>
    public bool waitForExternal;

    public bool cancelJump;     //取消本次跳转，留在当前选项
}
