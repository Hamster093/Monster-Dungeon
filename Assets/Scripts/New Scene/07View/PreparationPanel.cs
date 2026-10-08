/****************************************************
    文件：PreparationPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-10-04 13:19:04
	功能：整备界面
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PreparationPanel : PanelBase
{
    [Header("按钮")]
    [SerializeField] private Button _startReceptionButton;
    [SerializeField] private Button _shopButton;
    [SerializeField] private Button _abyssButton;

    [Header("槽位")]
    [SerializeField] private MonsterSlot[] _slots;         // 长度 7，对应 Img_Monster1~7
    [SerializeField] private Sprite _emptySprite;

    [Header("拖拽 Ghost")]
    [SerializeField] private Image _ghostIcon;             // 放 Canvas 最上层，初始 SetActive(false)

    private int _selectedIndex = -1;

    public override bool IsModal => false;

    // ---------- 生命周期 ----------


    public override void OnInit()
    {
        base.OnInit();
        BindEvents();
        InitSlots();

        PartyModel.Instance.OnChanged += OnPartyChanged;
        SystemManager.Instance.VisitSystem.OnCurrentVisitorChanged += OnVisitorChanged;
    }
    public override void OnDestroy()
    {
        SystemManager.Instance.VisitSystem.OnCurrentVisitorChanged -= OnVisitorChanged;
        PartyModel.Instance.OnChanged -= OnPartyChanged;   // 退订
        UnbindEvents();
        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        _selectedIndex = -1;
        RefreshAll();

        // 打开时按当前访客刷新一次（进对话回来/跨天后重新显示）
        OnVisitorChanged(SystemManager.Instance.VisitSystem.CurrentVisitorId);
    }

    private void OnVisitorChanged(int id)
    {
        // 这里刷界面上显示"当前访客"的字段，比如来访者名字 / 立绘
        // 如果整备界面不显示访客信息，这个方法可以留空
        Debug.Log($"[PreparationPanel] 当前访客: {id}");
    }

    public override void OnClose()
    {
        HideGhost();
        base.OnClose();
    }

    // ---------- 按钮 ----------
    /// <summary>
    /// 绑定按钮监听
    /// </summary>
    private void BindEvents()
    {
        _startReceptionButton.onClick.AddListener(OnStartReceptionClick);
        _shopButton.onClick.AddListener(OnShopClick);
        _abyssButton.onClick.AddListener(OnAbyssClick);
    }

    /// <summary>
    /// 解绑按钮监听
    /// </summary>
    private void UnbindEvents()
    {
        _startReceptionButton.onClick.RemoveListener(OnStartReceptionClick);
        _shopButton.onClick.RemoveListener(OnShopClick);
        _abyssButton.onClick.RemoveListener(OnAbyssClick);
    }

    #region 按钮回调

    /// <summary>
    /// 开始接待
    /// </summary>
    private void OnStartReceptionClick()
    {
        var vs = SystemManager.Instance.VisitSystem;
        string key = vs.GetCurrentDialogueKey();

        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[PreparationPanel] 当前没有访客，无法开始接待");
            return;
        }

        var panel = UIManager.Instance.Open<TalkPanel>();
        panel.Dialogue.StartDialogue(key);   
    }

    /// <summary>
    /// 打开商店
    /// </summary>
    private void OnShopClick()
    {
        Debug.Log("[PreparationPanel] 点击商店");
        UIManager.Instance.Open<ShopPanel>();
    }

    /// <summary>
    /// 进入深渊
    /// </summary>
    private void OnAbyssClick()
    {
        Debug.Log("[PreparationPanel] 点击深渊");
        // TODO: 打开深渊界面
         UIManager.Instance.Open<AbyssalAltarPanel>();
    }

    #endregion

    // ---------- 槽位 ----------

    private void InitSlots()
    {
        if (_slots == null || _slots.Length == 0)
        {
            Debug.LogError("[PreparationPanel] _slots 未配置！");
            return;
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Bind(i, _emptySprite);
            _slots[i].OnLeftClickedOverride = OnSlotLeftClicked;
            _slots[i].OnRightClickedOverride = OnSlotRightClicked;
            _slots[i].OnSwapRequested = OnSwap;
            _slots[i].OnDragStarted = OnDragStarted;
            _slots[i].OnDragEnded = OnDragEnded;
        }

        if (_ghostIcon != null) _ghostIcon.gameObject.SetActive(false);
    }

    private void RefreshAll()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            var data = new MonsterSlotData { monsterId = PartyModel.Instance.Get(i) };
            _slots[i].Refresh(data, _emptySprite);
        }
    }

    /// <summary>数据变化时刷新（含被别的面板改的情况）</summary>
    private void OnPartyChanged()
    {
        if (_slots == null) return;
        RefreshAll();
    }

    // ---------- 槽位点击 ----------

    private void OnSlotLeftClicked(int index)
    {
        _selectedIndex = (_selectedIndex == index) ? -1 : index;
        for (int i = 0; i < _slots.Length; i++)
            _slots[i].SetSelected(i == _selectedIndex);
    }

    private void OnSlotRightClicked(int index)
    {
        if (PartyModel.Instance.Get(index) <= 0) return;

        //还没有这个面板 右键直接分解
        Decompose(index);
        //UIManager.Instance.Open<MonsterActionPanel>(new MonsterActionData
        //{
        //    monsterId = PartyModel.Instance.Get(index),
        //    onDecompose = () => Decompose(index),
        //});
    }

    // ---------- 拖拽 ----------

    private void OnDragStarted(MonsterSlot src)
    {
        if (_ghostIcon == null) return;

        var cfg = src.Data.Config;
        if (cfg == null) return;

        _ghostIcon.gameObject.SetActive(true);
        LoadGhostIconAsync("Icons/WWM");
        //LoadGhostIconAsync($"Icons/Monsters/{cfg.id}");
    }

    private async void LoadGhostIconAsync(string addr)
    {
        var sp = await SpriteLoader.LoadAsync(addr);
        if (this == null || _ghostIcon == null) return;

        _ghostIcon.sprite = sp;
        _ghostIcon.enabled = sp != null;
    }

    private void Update()
    {
        if (_ghostIcon != null && _ghostIcon.gameObject.activeSelf)
            _ghostIcon.transform.position = Input.mousePosition;
    }

    private void OnSwap(int fromIndex, int toIndex)
    {
        PartyModel.Instance.Swap(fromIndex, toIndex);   // 数据变了会触发 OnChanged → 自动刷新
    }

    private void OnDragEnded(MonsterSlot src)
    {
        HideGhost();
    }
    private void HideGhost()
    {
        if (_ghostIcon != null) _ghostIcon.gameObject.SetActive(false);
    }

    // ---------- 分解 ----------

    private void Decompose(int index)
    {
        int id = PartyModel.Instance.Get(index);
        if (id <= 0) return;

        var cfg = ConfigDatabase.Instance.Monsters.TryGetValue(id, out var c) ? c : null;
        if (cfg == null)
        {
            Debug.LogWarning($"[PreparationPanel] 找不到魔物配置 id = {id}");
            return;
        }

        // 加钱
        SystemManager.Instance.Economy.AddCash(cfg.materialValue);

        PartyModel.Instance.Set(index, 0);
        _selectedIndex = -1;
    }


}