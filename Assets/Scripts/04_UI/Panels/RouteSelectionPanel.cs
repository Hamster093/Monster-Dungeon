using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 指路只展示当天的原棋盘：临时搬到模态层，关闭后完整归位。
/// 不复制棋盘、不增删魔物；正式接单由确认回调统一提交。
/// </summary>
public sealed class RouteSelectionPanel : PanelBase
{
    [Serializable]
    public sealed class RouteState
    {
        public int contractId, adventurerId;
        public bool isSpecial, detailOpen;
        public string selectedUid;
    }
    public sealed class OpenData
    {
        public RouteState state;
        public Action<PlacedItem> confirmed;
        public Action cancelled;
    }

    [SerializeField] private RectTransform _boardHost;
    [SerializeField] private Button _returnButton;
    [SerializeField] private Text _instruction;
    public static RouteSelectionPanel Active { get; private set; }
    public static bool IsShowing => Active != null && Active.IsOpen;
    public RouteState State => _data?.state;
    public override bool IsModal => true;
    private OpenData _data;
    private RectTransform _boardVisual;
    private Transform _oldParent;
    private int _oldSibling;
    private Vector2 _oldAnchorMin, _oldAnchorMax, _oldPivot, _oldSize, _oldPosition;
    private Vector3 _oldScale;
    private bool _completed;

    public override void OnInit()
    {
        _returnButton.onClick.AddListener(() => UIManager.Instance.Close(this));
    }
    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        _data = data as OpenData;
        _completed = false;
        Active = this;
        // BoardManager、格子、魔物和外框都在原 GridBackpack 下，必须一起搬动。
        var board = BackpackBoard.Instance;
        _boardVisual = board != null ? board.transform.parent as RectTransform : null;
        if (_boardVisual != null)
        {
            _oldParent = _boardVisual.parent; _oldSibling = _boardVisual.GetSiblingIndex();
            _oldAnchorMin = _boardVisual.anchorMin; _oldAnchorMax = _boardVisual.anchorMax;
            _oldPivot = _boardVisual.pivot; _oldSize = _boardVisual.sizeDelta;
            _oldPosition = _boardVisual.anchoredPosition; _oldScale = _boardVisual.localScale;
            _boardVisual.SetParent(_boardHost, false);
            _boardVisual.anchorMin = _boardVisual.anchorMax = _boardVisual.pivot = new Vector2(.5f, .5f);
            _boardVisual.anchoredPosition = Vector2.zero; _boardVisual.localScale = Vector3.one;
        }
        RefreshInstruction();
        if (State?.detailOpen == true) StartCoroutine(RestoreDetail());
    }
    private System.Collections.IEnumerator RestoreDetail()
    {
        // UIManager 完成本面板入栈后再开二级详情，否则父弹窗会反盖住详情。
        yield return null;
        if (!IsOpen || State == null) yield break;
        var item = FindSelected();
        if (item != null&&!item.IsResting) ShowDetails(item); else State.detailOpen = false;
    }
    private void RefreshInstruction()
    {
        var board=BackpackBoard.Instance;
        int total=0,available=0;
        if(board!=null)
            foreach(var view in board.Items)
                if(view!=null&&!view.IsGhost&&view.Item!=null)
                {total++;if(!view.Item.IsResting)available++;}
        // 仅排除今日已出战的实例，不筛掉错误类别；欺骗指路仍由原业务判断。
        _instruction.text=total==0?"当前棋盘没有魔物，请返回委托界面":available==0?"今日魔物均已出战，魔物状态：疲惫，请返回委托界面":
            available<total?"精神充沛 "+available+"／"+total+"，疲惫魔物本日无法指路":"选择一只魔物，查看信息后确认指路";
    }

    /// <summary>仅实际占用格可选，旋转形状的空洞不能选中魔物。</summary>
    public void SelectAt(ItemView view, PointerEventData pointer)
    {
        var board = BackpackBoard.Instance;
        if (!IsOpen || view == null || view.Item == null || State == null || board == null) return;
        if (!board.ScreenToBoardCoord(pointer.position, pointer.pressEventCamera, out var coord)) return;
        if (board.GetCell(coord)?.Occupant != view.Item) return;
        if(view.Item.IsResting)
        {
            RefreshInstruction();
            ToastManager.ShowFeedback("魔物状态：疲惫，今日无法再次指路。","route:resting:"+view.Item.Uid);
            return;
        }
        State.selectedUid = view.Item.Uid;
        ShowDetails(view.Item);
        SaveContext();
    }

    private PlacedItem FindSelected() => BackpackBoard.Instance?.Items
        .FirstOrDefault(v => v != null && v.Item != null && v.Item.Uid == State?.selectedUid)?.Item;

    private void ShowDetails(PlacedItem item)
    {
        if (!int.TryParse(item.Def.Id, out int id) || !ConfigDatabase.Instance.Items.TryGetValue(id, out var config)) return;
        State.detailOpen = true;
        UIManager.Instance.Open<DescriptionPanel>(new DescriptionPanel.OpenContext
        {
            item = ItemData.FromMonster(item), icon = item.Def.GetIcon(item.Evolved),
            combatAdventurerId=State.adventurerId,
            contractId=State.contractId,
            onConfirm = ConfirmSelection,
            onReturn = () => { if (State != null) State.detailOpen = false; SaveContext(); }
        });
    }

    private void ConfirmSelection()
    {
        if (_completed || !IsOpen) return;
        var item = FindSelected();
        if (item == null) { ToastManager.ShowFeedback("这只魔物已经不在棋盘上，请重新选择。"); return; }
        if(item.IsResting){RefreshInstruction();ToastManager.ShowFeedback("魔物状态：疲惫，今日无法再次指路。");return;}
        _completed = true;
        var callback = _data.confirmed;
        // 先还原棋盘与模态层，再运行剧情回调，避免新页面被旧关闭流程覆盖。
        UIManager.Instance.Close<DescriptionPanel>();
        UIManager.Instance.Close(this);
        callback?.Invoke(item);
    }

    private static void SaveContext()
    {
        if (SystemManager.Instance?.Narrative?.Enabled == true) SystemManager.Instance.Narrative.Save();
    }

    public override bool OnEscapePressed() { UIManager.Instance.Close(this); return true; }
    public override void OnClose()
    {
        if (!IsOpen) return;
        if (UIManager.Instance.Get<DescriptionPanel>()?.IsRouteContext == true) UIManager.Instance.Close<DescriptionPanel>();
        if (_boardVisual != null && _oldParent != null)
        {
            _boardVisual.SetParent(_oldParent, false); _boardVisual.SetSiblingIndex(_oldSibling);
            _boardVisual.anchorMin = _oldAnchorMin; _boardVisual.anchorMax = _oldAnchorMax;
            _boardVisual.pivot = _oldPivot; _boardVisual.sizeDelta = _oldSize;
            _boardVisual.anchoredPosition = _oldPosition; _boardVisual.localScale = _oldScale;
        }
        var cancel = !_completed ? _data?.cancelled : null;
        _data = null; _boardVisual = null; Active = null;
        base.OnClose();
        cancel?.Invoke();
    }
    public override void OnDestroy()
    {
        if (Active == this) Active = null;
        base.OnDestroy();
    }
}
