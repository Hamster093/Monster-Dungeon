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
    [Tooltip("选项按钮的父节点")]
    [SerializeField] private GameObject _optionPanel;

    [Tooltip("选项按钮生成位置")]
    [SerializeField] private Transform _optionButtonRoot;

    [Tooltip("选项按钮预制体")]
    [SerializeField] private GameObject _optionButtonPrefab;

    // 运行时绑定，不参与序列化
    [NonSerialized] private TalkPanel _talkPanel;
    [NonSerialized] private bool _disposed;

    private DialogueData _currentData;
    private Dictionary<string, DialogueNode> _nodeDict;
    private string _currentStopNodeId;

    //========对话回调相关========
    private bool _playingExtraLines;//正在播放返回的额外台词
    private string _pendingNextNodeId;//待处理的下一个节点ID

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
        if (_talkPanel == null)
        {
            Debug.LogError("[DialogueController] 未初始化或已被 Dispose，无法开始对话");
            return;
        }

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

            //遍历该节点包含的所有句子
            if (node.contents != null && node.contents.Count > 0)
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
            else
            {
                Debug.LogWarning($"[DialogueController] 节点 {node.id} 的 contents 为空！");
            }

            // 判断该节点是否有选项，或者没有下一节点
            if ((node.options != null && node.options.Count > 0) || string.IsNullOrEmpty(node.nextNodeId))
            {
                _currentStopNodeId = currentNodeId;
                break;
            }

            currentNodeId = node.nextNodeId;
        }

        if (linearLines.Count > 0)
        {
            DialogueNode stopNode = _nodeDict[_currentStopNodeId];
            bool hasOptions = stopNode.options != null && stopNode.options.Count > 0;

            _talkPanel.gameObject.SetActive(true);
            _talkPanel.Init(linearLines);
            _talkPanel.ResetDialogue();
            _talkPanel.SetAutoCompleteAtEnd(hasOptions);
            _talkPanel.MoveNext();
        }
    }

    /// <summary>
    /// TalkPanel 播放完一段线性对话后触发
    /// </summary>
    private void OnTalkPanelComplete()
    {
        // 额外行播放完毕 → 继续原流程
        if (_playingExtraLines)
        {
            _playingExtraLines = false;
            string next = _pendingNextNodeId;
            _pendingNextNodeId = null;
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
            EndDialogue();
        }
    }

    private void ShowOptions(List<DialogueOption> options)
    {
        _optionPanel.SetActive(true);
        // 清理旧按钮
        foreach (Transform child in _optionButtonRoot)
            UnityEngine.Object.Destroy(child.gameObject);

        foreach (var option in options)
        {
            GameObject btnObj = UnityEngine.Object.Instantiate(_optionButtonPrefab, _optionButtonRoot);
            // 假设按钮上有 Text 和 Button 组件
            btnObj.GetComponentInChildren<UnityEngine.UI.Text>().text = option.text;
            string targetId = option.targetNodeId; // 闭包捕获

            btnObj.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
            {
                _optionPanel.SetActive(false);
                HandleOptionSelected(option);
            });
        }
    }
    /// <summary>
    /// 处理选项选中事件：先尝试执行回调，若有额外台词则先播额外台词，否则直接跳转到目标节点
    /// </summary>
    /// <param name="option"></param>
    private void HandleOptionSelected(DialogueOption option)
    {
        string targetId = option.targetNodeId;

        // 有回调就先执行回调
        if (DialogueCallbackRegistry.TryInvoke(option.callbackKey, option, out var result))
        {
            if (!string.IsNullOrEmpty(result.targetNodeId))
                targetId = result.targetNodeId;

            // 有额外行 → 先播这些行，播完再跳
            if (result.extraLines != null && result.extraLines.Count > 0)
            {
                PlayExtraLines(result.extraLines, targetId);
                return;
            }
        }

        PlayFromNode(targetId);
    }
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
        if (_talkPanel != null)
            _talkPanel.gameObject.SetActive(false);
        Debug.Log("[DialogueController] 对话结束");
    }

    #endregion
}
