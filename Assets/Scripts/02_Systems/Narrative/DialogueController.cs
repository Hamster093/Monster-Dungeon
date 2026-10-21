/****************************************************
    文件：DialogueController.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/29 18:44:23
    功能：对话流程控制器（由 TalkPanel 持有）
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话流程控制器。27 天剧情（Ink）提供每一帧节点，
/// 本控制器负责把帧渲染成线性台词交给 TalkPanel，并处理选项与外部回调。
/// </summary>
[Serializable]
public class DialogueController
{
    // 运行时绑定，不参与序列化
    [NonSerialized] private TalkPanel _talkPanel;
    [NonSerialized] private bool _disposed;

    private DialogueData _currentData;
    private Dictionary<string, DialogueNode> _nodeDict;
    private bool _waitingForExternal;//等待外部回调处理，用于接收委托等特殊情况，回调处理完后再继续播放下一节点

    [NonSerialized] private bool _narrativeMode;
    [NonSerialized] private bool _narrativeAdvancing;
    [NonSerialized] private bool _waitingPresentation;

    /// <summary>完整剧情使用本控制器的现有节点显示逻辑；Ink 负责提供下一帧节点。</summary>
    public void StartNarrative()
    {
        if(PresentationEffects.IsBusy) { WaitForPresentation(); return; }
        _narrativeMode = true;
        _narrativeAdvancing = false;
        _talkPanel.CheckDayChangedAndClear();
        var campaign = SystemManager.Instance.Narrative;
        var frame = campaign.GetFrame();
        if (frame == null) { campaign.PresentPause(); return; }
        var visitorTransition=campaign.ConsumeVisitorTransition();
        if(!string.IsNullOrEmpty(visitorTransition))
        {
            string portrait=null;
            foreach(var part in frame.nodes)foreach(var line in part.lines)
                if(portrait==null&&!string.IsNullOrEmpty(line.icon))portrait=line.icon;
            var owner=_talkPanel;
            PresentationEffects.PlayVisitor(visitorTransition,()=>{if(owner)owner.PrepareVisitorPortrait(portrait);},()=>!owner||!owner.IsPortraitLoading);
            WaitForPresentation();return;
        }
        if(PresentationEffects.IsBusy) { WaitForPresentation(); return; }
        _currentData = frame;
        _nodeDict = new Dictionary<string, DialogueNode>();
        foreach (var node in frame.nodes) _nodeDict[node.id] = node;
        _talkPanel.SetInstantText(false);
        PlayFromNode(frame.startNodeId);
    }

    private void WaitForPresentation()
    {
        if(_waitingPresentation||_disposed||_talkPanel==null)return;
        _waitingPresentation=true;_talkPanel.StartCoroutine(ResumeAfterPresentation());
    }
    private System.Collections.IEnumerator ResumeAfterPresentation()
    {
        yield return null;
        while(PresentationEffects.IsBusy||GuideHelpOverlay.BlocksInput)yield return null;
        _waitingPresentation=false;
        if(!_disposed&&_talkPanel!=null)StartNarrative();
    }

    // 延后一帧，等旧按钮清理完再生成新选项，避免新选项被旧回调清掉。
    private System.Collections.IEnumerator AdvanceNarrative()
    {
        _narrativeAdvancing = true;
        yield return null;
        if (!_disposed && _talkPanel != null) StartNarrative();
    }

    #region 生命周期（由 TalkPanel 调用）

    /// <summary>
    /// 由 TalkPanel 在 OnInit 里调用：绑定 owner 并订阅事件
    /// </summary>
    public void Init(TalkPanel talkPanel)
    {
        _talkPanel = talkPanel;
        _talkPanel.OnDialogueComplete += OnTalkPanelComplete;
    }
    /// <summary>
    /// 由 TalkPanel 在 OnDestroy 里调用：退订，释放引用
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_talkPanel != null)
            _talkPanel.OnDialogueComplete -= OnTalkPanelComplete;

        _talkPanel = null;
    }

    #endregion

    #region 内部流程
    /// <summary>
    /// 从指定节点开始，向后拼接一段“线性对话”喂给 TalkPanel
    /// </summary>
    private void PlayFromNode(string nodeId)
    {
        string currentStopNodeId = nodeId;
        List<DialogueLine> linearLines = new List<DialogueLine>();
        string currentNodeId = nodeId;

        while (!string.IsNullOrEmpty(currentNodeId) && _nodeDict.ContainsKey(currentNodeId))
        {
            DialogueNode node = _nodeDict[currentNodeId];

            // 优先读 lines，回退到旧 contents
            if (node.lines != null && node.lines.Count > 0)
            {
                foreach (var line in node.lines)
                {
                    linearLines.Add(new DialogueLine
                    {
                        iconName = string.IsNullOrEmpty(line.icon) ? node.iconName : line.icon,
                        sourceId = line.sourceId,
                        speakerName = line.speaker,
                        content = line.content
                    });
                }
            }
            else if (node.contents != null && node.contents.Count > 0)
            {
                foreach (string text in node.contents)
                {
                    linearLines.Add(new DialogueLine
                    {
                        iconName = node.iconName,
                        speakerName = node.speakerName,
                        content = text
                    });
                }
            }

            if ((node.options != null && node.options.Count > 0) || string.IsNullOrEmpty(node.nextNodeId))
            {
                currentStopNodeId = currentNodeId;
                break;
            }
            currentNodeId = node.nextNodeId;
        }

        // 找到停止节点
        if (!_nodeDict.TryGetValue(currentStopNodeId, out var stopNode))
        {
            Debug.LogError($"[DialogueController] 找不到停止节点：{currentStopNodeId}");
            EndDialogue();
            return;
        }

        bool hasOptions = stopNode.options != null && stopNode.options.Count > 0;

        _talkPanel.gameObject.SetActive(true);
        _talkPanel.Init(linearLines);              // 空列表也初始化
        _talkPanel.ResetDialogue();
        _talkPanel.SetAutoCompleteAtEnd(hasOptions);
        _talkPanel.MoveNext();                     // 空列表会立即触发 OnDialogueComplete
    }

    /// <summary>
    /// TalkPanel 播放完一段线性对话后触发
    /// </summary>
    private void OnTalkPanelComplete()
    {
        if (!_narrativeMode) return;
        if (_narrativeAdvancing || _waitingForExternal || UIManager.Instance.HasLeftOverlay || _disposed || _talkPanel == null) return;
        var node = _currentData.nodes[0];
        if (node.options != null && node.options.Count > 0) ShowOptions(node.options);
        else
        {
            SystemManager.Instance.Narrative.Next();
            _talkPanel.StartCoroutine(AdvanceNarrative());
        }
    }

    private void ShowOptions(List<DialogueOption> options)
    {
        _talkPanel.ShowOptions(options, option =>
        {
            bool keep = HandleOptionSelected(option);

            // 只有"真的推进/跳转"才记录选择，cancelJump 不记录
            if (!keep)
                _talkPanel.RecordPlayerChoice(option.text);

            return keep;
        });
    }

    /// <summary>
    /// 处理选项选中事件：先尝试执行回调，否则交给 Ink 剧情推进。
    /// </summary>
    /// <param name="option"></param>
    private bool HandleOptionSelected(DialogueOption option)
    {
        if (_narrativeAdvancing || _waitingForExternal || UIManager.Instance.HasLeftOverlay) return true;
        // 完整剧情同样走原回调注册表。功能按钮不会再当作普通台词分支展开。
        if (!string.IsNullOrEmpty(option.callbackKey) && DialogueCallbackRegistry.TryInvoke(option.callbackKey, option, out var callback))
        {
            if (callback.cancelJump) return true;
            if (callback.waitForExternal) { _waitingForExternal = true; return false; }
        }
        else if (!SystemManager.Instance.Narrative.Choose(option)) return true;
        _talkPanel.StartCoroutine(AdvanceNarrative());
        return false;
    }

    /// <summary>外部（面板）通知：继续到指定节点</summary>
    public void ContinueFromNode(string nodeId)
    {
        if (!_waitingForExternal) return;
        _waitingForExternal = false;
        PlayFromNode(nodeId);
    }

    /// <summary>外部（面板）通知：放弃，重放当前节点</summary>
    public void ResumeCurrentNode()
    {
        _waitingForExternal = false;
        if (_talkPanel == null) return;
        _talkPanel.StartCoroutine(AdvanceNarrative());
    }

    public bool IsNarrativeMode => _narrativeMode;

    private void EndDialogue()
    {
        UIManager.Instance.Close<TalkPanel>();

        // 先渐黑，黑屏完成后打开结算面板
        ScreenFader.Instance.FadeToBlack(2f, () =>
        {
            var data = DailyIncomeCalculator.Compute();
            UIManager.Instance.Open<DailySettlementPanel>(data);
            Debug.Log("[DialogueController] 对话结束,打开结算面板");
        });
    }

    #endregion
}
