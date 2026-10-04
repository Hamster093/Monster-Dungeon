/****************************************************
    文件：ShopPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/4 13:43:04
	功能：商店面板
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 商店商品：物品 + 价格 + 库存
[Serializable]
public class ShopGoodsEntry
{
    public ItemData data;
    public int price = 100;
    [Tooltip("-1 无限库存")] public int stock = -1;
}

/// <summary>
/// 右键商店商品时，传给 ShopItemActionPanel 的数据
/// （对应 ItemActionData 的商店版）
/// </summary>
public class ShopActionData
{
    public ItemData data;
    public int price;
    public int stock;
    public Action onBuy;
}
public class ShopPanel : PanelBase
{
    [Header("槽位")]
    public ItemSlot[] _itemSlots;
    [SerializeField] private Sprite _emptySprite;

    [Header("商品配置（在编辑器填）")]
    [SerializeField] private List<ShopGoodsEntry> _goodsConfig = new();

    /// <summary>运行时槽位数据（配置 + 当前库存）</summary>
    private class ShopSlot
    {
        public ShopGoodsEntry entry;
        public ItemStack stack;
        public int stock;
    }
    private readonly List<ShopSlot> _slots = new();

    private int _selectedIndex = -1;

    public override bool IsModal => true;

    public override void OnInit()
    {
        BuildSlots();

        for (int i = 0; i < _itemSlots.Length; i++)
        {
            // 数据源指向商店自己
            _itemSlots[i].Bind(i, _emptySprite, GetGoodsStack);
            // 左键：商店选中逻辑
            _itemSlots[i].OnLeftClickedOverride = OnSlotLeftClicked;
            // 右键：弹自己的商店操作面板
            _itemSlots[i].OnRightClickedOverride = OnSlotRightClicked;
        }

    }

    public override void OnDestroy()
    {

        foreach (var slot in _itemSlots)
        {
            slot.OnLeftClickedOverride = null;
            slot.OnRightClickedOverride = null;
        }
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        _selectedIndex = -1;
        Time.timeScale = 0f;

        RefreshAll();
    }

    public override void OnClose()
    {
        Time.timeScale = 1f;
        base.OnClose();
    }

    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    // ---------- 数据 ----------

    /// <summary>
    /// 从 Inspector 配置构建运行时槽位
    /// </summary>
    private void BuildSlots()
    {
        _slots.Clear();

        foreach (var entry in _goodsConfig)
        {
            var stack = new ItemStack
            {
                data = entry.data, 
                quantity = 1,
            };

            _slots.Add(new ShopSlot
            {
                entry = entry,
                stack = stack,
                stock = entry.stock,
            });
        }
    }

    /// <summary>给 ItemSlot 用的数据源：库存为 0 或越界返回 null</summary>
    private ItemStack GetGoodsStack(int index)
    {
        if (index < 0 || index >= _slots.Count) return null;
        var s = _slots[index];
        if (s.stock == 0) return null;      // 售罄 -> 空槽
        return s.stack;
    }

    private ShopSlot GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Count) return null;
        return _slots[index];
    }

    // ---------- 刷新 ----------

    private void RefreshAll()
    {
        for (int i = 0; i < _itemSlots.Length; i++)
        {
            _itemSlots[i].Refresh(GetGoodsStack(i), _emptySprite);

            var slot = GetSlot(i);
            _itemSlots[i].SetSubText(
                slot != null && slot.stock != 0 ? $"{slot.entry.price}G" : "");
        }

        //RefreshSelectionUI();
    }

    public void DeselectAllSlots()
    {
        foreach (var slot in _itemSlots)
            slot.SetSelected(false);
        _selectedIndex = -1;
    }

    // ---------- 交互 ----------

    private void OnSlotLeftClicked(int index)
    {
        var slot = GetSlot(index);
        if (slot == null || slot.stock == 0) return;

        if (_selectedIndex == index)
        {
            _itemSlots[index].SetSelected(false);
            _selectedIndex = -1;
        }
        else
        {
            DeselectAllSlots();
            _selectedIndex = index;
            _itemSlots[index].SetSelected(true);
        }

    }

    /// <summary>
    /// 右键：弹出商店操作面板（查看/购买）。
    /// TODO: 换成你自己设计的面板类型
    /// </summary>
    private void OnSlotRightClicked(int index)
    {
        var slot = GetSlot(index);
        if (slot == null || slot.stock == 0) return;

        var entry = slot.entry;

        UIManager.Instance.Open<ShopItemActionPanel>(new ShopActionData
        {
            data = entry.data,
            price = entry.price,
            stock = slot.stock,
            onBuy = () => TryBuy(index),
        });
    }

    /// <summary>真正的购买逻辑</summary>
    private void TryBuy(int index)
    {
        var slot = GetSlot(index);
        if (slot == null || slot.stock == 0) return;

        var entry = slot.entry;
        int remain = InventoryModel.Instance.AddItem( new ItemStack { data = entry.data, quantity = 1 });

        if (remain > 0) { Debug.Log("背包满了"); return; }

        if (!SystemManager.Instance.Economy.SpendCash(entry.price)) return;

        // 减库存（-1 表示无限）
        if (slot.stock > 0) slot.stock--;

        DeselectAllSlots();
        RefreshAll();
    }
}