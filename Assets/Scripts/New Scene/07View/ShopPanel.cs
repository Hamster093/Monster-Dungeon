/****************************************************
    文件：ShopPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/4 13:43:04
	功能：商店面板（支持物品商店 / 魔物商店两类）//兼职魔物仓库（本质为不花钱的魔物商店）
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
    public List<MonsterInstance> customMonsters;
    public bool freeMode;               // true → 不扣钱，拖走时从仓库移除
    public string title;                // 自定义标题
}

#endregion


public class ShopPanel : PanelBase
{
    #region 字段 / 配置

    [Header("标题")]
    [SerializeField] private Text _titleText;
    [SerializeField] private string _itemShopTitle = "商店";
    [SerializeField] private string _monsterShopTitle = "魔物商店";
    [SerializeField] private string _monsterStorageTitle = "魔物仓库";

    [Header("槽位")]
    public ItemSlot[] _itemSlots;
    [SerializeField] private Sprite _emptySprite;

    [Header("商品配置：填 items.json 里的 id，顺序即槽位顺序")]
    [SerializeField] private List<int> _itemShopIds = new();
    [SerializeField] private List<int> _monsterShopIds = new();

    //  运行时库存缓存（商品ID -> 当前库存）
    private static readonly Dictionary<int, int> _runtimeStocks = new Dictionary<int, int>();

    [Header("拖拽配置")]
    [SerializeField] private ItemView _itemViewPrefab;
    [SerializeField] private float _cellSize;
    [SerializeField] private float _spacing;
    [SerializeField] private RectTransform _dragLayer;

    [Header("仓库容量（仅仓库模式）")]
    [SerializeField] private int _storageCapacity = 20;
    /// <summary>仓库是否已满</summary>
    public bool IsStorageFull => _freeMode && MonsterStorageModel.Instance.Items.Count >= _storageCapacity;

    [Header("按钮")]
    [SerializeField] private Button _closeButton;

    //=====运行时候状态
    private List<MonsterInstance> _customMonsters;   // 仓库模式：与槽位一一对应的实例列表
    private bool _freeMode;

    public bool IsStorageMode => _freeMode;

    /// <summary>当前商店分类（物品 / 魔物）</summary>
    private ShopCategory _currentCategory = ShopCategory.Item;


    /// <summary>运行时槽位数据（配置 + 当前库存）</summary>
    private class ShopSlot
    {
        public ItemConfig config;
        public ItemData data;
        public ItemStack stack;
        public int stock;

        public MonsterInstance instance;   // 仓库模式专用，指向具体实例
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
            _closeButton.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 关闭/返回音效
                UIManager.Instance.Close(this);
            });
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

        var openData = data as ShopOpenData;

        _currentCategory = openData?.category ?? ShopCategory.Item;
        _customMonsters = openData?.customMonsters;   
        _freeMode = openData?.freeMode ?? false;

        BuildSlots();
        _selectedIndex = -1;
        Time.timeScale = 0f;
        RefreshAll();
        RefreshTitle(openData?.title);
    }
    /// <summary>
    /// 刷新标题：优先用 customTitle，其次按分类显示默认标题，仓库模式显示仓库标题
    /// </summary>
    /// <param name="customTitle"></param>
    private void RefreshTitle(string customTitle)
    {
        if (_titleText == null) return;

        if (!string.IsNullOrEmpty(customTitle))
        {
            _titleText.text = customTitle;
            return;
        }

        _titleText.text = _freeMode
            ? _monsterStorageTitle
            : (_currentCategory == ShopCategory.Item ? _itemShopTitle : _monsterShopTitle);
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
        if (_currentCategory == category && !_freeMode) return;

        _currentCategory = category;
        _customMonsters = null;   // ← 清掉仓库模式
        _freeMode = false;
        DeselectAllSlots();
        BuildSlots();
        RefreshAll();
        RefreshTitle(null);
    }

    public static void OpenMonsterStorage()
    {
        var ui = UIManager.Instance;

        if (ui.IsOpen<ShopPanel>())
        {
            // 已开 → 直接切仓库模式，不重开
            var panel = ui.Get<ShopPanel>();
            panel?.SwitchToStorage();
            return;
        }

        ui.Open<ShopPanel>(new ShopOpenData
        {
            category = ShopCategory.Monster,
            customMonsters = new List<MonsterInstance>(MonsterStorageModel.Instance.Items),
            freeMode = true,
        });
    }

    /// <summary>面板已打开时，切到仓库模式</summary>
    public void SwitchToStorage()
    {
        _customMonsters = new List<MonsterInstance>(MonsterStorageModel.Instance.Items);
        _freeMode = true;
        _currentCategory = ShopCategory.Monster;

        DeselectAllSlots();
        BuildSlots();
        RefreshAll();
        RefreshTitle(null);
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

        // ==========================================
        // 模式 1：仓库模式（固定数量 1）
        // ==========================================
        if (_freeMode && _customMonsters != null)
        {
            foreach (var inst in _customMonsters)
            {
                if (!ConfigDatabase.Instance.Items.TryGetValue(inst.monsterId, out var cfg))
                {
                    Debug.LogError($"[ShopPanel] Items 表找不到 id={inst.monsterId}");
                    continue;
                }

                var data = ItemData.FromConfig(cfg);
                _slots.Add(new ShopSlot
                {
                    config = cfg,
                    data = data,
                    // ★ 仓库里的魔物都是 1 只，直接传 1
                    stack = new ItemStack { data = data, quantity = 1 },
                    stock = 1,
                    instance = inst,
                });
            }
            return;
        }

        // ==========================================
        // 模式 2：商店模式（无限或有限库存）
        // ==========================================
        var sourceIds = _currentCategory == ShopCategory.Item ? _itemShopIds : _monsterShopIds;
        foreach (var id in sourceIds)
        {
            if (!ConfigDatabase.Instance.Items.TryGetValue(id, out var cfg))
            {
                Debug.LogError($"[ShopPanel] items.json 中找不到商品 id = {id}");
                continue;
            }

            var data = ItemData.FromConfig(cfg);

            // ★ 如果缓存里没有，说明是第一次打开，用配置的初始库存
            if (!_runtimeStocks.ContainsKey(cfg.id))
            {
                _runtimeStocks[cfg.id] = cfg.stock;
            }

            int currentStock = _runtimeStocks[cfg.id];
            int displayQuantity = currentStock < 0 ? -1 : currentStock;

            _slots.Add(new ShopSlot
            {
                config = cfg,
                data = data,
                stack = new ItemStack { data = data, quantity = displayQuantity },
                stock = currentStock, // 使用缓存的库存
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
            {
                s.stock = s.config.restockPerPhase;
                _runtimeStocks[s.config.id] = s.stock; // ★ 补货时同步重置缓存
            }


        RefreshAll();
    }

    #endregion


    #region 刷新 / 图标

    private void RefreshAll()
    {
        // 仓库模式：显示到容量上限（空槽也显示），方便玩家拖回
        // 商店模式：只显示有货的槽
        int displayCount = _freeMode
            ? Mathf.Min(_storageCapacity, _itemSlots.Length)
            : _itemSlots.Length;

        for (int i = 0; i < _itemSlots.Length; i++)
        {
            // 超出显示数量 → 隐藏
            if (i >= displayCount)
            {
                _itemSlots[i].gameObject.SetActive(false);
                continue;
            }

            var slot = GetSlot(i);

            // ---- 仓库模式：空槽也显示 ----
            if (_freeMode)
            {
                _itemSlots[i].gameObject.SetActive(true);

                if (slot == null)
                {
                    // 空槽
                    _itemSlots[i].Refresh(null, _emptySprite);
                    _itemSlots[i].SetSubText("");
                    _itemSlots[i].SetGray(false);
                    continue;
                }

                // 有魔物
                _itemSlots[i].Refresh(slot.stack, _emptySprite);
                _itemSlots[i].SetSubText("");               // 仓库不显示价格
                _itemSlots[i].SetGray(false);
                LoadIconAsync(i, slot.data.iconAddress);
                continue;
            }

            // ---- 商店模式 ----
            if (slot == null)
            {
                _itemSlots[i].gameObject.SetActive(false);
                continue;
            }

            if (slot.stock == 0)
            {
                _itemSlots[i].gameObject.SetActive(true);
                _itemSlots[i].Refresh(null, _emptySprite);
                _itemSlots[i].SetSubText("售罄");
                _itemSlots[i].SetGray(true);
                continue;
            }

            _itemSlots[i].gameObject.SetActive(true);
            _itemSlots[i].Refresh(slot.stack, _emptySprite);
            _itemSlots[i].SetSubText($"{slot.config.price}G");
            _itemSlots[i].SetGray(false);
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

        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Click); // ★ 通用点击音效
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

        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Click); // ★ 通用点击音效

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
        if (!SystemManager.Instance.Economy.SpendCash(cfg.price))
        {
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Invalid); // ★ 失败：金钱不足
            return;
        }

        // 2. 再进背包
        int remain = InventoryModel.Instance.AddItem(
            new ItemStack { data = slot.data, quantity = 1 });

        if (remain > 0)
        {
            Debug.Log("背包满了");
            Refund(cfg);       // 退款
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Invalid); // ★ 失败：背包已满
            return;
        }

        // 3. 减库存（-1 表示无限）
        if (slot.stock > 0)
        {
            slot.stock--;
            _runtimeStocks[cfg.id] = slot.stock; // 同步写回缓存
        }
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_GoldDown); // ★ 成功：扣除金币/购买成功
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

        // 仓库模式：按实例 uid 精确移除
        if (_freeMode)
        {
            if (slot.instance == null) return false;
            if (!MonsterStorageModel.Instance.RemoveByUid(slot.instance.uid)) return false;

            SwitchToStorage();   // 重建 + 刷新
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Confirm); // ★ 成功：从仓库拿出
            return true;
        }

        // 商店模式：扣钱
        if (!SystemManager.Instance.Economy.SpendCash(cfg.price))
        {
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Invalid); // ★ 失败：金钱不足
            return false;
        }
        if (slot.stock > 0)
        {
            slot.stock--;
            _runtimeStocks[cfg.id] = slot.stock; // 同步写回缓存
        }
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_GoldDown); // ★ 成功：扣除金币
        DeselectAllSlots();
        RefreshAll();
        return true;
    }
    /// <summary>给拖拽器用：获取槽位绑定的运行时实例（仓库模式才有）</summary>
    public MonsterInstance GetSlotInstance(int index)
    {
        var s = GetSlot(index);
        return s?.instance;
    }

    /// <summary>库存是否允许购买</summary>
    public bool CanBuy(int index)
    {
        var s = GetSlot(index);
        return s != null && s.stock != 0;
    }

    /// <summary>屏幕点是否落在任意一个可见槽位上；命中返回槽位索引</summary>
    public bool IsPointerOverAnySlot(Vector2 screenPos, Camera cam, out int hitIndex)
    {
        hitIndex = -1;
        if (_itemSlots == null) return false;

        for (int i = 0; i < _itemSlots.Length; i++)
        {
            var slot = _itemSlots[i];
            if (slot == null) continue;
            if (!slot.gameObject.activeInHierarchy) continue;

            var rt = slot.transform as RectTransform;
            if (rt == null) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam))
            {
                hitIndex = i;
                return true;
            }
        }
        return false;
    }
    #endregion


    #region 给 外部 的访问接口

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
    public ItemConfig GetSlotConfig(int index)
    {
        var s = GetSlot(index);
        return s?.config;
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