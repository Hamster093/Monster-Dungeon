/****************************************************
    文件：CharacterArchivePanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/2 12:22:31
	功能：
*****************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterArchivePanel : PanelBase
{
    [Header("滚动视图")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _content;
    [SerializeField] private DialogueHistoryEntry _entryPrefab;
    [SerializeField] private Button _closeButton;

    [Header("人物信息")]
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _speakerNameText;
    [SerializeField] private Image _iconImage;  //还没用过

    private readonly List<DialogueHistoryEntry> _pool
        = new List<DialogueHistoryEntry>();

    public override bool IsModal => true;

    private string _speakerName;

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

    /// <summary>
    /// data 传入说话人名字（speakerName）
    /// </summary>
    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        _speakerName = data as string;
        Rebuild();
    }

    private void Rebuild()
    {
        // 清空已有条目（回收到 pool，之后可以复用）
        for (int i = 0; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);

        if (string.IsNullOrEmpty(_speakerName))
        {
            if (_titleText != null) _titleText.text = "暂无记录";
            return;
        }

        var list = CharacterArchiveModel.Instance?.Get(_speakerName);
        if (list == null || list.Count == 0)
        {
            if (_titleText != null) _titleText.text = "暂无记录";
            return;
        }

        // 顶部：名字（头像按需自行加载）
        if (_titleText != null) _titleText.text = _speakerName + " 的档案";
        if (_speakerNameText != null) _speakerNameText.text = _speakerName;

        // 条目列表
        for (int i = 0; i < list.Count; i++)
        {
            var entry = GetOrCreateEntry(i);
            entry.Bind(list[i]);
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