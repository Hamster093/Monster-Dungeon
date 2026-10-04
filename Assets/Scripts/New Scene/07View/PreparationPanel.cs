/****************************************************
    文件：PreparationPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-10-04 13:19:04
	功能：整备界面
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

public class PreparationPanel : PanelBase
{
    [SerializeField] private Button _startReceptionButton;
    [SerializeField] private Button _shopButton;
    [SerializeField] private Button _abyssButton;

    [SerializeField] private string _dialogueKey = "npc_liuming";//json文件名

    public override void OnInit()
    {
        base.OnInit();
        BindEvents();
    }

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
        Debug.Log("[PreparationPanel] 点击开始接待");
        UIManager.Instance.Open<TalkPanel>().Dialogue.StartDialogue(_dialogueKey);
        OnClose();
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

    public override void OnClose()
    {
        UnbindEvents();
        base.OnClose();
    }

    public override void OnDestroy()
    {
        UnbindEvents();
        base.OnDestroy();
    }
}