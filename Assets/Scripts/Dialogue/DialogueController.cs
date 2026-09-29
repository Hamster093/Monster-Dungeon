/****************************************************
    文件：DialogueController.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/29 18:44:23
	功能：对话流程控制器
*****************************************************/

using System.Collections.Generic;
using UnityEngine;

public class DialogueController : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private TalkPanle _talkPanel;
    [SerializeField] private GameObject _optionPanel;       // 选项按钮的父节点
    [SerializeField] private Transform _optionButtonRoot;   // 选项按钮生成位置
    [SerializeField] private GameObject _optionButtonPrefab; // 选项按钮预制体

    private DialogueData _currentData;
    private Dictionary<string, DialogueNode> _nodeDict;
    private string _currentStopNodeId;

    private void Start()
    {
        // 监听 TalkPanel 播放完毕事件
        if (_talkPanel != null)
            _talkPanel.OnDialogueComplete += OnTalkPanelComplete;
    }

    /// <summary>
    /// 对外唯一接口：开始一段新对话
    /// </summary>
    public async void StartDialogue(string dialogueAddressableKey)
    {
        _currentData = await DialogueLoader.LoadDialogueData(dialogueAddressableKey);
        if (_currentData == null) return;

        // 将 List 转为 Dictionary 方便按 ID 查找
        _nodeDict = new Dictionary<string, DialogueNode>();
        foreach (var node in _currentData.nodes)
            _nodeDict[node.id] = node;

        PlayFromNode(_currentData.startNodeId);
    }

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
        DialogueNode stopNode = _nodeDict[_currentStopNodeId];

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
        foreach (Transform child in _optionButtonRoot) Destroy(child.gameObject);

        foreach (var option in options)
        {
            GameObject btnObj = Instantiate(_optionButtonPrefab, _optionButtonRoot);
            // 假设按钮上有 Text 和 Button 组件
            btnObj.GetComponentInChildren<UnityEngine.UI.Text>().text = option.text;
            string targetId = option.targetNodeId; // 闭包捕获

            btnObj.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
            {
                _optionPanel.SetActive(false);
                PlayFromNode(targetId);
            });
        }
    }

    private void EndDialogue()
    {
        _talkPanel.gameObject.SetActive(false);
        Debug.Log("[DialogueController] 对话结束");
    }

    private void OnDestroy()
    {
        if (_talkPanel != null)
            _talkPanel.OnDialogueComplete -= OnTalkPanelComplete;
    }
}
