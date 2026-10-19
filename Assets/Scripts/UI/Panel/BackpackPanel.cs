using UnityEngine;
//*****************************************
//创建人： DaDi 
//功能说明：
//***************************************** 

public class BackpackPanel : PanelBase
{
    private System.Action _onReturned;
    public bool IsFunctionContext => _onReturned != null;
    [Header("槽位")]
    public ItemSlot[] _itemSlots;

    [SerializeField] private Sprite _emptySprite;

    // 背包是模态面板，挡住下层
    public override bool IsModal => false;

    public override void OnInit()
    {
        BackpackChanged.Register(RefreshAll);
        DeselectAllRequested.Register(DeselectAllSlots);

        // 把每个槽位和模型索引绑定
        for (int i = 0; i < _itemSlots.Length; i++)
            _itemSlots[i].Bind(i, _emptySprite);
    }

    public override void OnDestroy()
    {
        BackpackChanged.UnRegister(RefreshAll);
        DeselectAllRequested.UnRegister(DeselectAllSlots);
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);        
        _onReturned = data as System.Action;

        RefreshAll();
        foreach (var slot in _itemSlots)
            slot.OnLeftClickedOverride = _onReturned != null ? (System.Action<int>)OpenItemActions : null;
    }

    public override void OnClose()
    {
        UIManager.Instance.Close<ItemActionPanel>();
        UIManager.Instance.Close<DescriptionPanel>();
        foreach (var slot in _itemSlots) slot.OnLeftClickedOverride = null;
        base.OnClose();
        var callback = _onReturned; _onReturned = null;
        if (callback != null) callback();

    }

    public override void OnRefresh(object data = null)
    { _onReturned = data as System.Action; RefreshAll(); foreach (var slot in _itemSlots) slot.OnLeftClickedOverride = _onReturned != null ? (System.Action<int>)OpenItemActions : null; }

    private void OpenItemActions(int index)
    {
        var stack = InventoryModel.Instance.GetSlot(index);
        if (stack == null || stack.IsEmpty) return;
        UIManager.Instance.Open<ItemActionPanel>(new ItemActionData {
            data = stack.data,
            onUse = stack.data.id == 1 || stack.data.id == 4 ? (System.Action)(() => {
                if (SystemManager.Instance.Narrative.UseBagItem(stack.data.id))
                    UIManager.Instance.Close(this);
            }) : null,
            onDrop = () => InventoryModel.Instance.RemoveItem(index)
        });
    }

    // 按 ESC 关闭背包，并消费这次 ESC
    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    private void RefreshAll()
    {
        var model = InventoryModel.Instance;
        if (model == null) return;

        for (int i = 0; i < _itemSlots.Length; i++)
        {
            var stack = model.GetSlot(i);
            _itemSlots[i].Refresh(stack, _emptySprite);
        }
    }

    /// <summary>
    /// 取消选择所有槽位
    /// </summary>
    public void DeselectAllSlots()
    {
        foreach (var slot in _itemSlots)
            slot.SetSelected(false);
    }

  
}
