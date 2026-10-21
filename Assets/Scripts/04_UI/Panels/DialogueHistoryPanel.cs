
/****************************************************
    文件：DialogueHistoryPanel.cs
    功能：历史对话面板，分页 + 反向生成 + 对象池（池上限 100）
*****************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DialogueHistoryPanel : PanelBase
{
    private const int PageSize = 50;   // 每次加载条数
    private const int PoolMax = 100;  // 空闲池上限，超出销毁

    [Header("滚动视图")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _content;
    [SerializeField] private DialogueHistoryEntry _entryPrefab;
    [SerializeField] private Button _closeButton;

    public override bool IsModal => true;

    private readonly List<DialogueHistoryEntry> _idlePool = new List<DialogueHistoryEntry>();
    private readonly List<DialogueHistoryEntry> _activeEntries = new List<DialogueHistoryEntry>();

    private int _loadedStartIndex;   // 已经加载到的最早数据索引
    private bool _isLoadingMore;
    private bool _isOpen;

    public override void OnInit()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(OnCloseClick);

        if (_scrollRect != null)
            _scrollRect.onValueChanged.AddListener(OnScrollChanged);
    }

    public override void OnDestroy()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(OnCloseClick);

        if (_scrollRect != null)
            _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);

        // 主动销毁池子里的实例（防止 Panel 被销毁时池子里的还在）
        for (int i = 0; i < _idlePool.Count; i++)
            if (_idlePool[i] != null) Destroy(_idlePool[i].gameObject);
        _idlePool.Clear();

        for (int i = 0; i < _activeEntries.Count; i++)
            if (_activeEntries[i] != null) Destroy(_activeEntries[i].gameObject);
        _activeEntries.Clear();

        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        _isOpen = true;
        Rebuild();
    }

    public override void OnClose()
    {
        _isOpen = false;

        // ★ 回收当前所有条目到池子（超出上限的销毁）
        ReleaseAllToPool();

        base.OnClose();
    }

    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    // ───────────── 打开时重建：加载最新 50 条 ─────────────

    private void Rebuild()
    {
        ReleaseAllToPool();

        var model = DialogueHistoryModel.Instance;
        if (model == null || model.Count == 0) return;

        int total = model.Count;
        int start = Mathf.Max(0, total - PageSize);
        _loadedStartIndex = start;

        for (int i = start; i < total; i++)
        {
            var entry = GetOrCreateEntry();
            entry.Bind(model.Lines[i]);
        }

        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 0f;
    }

    // ───────────── 滚动到顶加载更早的 50 条 ─────────────

    private void OnScrollChanged(Vector2 pos)
    {
        if (!_isOpen || _isLoadingMore) return;
        if (_scrollRect == null) return;

        // verticalNormalizedPosition: 1 = 顶部(最早)，0 = 底部(最新)
        if (_scrollRect.verticalNormalizedPosition >= 0.999f)
            LoadEarlier();
    }

    private void LoadEarlier()
    {
        if (_loadedStartIndex <= 0) return;

        var model = DialogueHistoryModel.Instance;
        if (model == null) return;

        _isLoadingMore = true;

        // 记录加载前的高度与位置，用于保持视觉不跳
        float viewportH = _scrollRect.viewport != null
            ? _scrollRect.viewport.rect.height
            : _scrollRect.GetComponent<RectTransform>().rect.height;

        float oldContentH = _content.rect.height;
        float oldPos = _scrollRect.verticalNormalizedPosition;
        float oldScrollable = Mathf.Max(0f, oldContentH - viewportH);
        float offsetFromBottom = oldScrollable * (1f - oldPos);

        int newStart = Mathf.Max(0, _loadedStartIndex - PageSize);

        // 从 _loadedStartIndex - 1 往前插，每次 SetAsFirstSibling，
        // 最终顺序：newStart ... _loadedStartIndex-1，且在最顶部
        for (int i = _loadedStartIndex - 1; i >= newStart; i--)
        {
            var entry = GetOrCreateEntry();
            entry.Bind(model.Lines[i]);
            entry.transform.SetAsFirstSibling();
        }

        _loadedStartIndex = newStart;

        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        Canvas.ForceUpdateCanvases();

        float newContentH = _content.rect.height;
        float deltaH = newContentH - oldContentH;
        float newScrollable = Mathf.Max(0f, newContentH - viewportH);

        if (newScrollable > 0.01f)
        {
            float newOffsetFromBottom = offsetFromBottom + deltaH;
            _scrollRect.verticalNormalizedPosition =
                Mathf.Clamp01(1f - newOffsetFromBottom / newScrollable);
        }

        _isLoadingMore = false;
    }

    // ───────────── 对象池 ─────────────

    private DialogueHistoryEntry GetOrCreateEntry()
    {
        DialogueHistoryEntry entry;
        if (_idlePool.Count > 0)
        {
            int last = _idlePool.Count - 1;
            entry = _idlePool[last];
            _idlePool.RemoveAt(last);
            entry.gameObject.SetActive(true);
            entry.transform.SetAsLastSibling();   // 复用后回到末尾
        }
        else
        {
            entry = Instantiate(_entryPrefab, _content);
        }

        _activeEntries.Add(entry);
        return entry;
    }

    private void ReleaseAllToPool()
    {
        for (int i = 0; i < _activeEntries.Count; i++)
        {
            var entry = _activeEntries[i];
            if (entry == null) continue;
            entry.gameObject.SetActive(false);
            _idlePool.Add(entry);
        }
        _activeEntries.Clear();

        _isLoadingMore = false;

        // 空闲池超上限的销毁
        while (_idlePool.Count > PoolMax)
        {
            int last = _idlePool.Count - 1;
            var entry = _idlePool[last];
            _idlePool.RemoveAt(last);
            if (entry != null) Destroy(entry.gameObject);
        }
    }

    private void OnCloseClick()
    {
        UIManager.Instance.Close(this);
    }
}