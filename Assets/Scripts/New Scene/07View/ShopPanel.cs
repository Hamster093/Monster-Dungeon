/****************************************************
    文件：ShopPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/4 13:43:04
	功能：商店面板（支持物品商店 / 魔物商店两类）
*****************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#region 数据类

/// <summary>右键商店商品时，传给 ShopItemActionPanel 的数据</summary>
public class ShopActionData
{
    public ItemData data;
    public int price;
    public int stock;
    public Action onBuy;
}

/// <summary>商店分类</summary>
public enum ShopCategory
{
    Item,       // 物品商店
    Monster,    // 魔物商店
}

/// <summary>打开 ShopPanel 时传给 OnOpen 的数据</summary>
public class ShopOpenData
{
    public ShopCategory category;
}

#endregion


public class ShopPanel : PanelBase
{
    #region 字段 / 配置

    [Header("槽位")]
    public ItemSlot[] _itemSlots;
    [SerializeField] private Sprite _emptySprite;

    [Header("商品配置：填 items.json 里的 id，顺序即槽位顺序")]
    [SerializeField] private List<int> _itemShopIds = new();
    [SerializeField] private List<int> _monsterShopIds = new();

    [Header("拖拽配置")]
    [SerializeField] private ItemView _itemViewPrefab;
    [SerializeField] private float _cellSize;
    [SerializeField] private float _spacing;
    [SerializeField] private RectTransform _dragLayer;

    [Header("按钮")]
    [SerializeField] private Button _closeButton;

    /// <summary>当前商店分类（物品 / 魔物）</summary>
    private ShopCategory _currentCategory = ShopCategory.Item;

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

    public override bool IsModal => false;

    #endregion


    #region 生命周期

    public override void OnInit()
    {
        var dragLayer = GetOrCreateDragLayer();   // 创建拖拽层（Canvas 顶层）

        for (int i = 0; i < _itemSlots.Length; i++)
        {
            // 数据源指向商店自己
            _itemSlots[i].Bind(i, _emptySprite, GetGoodsStack);
            // 左键：商店选中逻辑
            _itemSlots[i].OnLeftClickedOverride = OnSlotLeftClicked;
            // 右键：弹自己的商店操作面板
            _itemSlots[i].OnRightClickedOverride = OnSlotRightClicked;

            // 挂拖拽器
            var dragger = _itemSlots[i].GetComponent<ShopSlotDragger>();
            if (dragger == null) dragger = _itemSlots[i].gameObject.AddComponent<ShopSlotDragger>();

            dragger.SlotIndex = i;
            dragger.Panel = this;
            dragger.Board = FindObjectOfType<BackpackBoard>();
            dragger.DragLayer = dragLayer;
            dragger.ItemViewPrefab = _itemViewPrefab;
            dragger.CellSize = 95f;
            dragger.Spacing = 5f;
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(() => UIManager.Instance.Close(this));
        }

        StartCoroutine(PreloadOwnIcons());   // 预加载图标
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

        _currentCategory = data switch
        {
            ShopOpenData d => d.category,
            ShopCategory c => c,
            _ => ShopCategory.Item,
        };

        BuildSlots();   // 内部根据 _currentCategory 选源

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

    #endregion


    #region 静态入口

    public static void OpenItemShop() => OpenShop(ShopCategory.Item);
    public static void OpenMonsterShop() => OpenShop(ShopCategory.Monster);

    /// <summary>
    /// 打开商店：若面板已打开则直接切分类，避免重开/闪烁
    /// </summary>
    private static void OpenShop(ShopCategory category)
    {
        var ui = UIManager.Instance;

        // 已打开 → 直接切分类
        if (ui.IsOpen<ShopPanel>())
        {
            var panel = ui.Get<ShopPanel>();
            panel?.SwitchCategory(category);
            return;
        }

        ui.Open<ShopPanel>(new ShopOpenData { category = category });
    }

    /// <summary>切到另一类商店（面板已打开时调用，避免重开）</summary>
    public void SwitchCategory(ShopCategory category)
    {
        if (_currentCategory == category) return;

        _currentCategory = category;
        DeselectAllSlots();
        BuildSlots();
        RefreshAll();
    }

    #endregion


    #region 数据构建

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

        // 根据分类选择配置来源，其余流程完全一致
        var sourceIds = _currentCategory == ShopCategory.Item
            ? _itemShopIds
            : _monsterShopIds;

        foreach (var id in sourceIds)
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

    #endregion


    #region 刷新 / 图标

    private void RefreshAll()
    {
        for (int i = 0; i < _itemSlots.Length; i++)
        {
            var slot = GetSlot(i);

            if (slot == null)
            {
                _itemSlots[i].gameObject.SetActive(false);
                continue;
            }

            if (slot.stock == 0)
            {
                _itemSlots[i].gameObject.SetActive(true);
                _itemSlots[i].Refresh(null, _emptySprite);   // 空图
                _itemSlots[i].SetSubText("售罄");
                _itemSlots[i].SetGray(true);                 // ← 变灰
                continue;
            }

            _itemSlots[i].gameObject.SetActive(true);
            _itemSlots[i].Refresh(slot.stack, _emptySprite);
            _itemSlots[i].SetSubText($"{slot.config.price}G");
            _itemSlots[i].SetGray(false);                    // ← 恢复
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

    /// <summary>面板初始化时后台预热两类商品的图标</summary>
    private IEnumerator PreloadOwnIcons()
    {
        var db = ConfigDatabase.Instance;
        if (db == null) yield break;

        foreach (var id in _itemShopIds)
            if (db.Items.TryGetValue(id, out var cfg) && !string.IsNullOrEmpty(cfg.iconAddress))
                yield return SpriteLoader.LoadAsync(cfg.iconAddress);

        foreach (var id in _monsterShopIds)
            if (db.Items.TryGetValue(id, out var cfg) && !string.IsNullOrEmpty(cfg.iconAddress))
                yield return SpriteLoader.LoadAsync(cfg.iconAddress);
    }

    #endregion


    #region 交互（点击 / 购买）

    public void DeselectAllSlots()
    {
        foreach (var slot in _itemSlots) slot.SetSelected(false);
        _selectedIndex = -1;
    }

    /// <summary>左键：物品选中；魔物弹出详情</summary>
    private void OnSlotLeftClicked(int index)
    {
        var slot = GetSlot(index);
        if (slot == null || slot.stock == 0) return;

        // 魔物：左键只弹详情，不参与选中
        if (slot.config.type == ItemType.Monster)
        {
            UIManager.Instance.Open<DescriptionPanel>(slot.data);
            return;
        }

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

    /// <summary>右键：物品弹出商店操作面板（查看/购买）；魔物无响应</summary>
    private void OnSlotRightClicked(int index)
    {
        var slot = GetSlot(index);
        if (slot == null || slot.stock == 0) return;

        if (slot.config.type == ItemType.Monster) return;

        UIManager.Instance.Open<ShopItemActionPanel>(new ShopActionData
        {
            data = slot.data,
            price = slot.config.price,
            stock = slot.stock,
            onBuy = () => TryBuy(index),
        });
    }

    /// <summary>真正的购买逻辑（右键菜单 → 进背包）</summary>
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

    /// <summary>给外部用的购买入口（拖拽到棋盘时调用，不进背包）</summary>
    public bool TryBuyForDrag(int index)
    {
        var slot = GetSlot(index);
        if (slot == null || slot.stock == 0) return false;

        var cfg = slot.config;
        if (!SystemManager.Instance.Economy.SpendCash(cfg.price)) return false;

        // 注意：这里不再往 InventoryModel 加物品了！
        // 因为现在是拖拽到棋盘，物品由 ShopSlotDragger 直接生成到棋盘
        if (slot.stock > 0) slot.stock--;

        DeselectAllSlots();
        RefreshAll();
        return true;
    }

    /// <summary>库存是否允许购买</summary>
    public bool CanBuy(int index)
    {
        var s = GetSlot(index);
        return s != null && s.stock != 0;
    }

    #endregion


    #region 给 ShopSlotDragger 的访问接口

    /// <summary>给 ShopSlotDragger 拿数据用</summary>
    public ItemData GetSlotData(int index)
    {
        var s = GetSlot(index);
        return s?.data;
    }

    public ItemShapeDefinition GetSlotShape(int index)
    {
        var s = GetSlot(index);
        return s?.data?.shape;
    }

    #endregion


    #region 拖拽层

    /// <summary>
    /// 创建、获取拖拽层 DragLayer，确保存在一个独立的顶层 DragLayer 用于挂拖拽幽灵
    /// </summary>
    private RectTransform GetOrCreateDragLayer()
    {
        if (_dragLayer != null) return _dragLayer;

        var rootCanvas = UIManager.Instance.RootCanvas;
        if (rootCanvas == null)
        {
            Debug.LogError("[ShopPanel] UIManager 未初始化或找不到根 Canvas");
            return null;
        }

        var t = rootCanvas.transform.Find("DragLayer_Runtime");
        if (t != null)
        {
            _dragLayer = t as RectTransform;
            return _dragLayer;
        }

        var go = new GameObject("DragLayer_Runtime", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(rootCanvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();

        _dragLayer = rect;
        return _dragLayer;
    }

    #endregion
}