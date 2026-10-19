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
    private readonly List<DialogueOption> _optionData=new();
    // 库存变化后同步置灰；可视按钮与真正提交读取同一项需求。
    private void LateUpdate()=>RefreshOptionAvailability();
    private void RefreshOptionAvailability()
    {
        var narrative=SystemManager.Instance.Narrative;
        for(int i=0;i<_activeOptions.Count&&i<_optionData.Count;i++)
            if(_activeOptions[i])_activeOptions[i].SetInteractable(narrative==null||narrative.IsOptionAvailable(_optionData[i]));
    }

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
    public void ShowOptions(List<DialogueOption> options, Func<DialogueOption, bool> onSelected)
    {
        ClearOptions();
        if (options == null || options.Count == 0) return;

        foreach (var opt in options)
        {
            var item = Instantiate(_optionItemPrefab, _content);
            item.transform.SetAsLastSibling();

            var captured = opt;
            item.SetData(string.IsNullOrEmpty(opt.displayText) ? opt.text : opt.displayText, () =>
            {
                // 先禁用所有选项，防重复点击
                foreach (var o in _activeOptions)
                    if (o != null) o.SetInteractable(false);

                // 回调返回 true = 保留选项，false = 清空
                bool keep = onSelected?.Invoke(captured) ?? false;

                if (keep)
                {
                    for(int i=0;i<_activeOptions.Count;i++)
                        if(_activeOptions[i]!=null)_activeOptions[i].SetInteractable(SystemManager.Instance.Narrative?.IsOptionAvailable(_optionData[i])!=false);
                }
                else
                {
                    ClearOptions();
                }
            });

            _activeOptions.Add(item);_optionData.Add(opt);
            item.SetInteractable(SystemManager.Instance.Narrative?.IsOptionAvailable(opt)!=false);
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
        _activeOptions.Clear();_optionData.Clear();
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
