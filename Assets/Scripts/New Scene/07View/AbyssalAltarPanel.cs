/****************************************************
    文件：AbyssalAltarPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/4 18:31:08
	功能：深渊祭坛面板
*****************************************************/

using System;
using UnityEngine;
using UnityEngine.UI;

public class AbyssalAltarPanel : PanelBase
{
    [Header("左侧按钮")]
    [SerializeField] private Button _newMonsterButton;
    [SerializeField] private Button _returnButton;

    [Header("关闭")]
    [SerializeField] private Button _closelButton;

    public override bool IsModal => false;

    public override void OnInit()
    {
        _newMonsterButton.onClick.AddListener(OnNewMonsterClick);
        _returnButton.onClick.AddListener(OnReturnClick);
        if (_closelButton != null)
            _closelButton.onClick.AddListener(OnClosePanelClick);
    }

    public override void OnDestroy()
    {
        _newMonsterButton.onClick.RemoveListener(OnNewMonsterClick);
        _returnButton.onClick.RemoveListener(OnReturnClick);
        if (_closelButton != null)
            _closelButton.onClick.RemoveListener(OnClosePanelClick);
        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        // 地狱面板现在由 UIManager 管，不再自己 SetActive
    }

    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    // ---------- 按钮回调 ----------
    private void OnNewMonsterClick() => ShopPanel.OpenMonsterShop();

    private void OnReturnClick()
    {
        // 打开分解面板
        Debug.Log("[AbyssalAltarPanel] 打开分解面板1");
        UIManager.Instance.Open<DecomposePanel>();
    }

    private void OnClosePanelClick()
    {
        // 顺手关掉分解窗口，避免它独立残留
        UIManager.Instance.Close<DecomposePanel>();
        UIManager.Instance.Close(this);
    }
}