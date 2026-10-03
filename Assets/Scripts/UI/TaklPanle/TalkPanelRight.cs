using System.Collections;
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

    // 对象池
    private readonly Queue<DialogueHistoryItem> _pool = new();
    private readonly List<DialogueHistoryItem> _activeItems = new();
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
    /// 滚动到底部
    /// </summary>
    private void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        _scrollRect.verticalNormalizedPosition = 0f;
    }


}
