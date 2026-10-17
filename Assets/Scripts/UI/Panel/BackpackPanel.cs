using UnityEngine;
//*****************************************
//创建人： DaDi 
//功能说明：
//***************************************** 

public class BackpackPanel : PanelBase
{
    [Header("槽位")]
    public ItemSlot[] _itemSlots;

    [SerializeField] private Sprite _emptySprite;

    // 背包是模态面板，挡住下层
    public override bool IsModal => true;

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
        Time.timeScale = 0f;
        RefreshAll();
    }

    public override void OnClose()
    {
        Time.timeScale = 1f;      
        base.OnClose();
        var controller = UIManager.Instance.Get<TalkPanel>()?.Dialogue;
        controller?.ResumeCurrentNode();
    }

    // 按 ESC 关闭背包，并消费这次 ESC
    public override bool OnEscapePressed()
    {
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 按 ESC 关闭音效
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
    public static bool TryConsumeItem(int itemId, int count = 1)
    {
        // 尝试消耗
        bool success = InventoryModel.Instance.TryConsumeById(itemId, count);

        // ★ 如果消耗成功，播放道具使用音效
        if (success)
        {
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_ItemUse);
        }

        return success;
    }

}
