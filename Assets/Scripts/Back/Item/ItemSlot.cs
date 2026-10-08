/****************************************************
    文件：ItemSlot.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 15:52:25
	功能：Nothing
*****************************************************/

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 右键菜单数据：物品 + 回调
public class ItemActionData
{
    public ItemData data;
    public Action onDrop;
}

public class ItemSlot : MonoBehaviour ,IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    //=====物品格子=====//
    [SerializeField] private Text _quantityText;
    [SerializeField] private Image _itemImage;
    [SerializeField] public GameObject _selectedShader;
    [SerializeField] private Text _subText;//可选，商店格子显示价格

    private Func<int, ItemStack> _dataProvider;
    private ItemStack _stack;
    private Sprite _emptySprite;

    // 图标加载状态
    private string _iconAddress;
    private int _iconRequestId;

    //=====商店复用代码添加的逻辑，商店点击格子时会走 OnLeftClickedOverride 逻辑======
    /// <summary>面板可注册，注册后左键点击由面板接管，默认逻辑不再执行</summary>
    public System.Action<int> OnLeftClickedOverride;
    /// <summary>面板可注册，注册后右键点击由面板接管</summary>
    public System.Action<int> OnRightClickedOverride;

    public bool _thisItemSelected;
    public int SlotIndex { get; private set; }

    /// <summary>
    /// 由 BackpackPanel 在 OnInit 时绑定索引,商店传入自己的provider
    /// </summary>
    public void Bind(int index, Sprite emptySprite, Func<int, ItemStack> provider = null)
    {
        SlotIndex = index;
        _emptySprite = emptySprite;
        _dataProvider = provider ?? (i => InventoryModel.Instance.GetSlot(i));
        Refresh(InventoryModel.Instance.GetSlot(index), emptySprite);
    }

    /// <summary>刷新单个格子的显示</summary>
    public void Refresh(ItemStack stack, Sprite emptySprite)
    {
        _stack = stack;
        _emptySprite = emptySprite;
        // ---------- 空槽 ----------
        if (stack == null || stack.IsEmpty)
        {
            _iconAddress = null;
            _iconRequestId++;                       // 作废进行中的加载
            SetImage(_emptySprite);
            _quantityText.enabled = false;
            return;
        }
        // ---------- 数量 ----------
        _quantityText.enabled = stack.quantity > 1;
        _quantityText.text = stack.quantity.ToString();
        // ---------- 图标 ----------
        string addr = stack.iconAddress;

        if (string.IsNullOrEmpty(addr))
        {
            // 没地址，退回空图
            _iconAddress = null;
            _iconRequestId++;
            SetImage(_emptySprite);
        }
        else if (addr != _iconAddress || _itemImage.sprite == null)
        {
            // 地址变了，或上一次没加载成功 -> 重新加载
            _iconAddress = addr;
            LoadIconAsync(addr, ++_iconRequestId);
        }
        // 地址没变且已有图 -> 什么都不做（避免重复加载/闪烁）

    }

    private void SetImage(Sprite sprite)
    {
        _itemImage.sprite = sprite;
        _itemImage.enabled = sprite != null;
    }

    private async void LoadIconAsync(string address, int requestId)
    {
        var sp = await SpriteLoader.LoadAsync(address);

        // 对象可能已销毁 / 请求已过期 / 已被换图
        if (this == null || _itemImage == null) return;
        if (requestId != _iconRequestId) return;

        SetImage(sp != null ? sp : _emptySprite);
    }

    // 外部仍可直接设置（想覆盖自动加载时用）
    public void SetIcon(Sprite sprite)
    {
        _iconRequestId++;               // 作废自动加载
        SetImage(sprite);
    }

    // ---------- 选中 ----------

    public void SetSelected(bool selected)
    {
        _thisItemSelected = selected;
        _selectedShader.SetActive(selected);
    }

    // ---------- 交互 ----------
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            OnLeftClick();
        else if (eventData.button == PointerEventData.InputButton.Right)
            OnRightClick();
    }

    private void OnLeftClick()
    {
        if (OnLeftClickedOverride != null)
        {
            OnLeftClickedOverride.Invoke(SlotIndex);
            return;
        }

        DeselectAllRequested.Trigger();
        SetSelected(true);

        if (_stack == null || _stack.IsEmpty) return;

        if (_thisItemSelected)
        {
            // 使用逻辑
            // InventoryModel.Instance.UseItem(SlotIndex);
            
        }
    }
    private void OnRightClick()
    {
        if (OnRightClickedOverride != null)
        {
            OnRightClickedOverride.Invoke(SlotIndex);
            return;
        }

        if (_stack == null || _stack.IsEmpty) return;

        UIManager.Instance.Open<ItemActionPanel>(new ItemActionData
        {
            data = _stack.data,
            onDrop = () => DropItem()
        });
    }

    private void DropItem()
    {
        if (_stack == null || _stack.IsEmpty) return;

        // 生成掉落物？TODO

        //丢弃一个
        InventoryModel.Instance.RemoveItem(SlotIndex, 1);
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_stack == null || _stack.IsEmpty) return;
        UIManager.Instance.Open<NamePanel>(_stack.itemName);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UIManager.Instance.Close<NamePanel>();
    }

    public void SetSubText(string text)
    {
        if (_subText == null) return;
        _subText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        _subText.text = text;
    }

}