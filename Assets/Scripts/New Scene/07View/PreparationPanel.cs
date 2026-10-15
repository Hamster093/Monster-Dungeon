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

    public override bool IsModal => false;

    #endregion


    #region 生命周期

    public override void OnInit()
    {
        base.OnInit();
        BindEvents();

        SystemManager.Instance.VisitSystem.OnCurrentVisitorChanged += OnVisitorChanged;
    }

    public override void OnDestroy()
    {
        SystemManager.Instance.VisitSystem.OnCurrentVisitorChanged -= OnVisitorChanged;
        UnbindEvents();
        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        // 打开时按当前访客刷新一次（进对话回来/跨天后重新显示）
        OnVisitorChanged(SystemManager.Instance.VisitSystem.CurrentVisitorId);
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