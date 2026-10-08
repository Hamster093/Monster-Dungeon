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

    [Header("商品配置：填 items.json 里的 id，顺序即槽位顺序")]
    [SerializeField] private List<int> _goodsIds = new();

    /// <summary>运行时槽位数据（配置 + 当前库存）</summary>
    private class ShopSlot
    {
        public ItemConfig config;
        public ItemData data;
        public ItemStack stack;
        public int stock;
    }
    private readonly List<ShopSlot> _slots = new();
    private readonly Dictionary<string, Sprite> _iconCache = new();   // 本面板已加载的图标
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
        _iconCache.Clear();
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
    /// 从 ConfigDatabase 读取 items.json 构建运行时槽位
    /// </summary>
    private void BuildSlots()
    {
        _slots.Clear();

        if (ConfigDatabase.Instance == null)
        {
            Debug.LogError("[ShopPanel] ConfigDatabase 尚未初始化");
            return;
        }

        foreach (var id in _goodsIds)
        {
            if (!ConfigDatabase.Instance.Items.TryGetValue(id, out var cfg))
            {
                Debug.LogError($"[ShopPanel] items.json 中找不到商品 id = {id}");
                continue;
            }

            var data = ItemData.FromConfig(cfg);

            _slots.Add(new ShopSlot
            {
                config = cfg,
                data = data,
                stack = new ItemStack { data = data, quantity = 1 },
                stock = cfg.stock,     // -1 = 无限
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

    /// <summary>进入新经营阶段时调用：按配置补充库存</summary>
    public void RestockForNewPhase()
    {
        foreach (var s in _slots)
            if (s.config.restockPerPhase > 0)
                s.stock = s.config.restockPerPhase;

        RefreshAll();
    }

    // ---------- 刷新 ----------

    private void RefreshAll()
    {
        for (int i = 0; i < _itemSlots.Length; i++)
        {
            var slot = GetSlot(i);

            _itemSlots[i].Refresh(GetGoodsStack(i), _emptySprite);
            _itemSlots[i].SetSubText(
                slot != null && slot.stock != 0 ? $"{slot.config.price}G" : "");

            if (slot != null && slot.stock != 0)
                LoadIconAsync(i, slot.data.iconAddress);
        }
    }

    /// <summary>按需异步加载图标，命中缓存直接设置</summary>
    private async void LoadIconAsync(int index, string address)
    {
        if (string.IsNullOrEmpty(address)) return;
        if (index < 0 || index >= _itemSlots.Length) return;

        // 1. 命中本面板缓存
        if (_iconCache.TryGetValue(address, out var cached))
        {
            _itemSlots[index].SetIcon(cached);
            return;
        }

        // 2. 走全局 SpriteLoader（内部还有一层全局缓存）
        var sp = await SpriteLoader.LoadAsync(address);

        // await 返回后检查存活
        if (this == null || _itemSlots == null || index >= _itemSlots.Length) return;

        if (sp != null)
        {
            _iconCache[address] = sp;
            _itemSlots[index].SetIcon(sp);
        }
    }

    public void DeselectAllSlots()
    {
        foreach (var slot in _itemSlots) slot.SetSelected(false);
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

        UIManager.Instance.Open<ShopItemActionPanel>(new ShopActionData
        {
            data = slot.data,
            price = slot.config.price,
            stock = slot.stock,
            onBuy = () => TryBuy(index),
        });
    }

    /// <summary>真正的购买逻辑</summary>
    private void TryBuy(int index)
    {
        var slot = GetSlot(index);
        if (slot == null || slot.stock == 0) return;

        var cfg = slot.config;

        // 1. 先扣钱（返回值 false = 现金不够）
        if (!SystemManager.Instance.Economy.SpendCash(cfg.price)) return;

        // 2. 再进背包
        int remain = InventoryModel.Instance.AddItem(
            new ItemStack { data = slot.data, quantity = 1 });

        if (remain > 0)
        {
            Debug.Log("背包满了");
            Refund(cfg);       // 退款
            return;
        }

        // 3. 减库存（-1 表示无限）
        if (slot.stock > 0) slot.stock--;

        DeselectAllSlots();
        RefreshAll();
    }

    private void Refund(ItemConfig cfg)
    {
        var eco = SystemManager.Instance.Economy;
        switch (cfg.currency)
        {
            case CurrencyType.Cash:
                 eco.AddCash(cfg.price);
                break;
        }
    }
}