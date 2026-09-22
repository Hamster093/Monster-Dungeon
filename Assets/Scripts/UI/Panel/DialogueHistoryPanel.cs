/****************************************************
    文件：DialogueHistoryPanel.cs
    功能：历史对话面板，滚动展示所有已播放的对话
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

public class DialogueHistoryPanel : PanelBase
{
    [Header("滚动视图")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _content;
    [SerializeField] private DialogueHistoryEntry _entryPrefab;
    [SerializeField] private Button _closeButton;

    public override bool IsModal => true;

    private readonly System.Collections.Generic.List<DialogueHistoryEntry> _pool
        = new System.Collections.Generic.List<DialogueHistoryEntry>();

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        Rebuild();
    }
    public override void OnInit()
    {
        _closeButton.onClick.AddListener(OnCloseClick);
    }

    public override void OnDestroy()
    {
        _closeButton.onClick.RemoveListener(OnCloseClick);
    }
    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    private void Rebuild()
    {
        // 清空已有条目（回收到 pool，之后可以复用）
        for (int i = 0; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);

        var lines = DialogueHistoryModel.Instance?.Lines;
        if (lines == null) return;

        for (int i = 0; i < lines.Count; i++)
        {
            var entry = GetOrCreateEntry(i);
            entry.Bind(lines[i]);
        }

        // 刷新布局，然后滚到最底（最新一条）
        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 0f;
    }

    private DialogueHistoryEntry GetOrCreateEntry(int index)
    {
        if (index < _pool.Count)
        {
            _pool[index].gameObject.SetActive(true);
            return _pool[index];
        }

        var entry = Instantiate(_entryPrefab, _content);
        _pool.Add(entry);
        return entry;
    }

    private void OnCloseClick()
    {
        UIManager.Instance.Close(this);
    }
}