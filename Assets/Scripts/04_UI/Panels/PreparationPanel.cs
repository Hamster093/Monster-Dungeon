/****************************************************
    文件：PreparationPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-10-04 13:19:04
	功能：整备界面
*****************************************************/

using System;
using UnityEngine;
using UnityEngine.UI;

public class PreparationPanel : PanelBase
{
    #region 字段

    [Header("按钮")]
    [SerializeField] private Button _startReceptionButton;
    [SerializeField] private Button _shopButton;
    [SerializeField] private Button _abyssButton;
    [SerializeField] private Button _monsterBackpackButton;
    [SerializeField] private Button _calculateIncomeButton;
    [SerializeField] private Text _incomeText;
    [SerializeField] private Button _specialPreviewButton;
    private float _nextIncomeRefresh;

    public override bool IsModal => false;

    #endregion


    #region 生命周期

    public override void OnInit()
    {
        base.OnInit();
        BindEvents();
        if (_specialPreviewButton) _specialPreviewButton.gameObject.SetActive(false); // 已撤除特殊委托入口。
        if (_calculateIncomeButton) _calculateIncomeButton.onClick.AddListener(RefreshIncome);
        BackpackChanged.Register(RefreshIncome);

        SystemManager.Instance.VisitSystem.OnCurrentVisitorChanged += OnVisitorChanged;
    }

    public override void OnDestroy()
    {
        SystemManager.Instance.VisitSystem.OnCurrentVisitorChanged -= OnVisitorChanged;
        UnbindEvents();
        if (_calculateIncomeButton) _calculateIncomeButton.onClick.RemoveListener(RefreshIncome);
        BackpackChanged.UnRegister(RefreshIncome);
        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        // 打开时按当前访客刷新一次（进对话回来/跨天后重新显示）
        OnVisitorChanged(SystemManager.Instance.VisitSystem.CurrentVisitorId);
        RefreshIncome();
    }

    public override void OnClose()
    {
        base.OnClose();
    }

    private void OnVisitorChanged(int id)
    {
        // 这里刷界面上显示"当前访客"的字段，比如来访者名字 / 立绘
        // 如果整备界面不显示访客信息，这个方法可以留空
        Debug.Log($"[PreparationPanel] 当前访客: {id}");
    }

    #endregion


    #region 按钮绑定

    private void BindEvents()
    {
        _startReceptionButton.onClick.AddListener(OnStartReceptionClick);
        _shopButton.onClick.AddListener(OnShopClick);
        _abyssButton.onClick.AddListener(OnAbyssClick);
        _monsterBackpackButton.onClick.AddListener(OnmonsterBackpack);
    }

    private void UnbindEvents()
    {
        _startReceptionButton.onClick.RemoveListener(OnStartReceptionClick);
        _shopButton.onClick.RemoveListener(OnShopClick);
        _abyssButton.onClick.RemoveListener(OnAbyssClick);
        _monsterBackpackButton.onClick.RemoveListener(OnmonsterBackpack);
    }

    #endregion


    #region 按钮回调

    /// <summary>开始接待</summary>
    private void OnStartReceptionClick()
    {
        if(BackpackBoard.Instance?.IsEditing==true)return;
        if (SystemManager.Instance.Narrative?.Enabled == true)
        {
            if (!SystemManager.Instance.Narrative.IsPreparing) return;
            SystemManager.Instance.Narrative.StartReception();
            return;
        }
        var vs = SystemManager.Instance.VisitSystem;
        string key = vs.GetCurrentDialogueKey();

        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[PreparationPanel] 当前没有访客，无法开始接待");
            return;
        }

        if (UIManager.Instance.IsOpen<ShopPanel>())
        {
            UIManager.Instance.Close<ShopPanel>();
        }

        var panel = UIManager.Instance.Open<TalkPanel>();
        panel.Dialogue.StartDialogue(key);
    }

    /// <summary>接待期间锁定开始按钮，棋盘所在的整备面板保持启用。</summary>
    public void SetReceptionAvailable(bool available)
    {
        // 只锁控件，不能关闭棋盘，否则 OnDisable 会撤销魔物登记。
        foreach (var button in new[] { _startReceptionButton, _shopButton, _abyssButton, _monsterBackpackButton })
            if (button != null) { button.interactable = available; button.gameObject.SetActive(available); }
        _startReceptionButton.transform.parent.gameObject.SetActive(available);
    }
    public void SetRightCovered(bool covered)
    { _startReceptionButton.transform.parent.gameObject.SetActive(!covered && (SystemManager.Instance.Narrative == null || SystemManager.Instance.Narrative.IsPreparing)); }
    private void RefreshIncome()
    {
        // 只预览原结算器的结果，不接单、不进行检定，也不发放金币。
        if (!_incomeText || BackpackBoard.Instance == null || ConfigDatabase.Instance == null) return;
        string income = DailyIncomeCalculator.Compute().monsterBusinessIncome.ToString();
        if (_incomeText.text != income) _incomeText.text = income;
    }
    private void Update()
    {
        if(_startReceptionButton&&SystemManager.Instance.Narrative?.IsPreparing==true)
            _startReceptionButton.interactable=BackpackBoard.Instance?.IsEditing!=true;
        // 沿用原收益数字。合法摆放及实例状态改变后刷新，拖动过程中不显示临时位置。
        if (Time.unscaledTime >= _nextIncomeRefresh && BackpackBoard.Instance?.IsEditing != true)
        {
            _nextIncomeRefresh = Time.unscaledTime + .2f;
            RefreshIncome();
        }
    }

    /// <summary>打开商店</summary>
    private void OnShopClick() => ShopPanel.OpenItemShop();

    /// <summary>进入深渊</summary>
    private void OnAbyssClick()
    {
        UIManager.Instance.Open<AbyssalAltarPanel>();
    }

    private void OnmonsterBackpack()
    {
        ShopPanel.OpenMonsterStorage();//打开魔物商店
    }

    #endregion
}
