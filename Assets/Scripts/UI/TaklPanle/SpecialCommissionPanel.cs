/****************************************************
    文件：DialogueCallbackBootstrap.cs
    作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/2 19:00:30
    功能：特殊委托面板（滑动列表 + 详情）
*****************************************************/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//SevenDimension 枚举 → 中文名映射
public static class SevenDimensionDisplay
{
    public static string GetName(SevenDimension d)
    {
        switch (d)
        {
            case SevenDimension.Reason: return "理智";
            case SevenDimension.Courage: return "勇气";
            case SevenDimension.Piety: return "虔诚";
            case SevenDimension.Tolerance: return "宽容";
            case SevenDimension.Benevolence: return "仁爱";
            case SevenDimension.Resolve: return "坚定";
            case SevenDimension.Diligence: return "勤勉";
            default: return d.ToString();
        }
    }
}

public class SpecialCommissionPanel : PanelBase
{
    [Header("滚动视图")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _content;
    [SerializeField] private SpecialCommissionItem _itemPrefab;

    [Header("详情面板（挂在当前面板下的子节点）")]
    [SerializeField] private SpecialCommissionDetailPanel _detailPanel;

    [Header("关闭")]
    [SerializeField] private Button _closeButton;

    public override bool IsModal => true;

    private readonly List<SpecialCommissionItem> _items = new List<SpecialCommissionItem>();

    public override void OnInit()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(OnCloseClick);
    }

    public override void OnDestroy()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(OnCloseClick);

        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        if (_detailPanel != null)
            _detailPanel.Hide();

        Rebuild();
    }

    public override bool OnEscapePressed()
    {
        // 详情开着 → 先关详情
        if (_detailPanel != null && _detailPanel.IsShowing)
        {
            _detailPanel.Hide();
            return true;
        }

        UIManager.Instance.Close(this);
        return true;
    }

    // ───────────── 列表构建 ─────────────

    private void Rebuild()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] != null) Destroy(_items[i].gameObject);
        }
        _items.Clear();

        var contracts = ConfigDatabase.Instance?.Contracts;
        if (contracts == null) return;

        foreach (var kvp in contracts)
        {
            var cfg = kvp.Value;
            if (cfg == null) continue;

            // 特殊委托 = influenceStats 非空
            if (cfg.influenceStats == null || cfg.influenceStats.Count == 0) continue;

            var item = Instantiate(_itemPrefab, _content);
            item.Bind(cfg, OnItemViewClicked);
            _items.Add(item);
        }

        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;   // 滚到顶
    }

    private void OnItemViewClicked(ContractConfig cfg)
    {
        if (_detailPanel == null) return;
        _detailPanel.Show(cfg, OnAcceptClicked);
    }

    private void OnAcceptClicked(ContractConfig cfg)
    {
        if (cfg == null) return;

        var sys = SystemManager.Instance?.DailyCommission;
        if (sys == null)
        {
            Debug.LogError("[SpecialCommissionPanel] DailyCommission 为空");
            return;
        }

        int adventurerId = SystemManager.Instance.VisitSystem.CurrentVisitorId;
        sys.AddSpecial(cfg.id, adventurerId);

        ToastManager.Show("特殊委托接取成功");
        Debug.Log($"[SpecialCommissionPanel] 接取特殊委托 id={cfg.id}, adv={adventurerId}");

        if (_detailPanel != null) _detailPanel.Hide();
    }

    private void OnCloseClick()
    {
        UIManager.Instance.Close(this);
    }

    public override void OnClose()
    {
        base.OnClose();

        var controller = UIManager.Instance.Get<TalkPanel>()?.Dialogue;
        controller?.ResumeCurrentNode();
    }
}