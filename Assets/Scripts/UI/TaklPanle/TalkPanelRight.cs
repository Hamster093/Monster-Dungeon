using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//*****************************************
//创建人： DaDi 
//功能说明：
//***************************************** 
public class TalkPanelRight :PanelBase
{
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _content;
    [SerializeField] private DialogueHistoryItem _itemPrefab;
    [SerializeField] private DialogueOptionItem _optionItemPrefab;

    // 对象池
    private readonly Queue<DialogueHistoryItem> _pool = new();
    private readonly List<DialogueHistoryItem> _activeItems = new();
    private readonly List<DialogueOptionItem> _activeOptions = new();
    // 限制最大显示条数
    private const int MaxShowCount = 50;

    public override void OnInit()
    {
        // 从模型拿全部历史
        var history = DialogueHistoryModel.Instance?.Lines;
        if (history == null || history.Count == 0) return;

        // 只取最近 MaxShowCount 条
        int start = Mathf.Max(0, history.Count - MaxShowCount);
        for (int i = start; i < history.Count; i++)
        {
            AddItem(history[i]);
        }

        ScrollToBottom();

    }

    /// <summary>
    ///  新对话产生时调用
    /// </summary>
    /// <param name="line"></param>
    public void AppendLine(DialogueLine line)
    {
        AddItem(line);
        ScrollToBottom();
    }
    /// <summary>
    /// 添加对话条目到UI中
    /// </summary>
    /// <param name="line"></param>
    private void AddItem(DialogueLine line)
    {
        DialogueHistoryItem item;
        if (_pool.Count > 0)
        {
            item = _pool.Dequeue();
            item.gameObject.SetActive(true);
        }
        else
        {
            item = Instantiate(_itemPrefab, _content);
        }

        item.transform.SetAsLastSibling(); // 确保在底部
        item.SetData(line);
        _activeItems.Add(item);
    }
    /// <summary>
    /// 追加一条任意文本（系统提示 / 旁白 / 内心独白等，不带说话人）
    /// </summary>
    public void AppendText(string content)
    {
        if (string.IsNullOrEmpty(content)) return;

        var line = new DialogueLine
        {
            speakerName = string.Empty,
            content = content,
            iconName = string.Empty
        };

        AddItem(line);
        ScrollToBottom();
    }
   

    /// <summary>
    /// 在历史内容末尾插入选项按钮
    /// </summary>
    public void ShowOptions(List<DialogueOption> options, Action<DialogueOption> onSelected)
    {
        ClearOptions();
        if (options == null || options.Count == 0) return;

        foreach (var opt in options)
        {
            var item = Instantiate(_optionItemPrefab, _content);
            item.transform.SetAsLastSibling();

            var captured = opt;
            item.SetData(opt.text, () =>
            {
                // 先禁用，再清空
                foreach (var o in _activeOptions)
                    if (o != null) o.SetInteractable(false);
                ClearOptions();
                onSelected?.Invoke(captured);
            });

            _activeOptions.Add(item);
        }

        ScrollToBottom();
    }

    /// <summary>
    /// 销毁所有已生成的选项按钮
    /// </summary>
    public void ClearOptions()
    {
        for (int i = 0; i < _activeOptions.Count; i++)
        {
            if (_activeOptions[i] != null)
                Destroy(_activeOptions[i].gameObject);
        }
        _activeOptions.Clear();
    }

    /// <summary>
    /// 滚动到底部
    /// </summary>
    private void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        _scrollRect.verticalNormalizedPosition = 0f;
    }
}
