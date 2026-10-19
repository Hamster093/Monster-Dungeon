/****************************************************
    文件：DialogueController.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/29 18:44:23
	功能：对话流程控制器（由 TalkPanel 持有
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class DialogueController
{
    // 运行时绑定，不参与序列化
    [NonSerialized] private TalkPanel _talkPanel;
    [NonSerialized] private bool _disposed;

    private DialogueData _currentData;
    private Dictionary<string, DialogueNode> _nodeDict;
    private string _currentStopNodeId;
    private bool _waitingForExternal;//等待外部回调处理，用于接收委托等特殊情况，回调处理完后再继续播放下一节点

    //========对话回调相关========
    private bool _playingExtraLines;//正在播放返回的额外台词
    private string _pendingNextNodeId;//待处理的下一个节点ID

    private string _pendingStartKey;   // 待启动的下一位访客对话 key
    [NonSerialized] private bool _narrativeMode;
    [NonSerialized] private bool _narrativeAdvancing;
    [NonSerialized] private bool _waitingPresentation;

    /// <summary>完整剧情仍使用本控制器的现有节点显示逻辑；Ink 负责提供下一帧节点。</summary>
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

    /// <summary>
    /// 访客间过渡台词提供者。参数 = 下一位访客的对话 key，返回 null/空表示不插过渡。
    /// 外部（如 GameBootstrap）可注册，不注册则直接切下一位。
    /// </summary>
    public static Func<string, List<DialogueLine>> TransitionProvider = DefaultTransition;

    private static List<DialogueLine> DefaultTransition(string nextKey)
    {
        return new List<DialogueLine>
    {
        new DialogueLine { speakerName = "旁白", content = "他离开了。" },
        new DialogueLine { speakerName = "旁白", content = "门再次被推开，新的客人走了进来。" }
    };
    }

    /// <summary>
    /// 所有访客接待完毕、结算前的钩子。
    /// 外部注册后，自行处理检定、总结、回访等，处理完调 controller.ContinueToSettlement()。
    /// 未注册 = 直接 EndDialogue。
    /// </summary>
    public static Action<DialogueController> OnAllVisitorsFinished;
    private Action _extraCompleteCallback;

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

    #region 对外接口

    /// <summary>
    /// 对外唯一接口：开始一段新对话
    /// </summary>
    public async void StartDialogue(string dialogueAddressableKey)
    {
        _narrativeMode = false;
        if (_talkPanel == null)
        {
            Debug.LogError("[DialogueController] 未初始化或已被 Dispose，无法开始对话");
            return;
        }
        _talkPanel.CheckDayChangedAndClear();
        _currentData = await DialogueLoader.LoadDialogueData(dialogueAddressableKey);
        if (_currentData == null || _disposed || _talkPanel == null) return;

        // 将 List 转为 Dictionary 方便按 ID 查找
        _nodeDict = new Dictionary<string, DialogueNode>();
        foreach (var node in _currentData.nodes)
            _nodeDict[node.id] = node;

        PlayFromNode(_currentData.startNodeId);
    }

    #endregion

    #region 内部流程
    /// <summary>
    /// 从指定节点开始，向后拼接一段“线性对话”喂给 TalkPanel
    /// </summary>
    private void PlayFromNode(string nodeId)
    {
        _currentStopNodeId = nodeId;
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
                _currentStopNodeId = currentNodeId;
                break;
            }
            currentNodeId = node.nextNodeId;
        }

        // 找到停止节点
        if (!_nodeDict.TryGetValue(_currentStopNodeId, out var stopNode))
        {
            Debug.LogError($"[DialogueController] 找不到停止节点：{_currentStopNodeId}");
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
        if (_narrativeMode)
        {
            if (_narrativeAdvancing || _waitingForExternal || UIManager.Instance.HasLeftOverlay || _disposed || _talkPanel == null) return;
            var node = _currentData.nodes[0];
            if (node.options != null && node.options.Count > 0) ShowOptions(node.options);
            else
            {
                SystemManager.Instance.Narrative.Next();
                _talkPanel.StartCoroutine(AdvanceNarrative());
            }
            return;
        }
        // 额外行播放完毕 → 继续原流程
        if (_playingExtraLines)
        {
            _playingExtraLines = false;
            string next = _pendingNextNodeId;
            _pendingNextNodeId = null;

            if (next == "__START_NEXT__")//访客间过渡台词播完 → 继续下一位访客
            {
                string key = _pendingStartKey;
                _pendingStartKey = null;
                if (!string.IsNullOrEmpty(key)) StartDialogue(key);
                else EndDialogue();
                return;
            }

            if (next == "__REPORT_DONE__")  // 回访阶段播
            {
                var cb = _extraCompleteCallback;
                _extraCompleteCallback = null;
                cb?.Invoke();
                return;
            }

            if (!string.IsNullOrEmpty(next)) PlayFromNode(next);
            else EndDialogue();
            return;
        }

        if (_disposed || _talkPanel == null) return;
        if (_nodeDict == null || !_nodeDict.TryGetValue(_currentStopNodeId, out var stopNode))
        {
            Debug.LogError($"[DialogueController] 找不到节点：{_currentStopNodeId}");
            return;
        }

        // 1. 如果有选项，弹窗让玩家选
        if (stopNode.options != null && stopNode.options.Count > 0)
        {
            ShowOptions(stopNode.options);
        }
        // 2. 没有选项，也没有下一节点，对话彻底结束
        else if (string.IsNullOrEmpty(stopNode.nextNodeId))
        {
            OnDialogueSequenceEnd();
        }
    }

    /// <summary>
    /// 一段对话自然结束时：
    /// - 若今日还有未接待的访客 → 推进到该访客，并用他的对话 key 开始新对话
    /// - 否则 → EndDialogue() 关闭面板
    /// </summary>
    private void OnDialogueSequenceEnd()
    {
        var vs = SystemManager.Instance.VisitSystem;
        bool hasNext = vs.OnVisitorFinished();

        if (hasNext)
        {
            string key = vs.GetCurrentDialogueKey();
            if (!string.IsNullOrEmpty(key))
            {
                //  问外部有没有过渡台词
                var transition = TransitionProvider?.Invoke(key);
                if (transition != null && transition.Count > 0)
                {
                    PlayTransitionThenStart(transition, key);
                }
                else
                {
                    StartDialogue(key);
                }
                return;
            }
        }
        //  无下一位访客 → 问外部有没有回访阶段
        if (OnAllVisitorsFinished != null)
        {
            var handler = OnAllVisitorsFinished;
            OnAllVisitorsFinished = null;   // 一次性
            handler(this);
            return;
        }

        EndDialogue();
    }
    private void PlayTransitionThenStart(List<DialogueLine> lines, string nextKey)
    {
        _playingExtraLines = true;
        _pendingNextNodeId = "__START_NEXT__";   // 特殊标记
        _pendingStartKey = nextKey;

        _talkPanel.gameObject.SetActive(true);
        _talkPanel.Init(lines);
        _talkPanel.ResetDialogue();

        _talkPanel.SetInstantText(true);         // 过渡不走打字机，快速过
        _talkPanel.SetAutoCompleteAtEnd(false);  // 等玩家点击推进

        _talkPanel.MoveNext();
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
    /// 处理选项选中事件：先尝试执行回调，若有额外台词则先播额外台词，否则直接跳转到目标节点
    /// </summary>
    /// <param name="option"></param>
    private bool HandleOptionSelected(DialogueOption option)
    {
        if (_narrativeMode)
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
        string targetId = option.targetNodeId;

        if (DialogueCallbackRegistry.TryInvoke(option.callbackKey, option, out var result))
        {
            if (!string.IsNullOrEmpty(result.targetNodeId))
                targetId = result.targetNodeId;

            if (result.extraLines != null && result.extraLines.Count > 0)
            {
                PlayExtraLines(result.extraLines, targetId);
                return false;
            }

            if (result.waitForExternal)
            {
                _waitingForExternal = true;   // ★ 关键
                return false;
            }

            if (result.cancelJump) return true;
        }

        PlayFromNode(targetId);
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
        if (_narrativeMode)
        {
            _waitingForExternal = false;
            _talkPanel.StartCoroutine(AdvanceNarrative());
            return;
        }
        if (!_waitingForExternal) return;
        _waitingForExternal = false;
        PlayFromNode(_currentStopNodeId);
    }

    public bool IsNarrativeMode => _narrativeMode;
    /// <summary>
    /// 播放额外台词
    /// </summary>
    /// <param name="lines"></param>
    /// <param name="nextNodeId"></param>
    private void PlayExtraLines(List<DialogueLine> lines, string nextNodeId)
    {
        _playingExtraLines = true;
        _pendingNextNodeId = nextNodeId;

        _talkPanel.gameObject.SetActive(true);
        _talkPanel.Init(lines);
        _talkPanel.ResetDialogue();

        _talkPanel.SetInstantText(true);// 立即显示，不走打字机
        _talkPanel.SetAutoCompleteAtEnd(false);// 不自动跳转，等玩家点击
        _talkPanel.MoveNext();
    }

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

    #region 工具方法
    /// <summary>
    /// 往 TalkPanel 插一段线性对话，播完执行 onComplete。
    /// 用于回访、总结、过渡等场景，不走 JSON 节点。
    /// </summary>
    public void PlayReportLines(List<DialogueLine> lines, Action onComplete)
    {
        if (lines == null || lines.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        _extraCompleteCallback = onComplete;
        _playingExtraLines = true;
        _pendingNextNodeId = "__REPORT_DONE__";

        _talkPanel.gameObject.SetActive(true);
        _talkPanel.Init(lines);
        _talkPanel.ResetDialogue();
        _talkPanel.SetInstantText(true);
        _talkPanel.SetAutoCompleteAtEnd(false);   // 播完自动触发 OnTalkPanelComplete

        _talkPanel.MoveNext();
    }

    /// <summary>回访阶段结束，继续走结算流程</summary>
    public void ContinueToSettlement() => EndDialogue();
    #endregion
}
